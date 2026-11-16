# coding=utf-8
#
# This file is part of StreamActions.
# Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
#
# StreamActions is free software: you can redistribute it and/or modify
# it under the terms of the GNU Affero General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# StreamActions is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU Affero General Public License for more details.
#
# You should have received a copy of the GNU Affero General Public License
# along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
#

"""
GitHub Actions workflow updater that automatically discovers workflow and composite action YAML files,
enforces the approved action vendor policy, and pins `uses:` references to the latest release's
40-character commit SHA with a `# <tag>` comment.
"""

from pathlib import Path
import re
import urllib.parse

from updaters.BaseUpdater import (
    ALLOWED_ACTION_OWNERS,
    BaseUpdater,
    DependencyUpdate,
    EcosystemResult,
    detect_newline_style,
    write_text_preserving_newlines,
)

_USES_LINE_RE = re.compile(
    r"^(?P<prefix>\s*(?:-\s*)?uses:\s*)(?P<q>[\"']?)"
    r"(?P<action>[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_./-]+)?)"
    r"@(?P<ref>[^\s#\"']+)(?P=q)(?P<comment>\s*#.*)?$"
)
_SHA40_RE = re.compile(r"^[0-9a-fA-F]{40}$")
_TAG_SEMVER_RE = re.compile(r"^v?(?P<major>\d+)(?:\.(?P<minor>\d+))?(?:\.(?P<patch>\d+))?(?P<pre>-[0-9A-Za-z.-]+)?$")
_SAFE_TAG_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._/-]{0,127}$")
"""
Permitted pattern for Git tag names (alphanumeric start, safe characters, length 1-128).
"""


def _validate_tag_name(tag_name: str) -> str:
    """
    Validate that a Git tag name contains only safe characters and cannot perform path traversal.

    Args:
        tag_name (str): Git tag name candidate.

    Returns:
        str: Stripped, validated tag name.

    Raises:
        RuntimeError: If the tag name is invalid, contains `..`, or does not match `_SAFE_TAG_RE`.
    """
    cleaned = tag_name.strip() if tag_name else ""
    if ".." in cleaned or not _SAFE_TAG_RE.fullmatch(cleaned):
        raise RuntimeError(f"Unsafe or invalid Git tag name: {tag_name!r}")
    return cleaned


def parse_action_repo(action_ref: str) -> tuple[str, str]:
    """
    Extract `(owner, repo)` from an action reference that may include a subpath.

    Args:
        action_ref (str): Action path such as `github/codeql-action/init`.

    Returns:
        tuple[str, str]: `(owner, repo)` pair such as `("github", "codeql-action")`.
    """
    parts = action_ref.split("/")
    return parts[0], parts[1]


def _parse_tag_semver(tag_name: str) -> tuple[int, int, int] | None:
    """
    Parse a non-prerelease SemVer tag such as `v6.0.1` or `v6` into a comparable tuple.

    Args:
        tag_name (str): Git tag string.

    Returns:
        tuple[int, int, int] | None: `(major, minor, patch)` tuple, or `None` if prerelease/non-semver.
    """
    match = _TAG_SEMVER_RE.match(tag_name.strip())
    if not match or match.group("pre"):
        return None
    return (
        int(match.group("major")),
        int(match.group("minor") or 0),
        int(match.group("patch") or 0),
    )


class GitHubActionsUpdater(BaseUpdater):
    """
    Discovers GitHub Actions YAML files, validates action owners against `ALLOWED_ACTION_OWNERS`,
    and resolves the latest release tag and commit SHA for each action repository.
    """

    ecosystem = "actions"

    def __init__(self, *args, **kwargs) -> None:
        """
        Initialize the GitHub Actions updater with a per-run repository resolution cache.

        Args:
            *args: Positional arguments forwarded to `BaseUpdater`.
            **kwargs: Keyword arguments forwarded to `BaseUpdater`.
        """
        super().__init__(*args, **kwargs)
        self._repo_cache: dict[str, tuple[str, str, str]] = {}

    def _discover_action_files(self) -> list[Path]:
        """
        Discover all workflow and composite action YAML files under `.github/workflows/` and `.github/actions/`.

        Returns:
            list[Path]: Sorted list of YAML file paths.
        """
        candidates = self.discover_files("*.yml", "*.yaml")
        matched: list[Path] = []
        for path in candidates:
            rel = self.rel_path(path)
            if rel.startswith(".github/workflows/") or (
                rel.startswith(".github/actions/") and path.name in ("action.yml", "action.yaml")
            ):
                matched.append(path)
        return matched

    def _resolve_tag_to_commit_sha(self, owner: str, repo: str, tag_name: str) -> str:
        """
        Resolve a Git tag on GitHub to its underlying 40-character commit SHA (dereferencing annotated tags).

        Args:
            owner (str): Repository owner.
            repo (str): Repository name.
            tag_name (str): Git tag name.

        Returns:
            str: 40-character lowercase commit SHA.

        Raises:
            RuntimeError: If the tag cannot be dereferenced to a valid commit SHA.
        """
        encoded_tag = urllib.parse.quote(tag_name, safe="/")
        ref_url = f"https://api.github.com/repos/{owner}/{repo}/git/ref/tags/{encoded_tag}"
        ref_data = self.http_get_json(ref_url)
        obj = ref_data["object"]
        obj_type = obj.get("type")
        obj_sha = str(obj.get("sha", "")).strip().lower()

        hops = 0
        while obj_type == "tag" and hops < 3:
            if not _SHA40_RE.fullmatch(obj_sha):
                raise RuntimeError(f"Invalid tag object SHA for {owner}/{repo} tag {tag_name}: {obj_sha}")
            tag_url = f"https://api.github.com/repos/{owner}/{repo}/git/tags/{obj_sha}"
            tag_data = self.http_get_json(tag_url)
            obj = tag_data["object"]
            obj_type = obj.get("type")
            obj_sha = str(obj.get("sha", "")).strip().lower()
            hops += 1

        commit_sha = obj_sha
        if obj_type != "commit" or not _SHA40_RE.fullmatch(commit_sha):
            raise RuntimeError(
                f"Failed to resolve tag {tag_name} to a valid commit SHA on {owner}/{repo}: type={obj_type}, sha={commit_sha}"
            )
        return commit_sha

    def _resolve_latest_release(self, owner: str, repo: str) -> tuple[str, str, str]:
        """
        Resolve the latest stable release tag, commit SHA, and release URL for a GitHub repository.

        Args:
            owner (str): Repository owner.
            repo (str): Repository name.

        Returns:
            tuple[str, str, str]: `(tag_name, commit_sha, release_url)`.

        Raises:
            RuntimeError: If no stable release or tag can be resolved.
        """
        cache_key = f"{owner.lower()}/{repo.lower()}"
        if cache_key in self._repo_cache:
            return self._repo_cache[cache_key]

        latest_url = f"https://api.github.com/repos/{owner}/{repo}/releases/latest"
        resp = self.http_request("GET", latest_url, allow_status=(200, 404))
        tag_name: str | None = None
        release_url = f"https://github.com/{owner}/{repo}/releases"

        if resp.status_code == 200:
            payload = resp.json()
            if not payload.get("draft") and not payload.get("prerelease"):
                tag_name = payload.get("tag_name")
                release_url = payload.get("html_url") or release_url

        if not tag_name:
            tags_url = f"https://api.github.com/repos/{owner}/{repo}/tags?per_page=30"
            tags_data = self.http_get_json(tags_url)
            semver_tags: list[tuple[tuple[int, int, int], str]] = []
            for item in tags_data:
                tname = item.get("name", "")
                parsed = _parse_tag_semver(tname)
                if parsed is not None:
                    semver_tags.append((parsed, tname))
            if semver_tags:
                semver_tags.sort(key=lambda item: item[0], reverse=True)
                tag_name = semver_tags[0][1]
            elif tags_data:
                tag_name = tags_data[0]["name"]

        if not tag_name:
            raise RuntimeError(f"No release or tag found for {owner}/{repo}")

        tag_name = _validate_tag_name(tag_name)
        commit_sha = self._resolve_tag_to_commit_sha(owner, repo, tag_name)
        release_url = f"https://github.com/{owner}/{repo}/releases/tag/{tag_name}"
        resolved = (tag_name, commit_sha, release_url)
        self._repo_cache[cache_key] = resolved
        return resolved

    def update(self) -> EcosystemResult:
        """
        Scan discovered workflow and action YAML files, validate action owners, and update/pin
        `uses:` lines to the latest release commit SHA.

        Returns:
            EcosystemResult: Summary of GitHub Actions updates and any policy/API errors.
        """
        result = EcosystemResult(ecosystem=self.ecosystem)
        action_files = self._discover_action_files()

        for yaml_path in action_files:
            rel = self.rel_path(yaml_path)
            newline = detect_newline_style(yaml_path)
            with open(yaml_path, "r", encoding="utf-8", newline="") as handle:
                original_text = handle.read()

            lines = original_text.replace("\r\n", "\n").split("\n")
            new_lines: list[str] = []
            file_changed = False
            recorded_in_file: set[tuple[str, str, str]] = set()

            for line in lines:
                match = _USES_LINE_RE.match(line)
                if not match:
                    new_lines.append(line)
                    continue

                action = match.group("action")
                current_ref = match.group("ref")
                comment = match.group("comment") or ""
                owner, repo = parse_action_repo(action)

                if owner.lower() not in ALLOWED_ACTION_OWNERS:
                    result.errors.append(
                        f"{rel}: Unapproved GitHub Action owner '{owner}' in '{action}@{current_ref}'. "
                        f"Allowed owners: {', '.join(sorted(ALLOWED_ACTION_OWNERS))}."
                    )
                    new_lines.append(line)
                    continue

                try:
                    latest_tag, latest_sha, release_url = self._resolve_latest_release(owner, repo)
                except RuntimeError as exc:
                    result.errors.append(f"{rel} ({action}): {exc}")
                    new_lines.append(line)
                    continue

                quote = match.group("q")
                updated_line = f"{match.group('prefix')}{quote}{action}@{latest_sha}{quote} # {latest_tag}"
                sha_changed = current_ref.lower() != latest_sha.lower()

                if sha_changed or (not _SHA40_RE.match(current_ref)):
                    file_changed = True
                    old_comment = comment.lstrip().lstrip("#").strip()
                    old_display = (
                        f"{old_comment} ({current_ref[:7]})"
                        if _SHA40_RE.match(current_ref) and old_comment
                        else (current_ref[:7] if _SHA40_RE.match(current_ref) else current_ref)
                    )
                    new_display = f"{latest_tag} ({latest_sha[:7]})"
                    dedup_key = (action, old_display, new_display)
                    if dedup_key not in recorded_in_file:
                        recorded_in_file.add(dedup_key)
                        result.updates.append(
                            DependencyUpdate(
                                ecosystem=self.ecosystem,
                                name=action,
                                old_version=old_display,
                                new_version=new_display,
                                source_file=rel,
                                release_url=release_url,
                            )
                        )
                    new_lines.append(updated_line)
                elif comment.strip() != f"# {latest_tag}":
                    file_changed = True
                    new_lines.append(updated_line)
                else:
                    new_lines.append(line)

            if file_changed:
                if rel not in result.modified_files:
                    result.modified_files.append(rel)
                if not self.dry_run:
                    write_text_preserving_newlines(yaml_path, "\n".join(new_lines), newline)

        return result

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
.NET / NuGet dependency updater supporting Central Package Management (`Directory.Packages.props`),
inline `.csproj` package references, `.config/dotnet-tools.json`, and `.github/dependabot.yml`
runtime-ceiling synchronization.
"""

from dataclasses import dataclass
import json
from pathlib import Path
import re
import urllib.parse
import xml.etree.ElementTree as ET

from updaters.BaseUpdater import (
    BaseUpdater,
    DependencyUpdate,
    EcosystemResult,
    SkippedUpdate,
    detect_newline_style,
    write_text_preserving_newlines,
)

RUNTIME_ALIGNED_PREFIXES = (
    "microsoft.aspnetcore.",
    "microsoft.entityframeworkcore.",
    "microsoft.extensions.",
    "system.",
)
"""
NuGet package ID prefixes whose major version aligns with the .NET runtime major version.
"""

RUNTIME_ALIGNED_EXACT = frozenset({
    "microsoft.entityframeworkcore",
})
"""
Exact lowercase NuGet package IDs whose major version aligns with the .NET runtime major version.
"""

_SEMVER_RE = re.compile(
    r"^(?P<major>\d+)\.(?P<minor>\d+)(?:\.(?P<patch>\d+))?(?:\.(?P<rev>\d+))?(?:-(?P<pre>[0-9A-Za-z.-]+))?(?:\+.*)?$"
)
_PACKAGE_VERSION_TAG_RE = re.compile(r"<PackageVersion\b[^>]*>", re.DOTALL)
_PACKAGE_REF_TAG_RE = re.compile(r"<PackageReference\b[^>]*>", re.DOTALL)
_ATTR_INCLUDE_RE = re.compile(r"""\bInclude\s*=\s*(?P<q>["'])(?P<val>[^"']+)(?P=q)""")
_ATTR_VERSION_RE = re.compile(r"""(\bVersion\s*=\s*)(?P<q>["'])(?P<val>[^"']+)(?P=q)""")
_TFM_SHORT_NET_RE = re.compile(r"^net(?P<major>\d+)\.(?P<minor>\d+)(?:-(?P<os>[a-z0-9._-]+))?$", re.IGNORECASE)
_TFM_LONG_NETCOREAPP_RE = re.compile(
    r"^\.NETCoreApp(?:,Version=v)?(?P<major>\d+)\.(?P<minor>\d+)(?:-(?P<os>[a-z0-9._-]+))?$",
    re.IGNORECASE,
)
_TFM_NETSTANDARD_RE = re.compile(
    r"^(?:netstandard|\.NETStandard(?:,Version=v)?)(?P<major>\d+)\.(?P<minor>\d+)$",
    re.IGNORECASE,
)
_TFM_NETCOREAPP_OLD_RE = re.compile(r"^netcoreapp(?P<major>\d+)\.(?P<minor>\d+)$", re.IGNORECASE)
_RANGE_MIN_RE = re.compile(r"^[(\[]?\s*(?P<ver>\d+\.\d+(?:\.\d+)?(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?)")
_DEPENDABOT_NUGET_CEILING_RE = re.compile(r'(versions:\s*\n\s*-\s*["\']?>=\s*)(\d+)\.0\.0(["\']?)')


@dataclass(frozen=True)
class NuGetVersion:
    """
    Comparable representation of a NuGet / SemVer2 version string.
    """

    raw: str
    major: int
    minor: int
    patch: int
    revision: int
    prerelease: tuple[tuple[int, int | str], ...]

    @property
    def is_prerelease(self) -> bool:
        """
        Return True if the version includes a prerelease label.

        Returns:
            bool: True when prerelease identifiers are present.
        """
        return bool(self.prerelease)

    @property
    def sort_key(self) -> tuple:
        """
        Compute a SemVer2-compliant sort key where stable releases sort higher than prereleases.

        Returns:
            tuple: Sortable key tuple.
        """
        return (
            self.major,
            self.minor,
            self.patch,
            self.revision,
            1 if not self.prerelease else 0,
            self.prerelease,
        )


def parse_nuget_version(version_str: str) -> NuGetVersion | None:
    """
    Parse a NuGet version string into a comparable `NuGetVersion`.

    Args:
        version_str (str): Version string such as `10.0.15` or `8.0.0-preview.1`.

    Returns:
        NuGetVersion | None: Parsed version object, or `None` if invalid.
    """
    match = _SEMVER_RE.match(version_str.strip())
    if not match:
        return None
    pre_raw = match.group("pre")
    pre_parts: list[tuple[int, int | str]] = []
    if pre_raw:
        for token in pre_raw.split("."):
            if token.isdigit():
                pre_parts.append((0, int(token)))
            else:
                pre_parts.append((1, token.lower()))
    return NuGetVersion(
        raw=version_str.strip(),
        major=int(match.group("major")),
        minor=int(match.group("minor")),
        patch=int(match.group("patch") or 0),
        revision=int(match.group("rev") or 0),
        prerelease=tuple(pre_parts),
    )


def is_runtime_aligned_package(package_id: str) -> bool:
    """
    Determine whether a NuGet package ID follows the .NET runtime major version cadence.

    Args:
        package_id (str): NuGet package identifier.

    Returns:
        bool: True if the package major version is tied to the .NET runtime major version.
    """
    lowered = package_id.strip().lower()
    return lowered in RUNTIME_ALIGNED_EXACT or any(lowered.startswith(prefix) for prefix in RUNTIME_ALIGNED_PREFIXES)


def _tfm_compatibility_rank(tfm: str | None, runtime_tfm: tuple[int, int]) -> tuple[int, int, int] | None:
    """
    Evaluate whether a NuGet `dependencyGroups` target framework is compatible with `runtime_tfm`.

    Returns a specificity tuple (higher is more specific) if compatible, or `None` if incompatible.

    Args:
        tfm (str | None): Target framework string from NuGet registration metadata.
        runtime_tfm (tuple[int, int]): Active project `(major, minor)` TFM (for example `(10, 0)`).

    Returns:
        tuple[int, int, int] | None: Compatibility rank tuple if compatible, otherwise `None`.
    """
    if not tfm or tfm.strip().lower() in ("", "any", "agnostic"):
        return (1, 0, 0)

    cleaned = tfm.strip()
    runtime_major, runtime_minor = runtime_tfm

    for pattern in (_TFM_SHORT_NET_RE, _TFM_LONG_NETCOREAPP_RE):
        match = pattern.match(cleaned)
        if match:
            if match.group("os"):
                return None
            major = int(match.group("major"))
            minor = int(match.group("minor"))
            if (major, minor) <= (runtime_major, runtime_minor):
                return (4, major, minor)
            return None

    std_match = _TFM_NETSTANDARD_RE.match(cleaned)
    if std_match:
        major = int(std_match.group("major"))
        minor = int(std_match.group("minor"))
        if major == 1 or (major == 2 and minor <= 1):
            return (3, major, minor)
        return None

    coreapp_match = _TFM_NETCOREAPP_OLD_RE.match(cleaned)
    if coreapp_match:
        major = int(coreapp_match.group("major"))
        minor = int(coreapp_match.group("minor"))
        return (2, major, minor)

    return None


def _parse_min_dependency_version(version_range: str | None) -> NuGetVersion | None:
    """
    Extract the lower-bound version from a NuGet dependency range expression.

    Args:
        version_range (str | None): NuGet range string such as `[11.0.0, )` or `10.0.15`.

    Returns:
        NuGetVersion | None: Minimum version, or `None` if unparsed.
    """
    if not version_range:
        return None
    match = _RANGE_MIN_RE.match(version_range.strip())
    if not match:
        return None
    return parse_nuget_version(match.group("ver"))


class DotNetUpdater(BaseUpdater):
    """
    Discovers .NET Central Package Management manifests, project files, and local tool manifests,
    resolves compatible NuGet updates capped by the active .NET target framework, and verifies builds.
    """

    ecosystem = "dotnet"

    def __init__(
        self,
        repo_root: str | Path,
        dry_run: bool = False,
        verify: bool = False,
        github_token: str | None = None,
        timeout: int = 30,
    ) -> None:
        """
        Initialize the .NET updater and an in-memory NuGet registration cache.

        Args:
            repo_root (str | Path): Repository root path.
            dry_run (bool): Preview updates without writing files.
            verify (bool): Run `dotnet restore`, `dotnet build`, and `dotnet test` after updating.
            github_token (str | None): Optional GitHub token.
            timeout (int): HTTP request timeout in seconds.
        """
        super().__init__(repo_root, dry_run=dry_run, verify=verify, github_token=github_token, timeout=timeout)
        self._catalog_cache: dict[str, list[dict]] = {}

    def _fetch_catalog_entries(self, package_id: str) -> list[dict]:
        """
        Fetch all NuGet v3 registration catalog entries for a package ID, caching across calls.

        Args:
            package_id (str): NuGet package identifier.

        Returns:
            list[dict]: List of `catalogEntry` dictionaries from NuGet registration pages.

        Raises:
            RuntimeError: If a NuGet catalog page URL does not use HTTPS or originates from outside api.nuget.org.
        """
        cache_key = package_id.lower()
        if cache_key in self._catalog_cache:
            return self._catalog_cache[cache_key]

        encoded_id = urllib.parse.quote(cache_key, safe="")
        index_url = f"https://api.nuget.org/v3/registration5-gz-semver2/{encoded_id}/index.json"
        index_data = self.http_get_json(index_url)
        entries: list[dict] = []

        for page in index_data.get("items", []):
            page_items = page.get("items")
            if page_items is None and "@id" in page:
                page_url = str(page["@id"])
                parsed_page = urllib.parse.urlsplit(page_url)
                if parsed_page.scheme.lower() != "https" or (parsed_page.hostname or "").lower() != "api.nuget.org":
                    raise RuntimeError(f"Untrusted NuGet registration page URL: {page_url}")
                page_data = self.http_get_json(page_url)
                page_items = page_data.get("items", [])
            for leaf in page_items or []:
                catalog = leaf.get("catalogEntry")
                if catalog:
                    entries.append(catalog)

        self._catalog_cache[cache_key] = entries
        return entries

    def _check_candidate_compatibility(
        self,
        package_id: str,
        catalog_entry: dict,
        runtime_tfm: tuple[int, int] | None,
    ) -> str | None:
        """
        Check whether a NuGet `catalogEntry` is compatible with the repository's `.NET` runtime.

        Args:
            package_id (str): NuGet package ID.
            catalog_entry (dict): NuGet v3 registration `catalogEntry` payload.
            runtime_tfm (tuple[int, int] | None): Active `(major, minor)` target framework.

        Returns:
            str | None: `None` if compatible, or a human-readable rejection reason if incompatible.
        """
        version_str = catalog_entry.get("version", "")
        parsed = parse_nuget_version(version_str)
        if parsed is None:
            return "Unparseable version"

        if runtime_tfm is None:
            return None

        runtime_major, runtime_minor = runtime_tfm

        if is_runtime_aligned_package(package_id) and parsed.major > runtime_major:
            return f"Blocked by .NET {runtime_major} runtime ceiling (net{runtime_major}.{runtime_minor})"

        dep_groups = catalog_entry.get("dependencyGroups") or []
        if not dep_groups:
            return None

        best_rank: tuple[int, int, int] | None = None
        best_group: dict | None = None
        for group in dep_groups:
            rank = _tfm_compatibility_rank(group.get("targetFramework"), runtime_tfm)
            if rank is not None and (best_rank is None or rank > best_rank):
                best_rank = rank
                best_group = group

        if best_group is None:
            tfms = ", ".join(str(g.get("targetFramework") or "any") for g in dep_groups)
            return f"No dependency group compatible with net{runtime_major}.{runtime_minor} (targets: {tfms})"

        for dep in best_group.get("dependencies") or []:
            dep_id = dep.get("id", "")
            min_ver = _parse_min_dependency_version(dep.get("range"))
            if min_ver is not None and is_runtime_aligned_package(dep_id) and min_ver.major > runtime_major:
                return (
                    f"Depends on {dep_id} >= {min_ver.raw} which exceeds .NET {runtime_major} runtime ceiling"
                )

        return None

    def _resolve_latest_compatible(
        self,
        package_id: str,
        current_version_str: str,
        runtime_tfm: tuple[int, int] | None,
    ) -> tuple[str | None, SkippedUpdate | None, str]:
        """
        Resolve the highest compatible NuGet version for a package.

        Args:
            package_id (str): NuGet package identifier.
            current_version_str (str): Currently pinned version in the repository.
            runtime_tfm (tuple[int, int] | None): Active `(major, minor)` runtime TFM.

        Returns:
            tuple[str | None, SkippedUpdate | None, str]:
                `(new_version_or_none, highest_skipped_update_or_none, release_url)`.
        """
        current_parsed = parse_nuget_version(current_version_str)
        if current_parsed is None:
            return None, None, f"https://www.nuget.org/packages/{package_id}"

        entries = self._fetch_catalog_entries(package_id)
        candidates: list[tuple[NuGetVersion, dict]] = []
        for entry in entries:
            if entry.get("listed", True) is False:
                continue
            if entry.get("deprecation"):
                continue
            ver = parse_nuget_version(entry.get("version", ""))
            if ver is None:
                continue
            if ver.is_prerelease and not current_parsed.is_prerelease:
                continue
            if ver.sort_key > current_parsed.sort_key:
                candidates.append((ver, entry))

        candidates.sort(key=lambda item: item[0].sort_key, reverse=True)
        highest_skipped: SkippedUpdate | None = None

        for ver, entry in candidates:
            reason = self._check_candidate_compatibility(package_id, entry, runtime_tfm)
            if reason is None:
                release_url = f"https://www.nuget.org/packages/{package_id}/{ver.raw}"
                return ver.raw, highest_skipped, release_url
            if highest_skipped is None:
                highest_skipped = SkippedUpdate(
                    ecosystem=self.ecosystem,
                    name=package_id,
                    current_version=current_version_str,
                    candidate_version=ver.raw,
                    source_file="",
                    reason=reason,
                )

        return None, highest_skipped, f"https://www.nuget.org/packages/{package_id}"

    def _update_props_or_csproj(
        self,
        file_path: Path,
        tag_regex: re.Pattern[str],
        runtime_tfm: tuple[int, int] | None,
        result: EcosystemResult,
        snapshots: dict[Path, str],
    ) -> list[tuple[Path, str, str, str]]:
        """
        Scan an MSBuild file for `<PackageVersion>` or `<PackageReference>` version attributes and update them.

        Args:
            file_path (Path): Path to `Directory.Packages.props` or `*.csproj`.
            tag_regex (re.Pattern[str]): Compiled regex matching the target XML element.
            runtime_tfm (tuple[int, int] | None): Active `.NET` target framework version.
            result (EcosystemResult): Result object to populate with updates and skipped entries.
            snapshots (dict[Path, str]): Original file contents keyed by path for potential rollback.

        Returns:
            list[tuple[Path, str, str, str]]: Applied updates `(path, pkg_id, old_ver, new_ver)`.
        """
        rel = self.rel_path(file_path)
        newline = detect_newline_style(file_path)
        with open(file_path, "r", encoding="utf-8", newline="") as handle:
            original_text = handle.read()

        applied: list[tuple[Path, str, str, str]] = []

        def replace_tag(match: re.Match[str]) -> str:
            """
            Inspect a matched MSBuild element and update its `Version` attribute if a newer compatible version exists.

            Args:
                match (re.Match[str]): Regex match for the XML element.

            Returns:
                str: Original or updated XML element string.
            """
            tag_text = match.group(0)
            inc_match = _ATTR_INCLUDE_RE.search(tag_text)
            ver_match = _ATTR_VERSION_RE.search(tag_text)
            if not inc_match or not ver_match:
                return tag_text

            pkg_id = inc_match.group("val").strip()
            current_ver = ver_match.group("val").strip()
            try:
                new_ver, skipped, release_url = self._resolve_latest_compatible(pkg_id, current_ver, runtime_tfm)
            except RuntimeError as exc:
                result.errors.append(f"{rel} ({pkg_id}): {exc}")
                return tag_text

            if skipped is not None:
                skipped.source_file = rel
                result.skipped.append(skipped)

            if new_ver is None:
                return tag_text

            applied.append((file_path, pkg_id, current_ver, new_ver))
            result.updates.append(
                DependencyUpdate(
                    ecosystem=self.ecosystem,
                    name=pkg_id,
                    old_version=current_ver,
                    new_version=new_ver,
                    source_file=rel,
                    release_url=release_url,
                )
            )
            return _ATTR_VERSION_RE.sub(lambda m: f"{m.group(1)}{m.group('q')}{new_ver}{m.group('q')}", tag_text, count=1)

        updated_text = tag_regex.sub(replace_tag, original_text)
        if updated_text != original_text:
            snapshots[file_path] = original_text
            if rel not in result.modified_files:
                result.modified_files.append(rel)
            if not self.dry_run:
                write_text_preserving_newlines(file_path, updated_text, newline)
        return applied

    def _update_dotnet_tools(
        self,
        tools_path: Path,
        runtime_tfm: tuple[int, int] | None,
        result: EcosystemResult,
        snapshots: dict[Path, str],
    ) -> None:
        """
        Update `.NET` local tool versions in a discovered `dotnet-tools.json` manifest.

        Args:
            tools_path (Path): Path to `dotnet-tools.json`.
            runtime_tfm (tuple[int, int] | None): Active `.NET` target framework version.
            result (EcosystemResult): Result object to populate.
            snapshots (dict[Path, str]): Original file content map for rollback.
        """
        rel = self.rel_path(tools_path)
        newline = detect_newline_style(tools_path)
        with open(tools_path, "r", encoding="utf-8", newline="") as handle:
            original_text = handle.read()

        data = json.loads(original_text)
        tools = data.get("tools", {})
        changed = False
        for tool_id, tool_meta in tools.items():
            current_ver = tool_meta.get("version")
            if not current_ver:
                continue
            try:
                new_ver, skipped, release_url = self._resolve_latest_compatible(tool_id, current_ver, runtime_tfm)
            except RuntimeError as exc:
                result.errors.append(f"{rel} ({tool_id}): {exc}")
                continue

            if skipped is not None:
                skipped.source_file = rel
                result.skipped.append(skipped)
            if new_ver is not None:
                tool_meta["version"] = new_ver
                changed = True
                result.updates.append(
                    DependencyUpdate(
                        ecosystem=self.ecosystem,
                        name=tool_id,
                        old_version=current_ver,
                        new_version=new_ver,
                        source_file=rel,
                        release_url=release_url,
                        notes="dotnet local tool",
                    )
                )

        if changed:
            snapshots[tools_path] = original_text
            if rel not in result.modified_files:
                result.modified_files.append(rel)
            if not self.dry_run:
                formatted = json.dumps(data, indent=2) + "\n"
                write_text_preserving_newlines(tools_path, formatted, newline)

    def _sync_dependabot_ceiling(
        self,
        runtime_tfm: tuple[int, int] | None,
        result: EcosystemResult,
    ) -> None:
        """
        Keep NuGet runtime ceiling `ignore:` rules in `.github/dependabot.yml` synchronized
        with the active `<TargetFramework>` major version.

        Args:
            runtime_tfm (tuple[int, int] | None): Active `(major, minor)` runtime TFM.
            result (EcosystemResult): Result object to record modified files.
        """
        if runtime_tfm is None:
            return
        next_major = runtime_tfm[0] + 1
        for dep_file in self.discover_files("dependabot.yml", "dependabot.yaml"):
            rel = self.rel_path(dep_file)
            newline = detect_newline_style(dep_file)
            with open(dep_file, "r", encoding="utf-8", newline="") as handle:
                original_text = handle.read()

            updated_text = _DEPENDABOT_NUGET_CEILING_RE.sub(
                lambda m: f"{m.group(1)}{next_major}.0.0{m.group(3)}",
                original_text,
            )
            if updated_text != original_text:
                if rel not in result.modified_files:
                    result.modified_files.append(rel)
                if not self.dry_run:
                    write_text_preserving_newlines(dep_file, updated_text, newline)

    def _find_solution_or_project_target(self) -> Path | None:
        """
        Locate the primary `.sln`, `.slnx`, or project target for `dotnet` verification commands.

        Returns:
            Path | None: Path to the solution file, or `None` if none was discovered.
        """
        solutions = self.discover_files("*.sln", "*.slnx")
        return solutions[0] if solutions else None

    def _run_dotnet_verification(self, has_tools_manifest: bool) -> tuple[bool, list[str]]:
        """
        Execute `dotnet restore`, `dotnet build`, `dotnet test`, and optional `dotnet tool restore`.

        Args:
            has_tools_manifest (bool): Whether a `dotnet-tools.json` file exists in the repository.

        Returns:
            tuple[bool, list[str]]: `(all_passed, verification_messages)`.
        """
        messages: list[str] = []
        target = self._find_solution_or_project_target()
        target_args = [str(target)] if target else []

        if has_tools_manifest:
            tool_proc = self.run_command(["dotnet", "tool", "restore"])
            if tool_proc.returncode != 0:
                messages.append(f"`dotnet tool restore`: FAIL ({tool_proc.stderr.strip() or tool_proc.stdout.strip()})")
                return False, messages
            messages.append("`dotnet tool restore`: PASS")

        restore_proc = self.run_command(["dotnet", "restore", *target_args, "-v", "q", "-nologo"])
        if restore_proc.returncode != 0:
            messages.append(f"`dotnet restore`: FAIL ({restore_proc.stderr.strip() or restore_proc.stdout.strip()})")
            return False, messages
        messages.append("`dotnet restore`: PASS")

        build_proc = self.run_command(["dotnet", "build", *target_args, "--no-restore", "-v", "q", "-nologo"])
        if build_proc.returncode != 0:
            messages.append(f"`dotnet build`: FAIL ({build_proc.stderr.strip() or build_proc.stdout.strip()})")
            return False, messages
        messages.append("`dotnet build`: PASS")

        test_proc = self.run_command(["dotnet", "test", *target_args, "--no-build", "-v", "q", "-nologo"])
        if test_proc.returncode != 0:
            messages.append(f"`dotnet test`: FAIL ({test_proc.stderr.strip() or test_proc.stdout.strip()})")
            return False, messages
        messages.append("`dotnet test`: PASS")
        return True, messages

    def _revert_single_package(
        self,
        file_path: Path,
        pkg_id: str,
        old_ver: str,
        tag_regex: re.Pattern[str],
    ) -> None:
        """
        Revert a single package's `Version` attribute back to `old_ver` inside an MSBuild XML file.

        Args:
            file_path (Path): Target XML file path.
            pkg_id (str): Package ID to revert.
            old_ver (str): Original version string to restore.
            tag_regex (re.Pattern[str]): Compiled regex matching the XML element.
        """
        newline = detect_newline_style(file_path)
        with open(file_path, "r", encoding="utf-8", newline="") as handle:
            content = handle.read()

        def revert_tag(match: re.Match[str]) -> str:
            """
            Restore `old_ver` on the matching package element.

            Args:
                match (re.Match[str]): Regex match for the XML element.

            Returns:
                str: Restored or unchanged XML element string.
            """
            tag_text = match.group(0)
            inc_match = _ATTR_INCLUDE_RE.search(tag_text)
            if inc_match and inc_match.group("val").strip().lower() == pkg_id.lower():
                return _ATTR_VERSION_RE.sub(
                    lambda m: f"{m.group(1)}{m.group('q')}{old_ver}{m.group('q')}",
                    tag_text,
                    count=1,
                )
            return tag_text

        reverted = tag_regex.sub(revert_tag, content)
        write_text_preserving_newlines(file_path, reverted, newline)

    def _verify_with_rollback(
        self,
        applied: list[tuple[Path, str, str, str]],
        snapshots: dict[Path, str],
        has_tools_manifest: bool,
        result: EcosystemResult,
    ) -> None:
        """
        Run `.NET` build and test verification; if the combined batch fails, isolate and roll back
        the offending package update(s) while keeping all valid updates.

        Args:
            applied (list[tuple[Path, str, str, str]]): Applied MSBuild package updates.
            snapshots (dict[Path, str]): Original file texts prior to updates.
            has_tools_manifest (bool): Whether `dotnet-tools.json` exists.
            result (EcosystemResult): Result object to update on rollback.
        """
        passed, messages = self._run_dotnet_verification(has_tools_manifest)
        if passed:
            result.verification.extend(messages)
            return

        if not applied:
            result.verification.extend(messages)
            result.errors.append(".NET verification failed; reverting updates.")
            for path, original in snapshots.items():
                newline = detect_newline_style(path)
                write_text_preserving_newlines(path, original, newline)
            result.updates.clear()
            result.modified_files.clear()
            return

        # Roll back individual updates one by one until the build/tests pass, or revert all if none work.
        rolled_back: set[tuple[str, str]] = set()
        for file_path, pkg_id, old_ver, new_ver in reversed(applied):
            tag_regex = _PACKAGE_VERSION_TAG_RE if file_path.name == "Directory.Packages.props" else _PACKAGE_REF_TAG_RE
            self._revert_single_package(file_path, pkg_id, old_ver, tag_regex)
            rel = self.rel_path(file_path)
            rolled_back.add((rel, pkg_id.lower()))
            result.skipped.append(
                SkippedUpdate(
                    ecosystem=self.ecosystem,
                    name=pkg_id,
                    current_version=old_ver,
                    candidate_version=new_ver,
                    source_file=rel,
                    reason="Rolled back after failing dotnet restore/build/test verification",
                )
            )
            passed, messages = self._run_dotnet_verification(has_tools_manifest)
            if passed:
                break

        result.updates = [
            upd
            for upd in result.updates
            if (upd.source_file, upd.name.lower()) not in rolled_back
        ]
        result.modified_files = [
            rel
            for rel in result.modified_files
            if self.repo_root.joinpath(rel) not in snapshots
            or self.repo_root.joinpath(rel).read_text(encoding="utf-8") != snapshots[self.repo_root.joinpath(rel)]
        ]
        if not passed:
            for path, original in snapshots.items():
                newline = detect_newline_style(path)
                write_text_preserving_newlines(path, original, newline)
            result.updates.clear()
            result.modified_files.clear()
            result.errors.append(".NET verification failed even after rolling back package updates.")
        result.verification.extend(messages)

    def update(self) -> EcosystemResult:
        """
        Discover all `.NET` package manifests, resolve compatible updates, apply edits, and verify.

        Returns:
            EcosystemResult: Structured summary of `.NET` updates, skipped packages, and verification.
        """
        result = EcosystemResult(ecosystem=self.ecosystem)
        runtime_tfm = self.discover_dotnet_runtime()
        snapshots: dict[Path, str] = {}
        applied_msbuild: list[tuple[Path, str, str, str]] = []

        for props_file in self.discover_files("Directory.Packages.props"):
            applied_msbuild.extend(
                self._update_props_or_csproj(props_file, _PACKAGE_VERSION_TAG_RE, runtime_tfm, result, snapshots)
            )

        for csproj_file in self.discover_files("*.csproj"):
            applied_msbuild.extend(
                self._update_props_or_csproj(csproj_file, _PACKAGE_REF_TAG_RE, runtime_tfm, result, snapshots)
            )

        tools_manifests = self.discover_files("dotnet-tools.json")
        for tools_file in tools_manifests:
            self._update_dotnet_tools(tools_file, runtime_tfm, result, snapshots)

        self._sync_dependabot_ceiling(runtime_tfm, result)

        if self.verify and not self.dry_run and result.has_changes:
            self._verify_with_rollback(applied_msbuild, snapshots, bool(tools_manifests), result)

        return result


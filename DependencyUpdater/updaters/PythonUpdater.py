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
Python / `uv` dependency updater that automatically discovers all `pyproject.toml` projects,
upgrades `uv.lock` via `uv lock --upgrade`, synchronizes `>=` / `==` version bounds in
`pyproject.toml`, and verifies `uv sync --locked`.
"""

from pathlib import Path
import re
import shutil
import tempfile
import tomllib

from updaters.BaseUpdater import (
    BaseUpdater,
    DependencyUpdate,
    EcosystemResult,
    detect_newline_style,
    write_text_preserving_newlines,
)

_PEP503_NORMALIZE_RE = re.compile(r"[-_.]+")
_DEP_SPEC_STRING_RE = re.compile(
    r"""(?P<q>["'])(?P<name>[A-Za-z0-9][A-Za-z0-9._-]*)(?P<extras>\[[^\]]+\])?(?P<op>\s*(?:>=|==)\s*)(?P<ver>[0-9A-Za-z.+!_-]+)(?P<rest>[^"']*)(?P=q)"""
)


def normalize_pep503_name(name: str) -> str:
    """
    Normalize a Python distribution name according to PEP 503.

    Args:
        name (str): Raw package name.

    Returns:
        str: Lowercase hyphen-normalized distribution name.
    """
    return _PEP503_NORMALIZE_RE.sub("-", name).lower()


def _parse_uv_lock_versions(lock_path: Path) -> dict[str, str]:
    """
    Parse resolved package versions from a `uv.lock` file.

    Args:
        lock_path (Path): Path to `uv.lock`.

    Returns:
        dict[str, str]: Mapping of normalized package name to resolved version string.
    """
    if not lock_path.is_file():
        return {}
    with open(lock_path, "rb") as handle:
        data = tomllib.load(handle)
    versions: dict[str, str] = {}
    for pkg in data.get("package", []):
        name = pkg.get("name")
        version = pkg.get("version")
        if name and version:
            versions[normalize_pep503_name(name)] = str(version)
    return versions


def _collect_direct_dependency_names(pyproject_data: dict) -> set[str]:
    """
    Collect normalized names of all direct dependencies declared in a parsed `pyproject.toml`.

    Args:
        pyproject_data (dict): Parsed TOML dictionary from `pyproject.toml`.

    Returns:
        set[str]: Set of normalized direct dependency names.
    """
    raw_specs: list[str] = []
    project = pyproject_data.get("project", {})
    raw_specs.extend(s for s in project.get("dependencies", []) if isinstance(s, str))
    for group_specs in (project.get("optional-dependencies") or {}).values():
        if isinstance(group_specs, list):
            raw_specs.extend(s for s in group_specs if isinstance(s, str))
    for group_items in (pyproject_data.get("dependency-groups") or {}).values():
        if isinstance(group_items, list):
            raw_specs.extend(s for s in group_items if isinstance(s, str))

    names: set[str] = set()
    for spec in raw_specs:
        match = re.match(r"^\s*([A-Za-z0-9][A-Za-z0-9._-]*)", spec)
        if match:
            names.add(normalize_pep503_name(match.group(1)))
    return names


def update_pyproject_specifiers(
    pyproject_text: str,
    resolved_versions: dict[str, str],
) -> tuple[str, list[tuple[str, str, str]]]:
    """
    Update `>=` and `==` version bounds in `pyproject.toml` dependency strings to match `uv.lock`.

    Preserves existing formatting, comments, quotes, extras, upper bounds, and environment markers.

    Args:
        pyproject_text (str): Raw text of `pyproject.toml`.
        resolved_versions (dict[str, str]): Mapping of normalized package name to locked version.

    Returns:
        tuple[str, list[tuple[str, str, str]]]:
            `(updated_text, [(package_name, old_spec_ver, new_spec_ver), ...])`.
    """
    changes: list[tuple[str, str, str]] = []

    def replace_spec(match: re.Match[str]) -> str:
        """
        Replace the version component in a single dependency specifier string if `uv.lock` has a newer version.

        Args:
            match (re.Match[str]): Regex match for the quoted dependency string.

        Returns:
            str: Updated or original quoted dependency string.
        """
        pkg_name = match.group("name")
        norm_name = normalize_pep503_name(pkg_name)
        old_ver = match.group("ver")
        new_ver = resolved_versions.get(norm_name)
        if not new_ver or new_ver == old_ver:
            return match.group(0)
        changes.append((pkg_name, old_ver, new_ver))
        extras = match.group("extras") or ""
        return (
            f"{match.group('q')}{pkg_name}{extras}{match.group('op')}"
            f"{new_ver}{match.group('rest')}{match.group('q')}"
        )

    updated = _DEP_SPEC_STRING_RE.sub(replace_spec, pyproject_text)
    return updated, changes


class PythonUpdater(BaseUpdater):
    """
    Discovers all Python `uv` projects in the repository, upgrades `uv.lock`, updates `pyproject.toml`
    minimum bounds, and verifies lockfile consistency via `uv sync --locked`.
    """

    ecosystem = "python"

    def _run_uv_upgrade(self, project_dir: Path, has_lock: bool) -> tuple[bool, dict[str, str], str]:
        """
        Run `uv lock --upgrade` for a project directory (or an isolated temp copy during dry-run).

        Args:
            project_dir (Path): Directory containing `pyproject.toml`.
            has_lock (bool): Whether `uv.lock` already exists in `project_dir`.

        Returns:
            tuple[bool, dict[str, str], str]: `(success, resolved_versions, error_message)`.
        """
        if self.dry_run:
            with tempfile.TemporaryDirectory() as tmpdir:
                tmp_path = Path(tmpdir)
                shutil.copy2(project_dir / "pyproject.toml", tmp_path / "pyproject.toml")
                if has_lock:
                    shutil.copy2(project_dir / "uv.lock", tmp_path / "uv.lock")
                py_ver = project_dir / ".python-version"
                if py_ver.is_file():
                    shutil.copy2(py_ver, tmp_path / ".python-version")
                proc = self.run_command(["uv", "lock", "--upgrade", "--project", str(tmp_path)], cwd=tmp_path)
                if proc.returncode != 0:
                    return False, {}, proc.stderr.strip() or proc.stdout.strip()
                return True, _parse_uv_lock_versions(tmp_path / "uv.lock"), ""

        proc = self.run_command(["uv", "lock", "--upgrade", "--project", str(project_dir)])
        if proc.returncode != 0:
            return False, {}, proc.stderr.strip() or proc.stdout.strip()
        return True, _parse_uv_lock_versions(project_dir / "uv.lock"), ""

    def update(self) -> EcosystemResult:
        """
        Discover all `pyproject.toml` projects, upgrade their `uv.lock` and `pyproject.toml` versions,
        and run `uv sync --locked` when verification is enabled.

        Returns:
            EcosystemResult: Summary of Python dependency updates, verification checks, and errors.
        """
        result = EcosystemResult(ecosystem=self.ecosystem)
        pyproject_files = self.discover_files("pyproject.toml")

        for pyproject_path in pyproject_files:
            project_dir = pyproject_path.parent
            rel_pyproject = self.rel_path(pyproject_path)
            lock_path = project_dir / "uv.lock"
            rel_lock = self.rel_path(lock_path)

            with open(pyproject_path, "rb") as handle:
                pyproject_data = tomllib.load(handle)

            if "project" not in pyproject_data:
                continue

            has_lock = lock_path.is_file()
            direct_names = _collect_direct_dependency_names(pyproject_data)
            if not direct_names and not has_lock:
                continue

            old_lock_versions = _parse_uv_lock_versions(lock_path)
            ok, new_lock_versions, err_msg = self._run_uv_upgrade(project_dir, has_lock)
            if not ok:
                result.errors.append(f"{rel_pyproject}: `uv lock --upgrade` failed: {err_msg}")
                continue

            newline = detect_newline_style(pyproject_path)
            with open(pyproject_path, "r", encoding="utf-8", newline="") as handle:
                original_pyproject_text = handle.read()

            updated_pyproject_text, spec_changes = update_pyproject_specifiers(
                original_pyproject_text,
                new_lock_versions,
            )

            pyproject_changed = updated_pyproject_text != original_pyproject_text
            if pyproject_changed:
                if rel_pyproject not in result.modified_files:
                    result.modified_files.append(rel_pyproject)
                if not self.dry_run:
                    write_text_preserving_newlines(pyproject_path, updated_pyproject_text, newline)
                    relock = self.run_command(["uv", "lock", "--project", str(project_dir)])
                    if relock.returncode != 0:
                        result.errors.append(
                            f"{rel_pyproject}: `uv lock` after pyproject update failed: "
                            f"{relock.stderr.strip() or relock.stdout.strip()}"
                        )

            updated_direct_names = {normalize_pep503_name(name) for name, _, _ in spec_changes}
            for pkg_name, old_spec_ver, new_spec_ver in spec_changes:
                old_locked = old_lock_versions.get(normalize_pep503_name(pkg_name), old_spec_ver)
                old_display = old_locked if old_locked != new_spec_ver else f">={old_spec_ver}"
                new_display = new_spec_ver if old_locked != new_spec_ver else f">={new_spec_ver}"
                result.updates.append(
                    DependencyUpdate(
                        ecosystem=self.ecosystem,
                        name=pkg_name,
                        old_version=old_display,
                        new_version=new_display,
                        source_file=rel_pyproject,
                        release_url=f"https://pypi.org/project/{pkg_name}/{new_spec_ver}/",
                        notes="direct + pyproject.toml",
                    )
                )

            lock_changed = old_lock_versions != new_lock_versions
            if lock_changed and rel_lock not in result.modified_files:
                result.modified_files.append(rel_lock)

            for norm_name, new_ver in sorted(new_lock_versions.items()):
                old_ver = old_lock_versions.get(norm_name)
                if old_ver and old_ver != new_ver and norm_name not in updated_direct_names:
                    note = "direct (uv.lock)" if norm_name in direct_names else "transitive (uv.lock)"
                    result.updates.append(
                        DependencyUpdate(
                            ecosystem=self.ecosystem,
                            name=norm_name,
                            old_version=old_ver,
                            new_version=new_ver,
                            source_file=rel_lock,
                            release_url=f"https://pypi.org/project/{norm_name}/{new_ver}/",
                            notes=note,
                        )
                    )

            if self.verify and not self.dry_run and (pyproject_changed or lock_changed):
                sync_proc = self.run_command(
                    ["uv", "sync", "--locked", "--all-extras", "--dev", "--project", str(project_dir)]
                )
                if sync_proc.returncode == 0:
                    result.verification.append(f"`uv sync --locked --project {self.rel_path(project_dir)}`: PASS")
                else:
                    msg = sync_proc.stderr.strip() or sync_proc.stdout.strip()
                    result.verification.append(
                        f"`uv sync --locked --project {self.rel_path(project_dir)}`: FAIL ({msg})"
                    )
                    result.errors.append(f"{rel_pyproject}: `uv sync --locked` failed: {msg}")

        return result

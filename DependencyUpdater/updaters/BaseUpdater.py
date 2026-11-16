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
Base abstractions, repository discovery utilities, HTTP helpers, and report formatters
shared across all per-ecosystem dependency updaters.
"""

from abc import ABC, abstractmethod
from dataclasses import asdict, dataclass, field
import fnmatch
import os
from pathlib import Path
import re
import subprocess
import time
import urllib.parse
import xml.etree.ElementTree as ET
import requests

ALLOWED_ACTION_OWNERS = frozenset({"actions", "github", "microsoft", "docker", "astral-sh"})
"""
GitHub organizations permitted for `uses:` workflow references.
"""

EXCLUDED_DIR_NAMES = frozenset({
    ".git",
    ".vs",
    ".venv",
    "venv",
    "__pycache__",
    "bin",
    "obj",
    "node_modules",
    ".agents",
    ".output",
    ".cache",
    ".diff",
})
"""
Directory names skipped during automatic repository project and file discovery.
"""

_NET_TFM_RE = re.compile(r"^net(?P<major>\d+)\.(?P<minor>\d+)(?:-.*)?$", re.IGNORECASE)
"""
Matches modern .NET target framework monikers such as `net10.0` or `net10.0-windows`.
"""

_STRIPPED_ENV_VARS: tuple[str, ...] = (
    "GITHUB_TOKEN",
    "GH_TOKEN",
    "INPUT_GITHUB-TOKEN",
    "ACTIONS_RUNTIME_TOKEN",
    "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
    "GIT_PUSH_TOKEN",
)
"""
Environment variables containing sensitive authentication tokens stripped from child processes.
"""


@dataclass
class DependencyUpdate:
    """
    Represents a single applied (or dry-run proposed) dependency update.
    """

    ecosystem: str
    name: str
    old_version: str
    new_version: str
    source_file: str
    release_url: str | None = None
    notes: str | None = None


@dataclass
class SkippedUpdate:
    """
    Represents a candidate dependency update that was intentionally skipped or rolled back.
    """

    ecosystem: str
    name: str
    current_version: str
    candidate_version: str
    source_file: str
    reason: str


@dataclass
class EcosystemResult:
    """
    Aggregates all changes, skipped candidates, verification steps, and errors for one ecosystem.
    """

    ecosystem: str
    updates: list[DependencyUpdate] = field(default_factory=list)
    skipped: list[SkippedUpdate] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    modified_files: list[str] = field(default_factory=list)
    verification: list[str] = field(default_factory=list)

    @property
    def has_changes(self) -> bool:
        """
        Return True if any files were modified or updates were proposed.

        Returns:
            bool: True when updates or modified files are non-empty.
        """
        return bool(self.updates or self.modified_files)


class BaseUpdater(ABC):
    """
    Abstract base class for ecosystem-specific dependency updaters.
    """

    ecosystem: str = "base"
    user_agent: str = "streamactions.dependency.updater/1"

    def __init__(
        self,
        repo_root: str | Path,
        dry_run: bool = False,
        verify: bool = False,
        github_token: str | None = None,
        timeout: int = 30,
    ) -> None:
        """
        Initialize the updater for a repository root.

        Args:
            repo_root (str | Path): Path to the repository root directory.
            dry_run (bool): When True, compute updates without writing files in the workspace.
            verify (bool): When True, run ecosystem verification commands after applying updates.
            github_token (str | None): Optional GitHub token for authenticated GitHub API requests.
            timeout (int): HTTP request timeout in seconds.
        """
        self.repo_root = Path(repo_root).resolve()
        self.dry_run = dry_run
        self.verify = verify
        self.github_token = github_token or os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
        self.timeout = timeout
        self.session = requests.Session()
        self.session.headers.update({"User-Agent": self.user_agent})

    def rel_path(self, path: Path) -> str:
        """
        Convert an absolute path inside the repository to a POSIX-style relative path.

        Args:
            path (Path): Target path inside `repo_root`.

        Returns:
            str: Forward-slash relative path from `repo_root`.
        """
        return path.resolve().relative_to(self.repo_root).as_posix()

    def discover_files(self, *patterns: str) -> list[Path]:
        """
        Automatically discover files in the repository matching any of the supplied filename globs.

        Args:
            *patterns (str): Filename glob patterns (for example `Directory.Packages.props` or `*.csproj`).

        Returns:
            list[Path]: Sorted list of matching file paths, excluding build and cache directories.
        """
        matches: list[Path] = []
        for root, dirs, files in os.walk(self.repo_root):
            dirs[:] = sorted(d for d in dirs if d not in EXCLUDED_DIR_NAMES)
            root_path = Path(root)
            for filename in sorted(files):
                if any(fnmatch.fnmatch(filename, pat) for pat in patterns):
                    matches.append(root_path / filename)
        return sorted(matches, key=lambda p: self.rel_path(p))

    def discover_dotnet_runtime(self) -> tuple[int, int] | None:
        """
        Discover the active target .NET runtime version from `Directory.Build.props` and `*.csproj` files.

        Reads `<TargetFramework>` and `<TargetFrameworks>` elements across discovered MSBuild files
        and returns the lowest `net<major>.<minor>` target framework version so all packages and
        images remain compatible across every project in the repository.

        Returns:
            tuple[int, int] | None: `(major, minor)` tuple (for example `(10, 0)`), or `None` if not found.
        """
        tfms: list[tuple[int, int]] = []
        candidate_files = self.discover_files("Directory.Build.props", "*.csproj")
        for file_path in candidate_files:
            try:
                tree = ET.parse(file_path)
            except ET.ParseError:
                continue
            root = tree.getroot()
            for tag_name in ("TargetFramework", "TargetFrameworks"):
                for elem in root.iter():
                    local_name = elem.tag.rsplit("}", 1)[-1]
                    if local_name != tag_name or not elem.text:
                        continue
                    for raw_tfm in elem.text.split(";"):
                        match = _NET_TFM_RE.match(raw_tfm.strip())
                        if match:
                            tfms.append((int(match.group("major")), int(match.group("minor"))))
        return min(tfms) if tfms else None

    def http_request(
        self,
        method: str,
        url: str,
        headers: dict[str, str] | None = None,
        allow_status: tuple[int, ...] = (200,),
        max_retries: int = 3,
    ) -> requests.Response:
        """
        Execute an HTTP request with retry/backoff handling for transient rate-limit and server errors.

        Args:
            method (str): HTTP method (`GET`, `HEAD`, etc.).
            url (str): Target URL.
            headers (dict[str, str] | None): Optional extra request headers.
            allow_status (tuple[int, ...]): Status codes considered valid without raising.
            max_retries (int): Maximum retry attempts for transient failures.

        Returns:
            requests.Response: Completed HTTP response object.

        Raises:
            RuntimeError: If the URL is non-HTTPS, or if the final response status is not in `allow_status`.
        """
        parsed = urllib.parse.urlsplit(url)
        if parsed.scheme.lower() != "https" or not parsed.hostname:
            raise RuntimeError(f"Refusing non-HTTPS URL: {url}")

        req_headers = dict(headers) if headers else {}
        if self.github_token and self.ecosystem == "actions" and (parsed.hostname or "").lower() == "api.github.com":
            req_headers.setdefault("Authorization", f"Bearer {self.github_token}")
            req_headers.setdefault("Accept", "application/vnd.github+json")
            req_headers.setdefault("X-GitHub-Api-Version", "2022-11-28")

        last_exc: Exception | None = None
        for attempt in range(max_retries):
            try:
                resp = self.session.request(method, url, headers=req_headers, timeout=self.timeout)
                if resp.status_code in allow_status:
                    return resp
                if resp.status_code in (429, 500, 502, 503, 504) and attempt < max_retries - 1:
                    time.sleep(2**attempt)
                    continue
                raise RuntimeError(f"HTTP {resp.status_code} for {method} {url}: {resp.text[:300]}")
            except requests.RequestException as exc:
                last_exc = exc
                if attempt < max_retries - 1:
                    time.sleep(2**attempt)
                    continue
                raise RuntimeError(f"HTTP request failed for {method} {url}: {exc}") from exc

        raise RuntimeError(f"HTTP request failed for {method} {url}: {last_exc}")

    def http_get_json(self, url: str, headers: dict[str, str] | None = None) -> dict | list:
        """
        Fetch and decode a JSON payload from the specified URL.

        Args:
            url (str): Target URL.
            headers (dict[str, str] | None): Optional extra request headers.

        Returns:
            dict | list: Parsed JSON payload.
        """
        resp = self.http_request("GET", url, headers=headers)
        return resp.json()

    def run_command(
        self,
        args: list[str],
        cwd: Path | None = None,
    ) -> subprocess.CompletedProcess[str]:
        """
        Execute a subprocess command and capture stdout and stderr as UTF-8 text,
        stripping sensitive tokens from the child environment.

        Args:
            args (list[str]): Command and argument list.
            cwd (Path | None): Working directory; defaults to `repo_root`.

        Returns:
            subprocess.CompletedProcess[str]: Completed process result.
        """
        child_env = os.environ.copy()
        for var in _STRIPPED_ENV_VARS:
            child_env.pop(var, None)

        return subprocess.run(
            args,
            cwd=str(cwd or self.repo_root),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
            env=child_env,
        )

    @abstractmethod
    def update(self) -> EcosystemResult:
        """
        Discover dependencies for this ecosystem, resolve updates, and apply or preview them.

        Returns:
            EcosystemResult: Structured summary of applied updates, skipped candidates, and errors.
        """


def _read_newline_style(text: str) -> str:
    """
    Detect the dominant newline sequence in a text file so edits preserve existing line endings.

    Args:
        text (str): File content to inspect.

    Returns:
        str: `\\r\\n` if CRLF is present, otherwise `\\n`.
    """
    return "\r\n" if "\r\n" in text else "\n"


def write_text_preserving_newlines(path: Path, content: str, newline: str) -> None:
    """
    Write text to disk using the exact newline convention originally found in the file.

    Args:
        path (Path): Destination file path.
        content (str): Text content normalized to `\\n` or target newlines.
        newline (str): Target newline string (`\\r\\n` or `\\n`).
    """
    normalized = content.replace("\r\n", "\n").replace("\n", newline)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(normalized)


def detect_newline_style(path: Path) -> str:
    """
    Read a file and return its existing newline convention (`\\r\\n` or `\\n`).

    Args:
        path (Path): File path to inspect.

    Returns:
        str: `\\r\\n` or `\\n`.
    """
    with open(path, "r", encoding="utf-8", newline="") as handle:
        return _read_newline_style(handle.read())


def results_to_dict(results: list[EcosystemResult], runtime_tfm: tuple[int, int] | None = None) -> dict:
    """
    Convert a list of `EcosystemResult` instances into a JSON-serializable dictionary.

    Args:
        results (list[EcosystemResult]): Updater results across ecosystems.
        runtime_tfm (tuple[int, int] | None): Discovered .NET target framework version, if any.

    Returns:
        dict: Serialized summary payload.
    """
    return {
        "dotnet_runtime": f"net{runtime_tfm[0]}.{runtime_tfm[1]}" if runtime_tfm else None,
        "has_changes": any(r.has_changes for r in results),
        "ecosystems": [asdict(r) for r in results],
    }


def _escape_md_cell(value: str) -> str:
    """
    Escape pipe characters and newlines for safe rendering inside a Markdown table cell.

    Args:
        value (str): Cell text.

    Returns:
        str: Escaped Markdown cell string.
    """
    return value.replace("|", "\\|").replace("\r", " ").replace("\n", " ")


_MD_URL_TRANSLATION: dict[int, str] = str.maketrans({
    "(": "%28",
    ")": "%29",
    "|": "%7C",
    "<": "%3C",
    ">": "%3E",
    "[": "%5B",
    "]": "%5D",
    '"': "%22",
    "'": "%27",
    "`": "%60",
})
"""
Translation mapping for characters that could break out of Markdown link syntax or table cells.
"""


def _sanitize_md_url(url: str | None) -> str | None:
    """
    Sanitize and validate a URL for safe embedding in Markdown link targets and table cells.

    Args:
        url (str | None): Candidate URL string, or None.

    Returns:
        str | None: Sanitized HTTPS URL with Markdown breakout characters escaped,
            or None if invalid, non-HTTPS, or containing control/whitespace characters.
    """
    if not url:
        return None
    stripped = url.strip()
    if not stripped:
        return None
    if any(c.isspace() or ord(c) < 0x20 or ord(c) == 0x7F for c in stripped):
        return None
    try:
        parsed = urllib.parse.urlsplit(stripped)
    except Exception:
        return None
    if parsed.scheme.lower() != "https" or not parsed.netloc:
        return None
    return stripped.translate(_MD_URL_TRANSLATION)


def format_markdown_report(
    results: list[EcosystemResult],
    runtime_tfm: tuple[int, int] | None = None,
) -> str:
    """
    Format one or more ecosystem update results into a Pull Request Markdown body.

    Args:
        results (list[EcosystemResult]): Results to include in the Markdown body.
        runtime_tfm (tuple[int, int] | None): Discovered .NET runtime TFM, if applicable.

    Returns:
        str: Formatted Markdown report suitable for a GitHub Pull Request description.
    """
    lines: list[str] = ["## Dependency Updates", ""]
    if runtime_tfm is not None:
        lines.append(f"- **Active .NET Runtime Target:** `net{runtime_tfm[0]}.{runtime_tfm[1]}` (runtime-aligned packages capped at major `{runtime_tfm[0]}`)")
        lines.append("")

    ecosystem_titles = {
        "dotnet": ".NET / NuGet",
        "python": "Python / uv",
        "docker": "Docker",
        "actions": "GitHub Actions",
    }

    any_content = False
    for result in results:
        if not (result.updates or result.skipped or result.errors or result.verification):
            continue
        any_content = True
        title = ecosystem_titles.get(result.ecosystem, result.ecosystem)
        lines.append(f"### {title}")
        lines.append("")

        if result.updates:
            lines.append("| Dependency | From | To | File | Notes |")
            lines.append("| :--- | :--- | :--- | :--- | :--- |")
            for upd in result.updates:
                safe_url = _sanitize_md_url(upd.release_url)
                name_cell = f"[{_escape_md_cell(upd.name)}]({safe_url})" if safe_url else f"`{_escape_md_cell(upd.name)}`"
                notes_cell = _escape_md_cell(upd.notes) if upd.notes else ""
                lines.append(
                    f"| {name_cell} | `{_escape_md_cell(upd.old_version)}` | `{_escape_md_cell(upd.new_version)}` | `{_escape_md_cell(upd.source_file)}` | {notes_cell} |"
                )
            lines.append("")
        else:
            lines.append("_No dependency updates applied for this ecosystem._")
            lines.append("")

        if result.skipped:
            lines.append("<details>")
            lines.append(f"<summary>Skipped or held-back updates ({len(result.skipped)})</summary>")
            lines.append("")
            lines.append("| Dependency | Current | Candidate | File | Reason |")
            lines.append("| :--- | :--- | :--- | :--- | :--- |")
            for skip in result.skipped:
                lines.append(
                    f"| `{_escape_md_cell(skip.name)}` | `{_escape_md_cell(skip.current_version)}` | `{_escape_md_cell(skip.candidate_version)}` | `{_escape_md_cell(skip.source_file)}` | {_escape_md_cell(skip.reason)} |"
                )
            lines.append("")
            lines.append("</details>")
            lines.append("")

        if result.verification:
            lines.append("**Verification:**")
            for check in result.verification:
                lines.append(f"- {check}")
            lines.append("")

        if result.errors:
            lines.append("**Errors:**")
            for err in result.errors:
                lines.append(f"- {err}")
            lines.append("")

    if not any_content:
        lines.append("_No dependency updates available._")
        lines.append("")

    lines.append("---")
    lines.append("_Automated by `DependencyUpdater` via GitHub Actions. Security advisories continue to be handled by Dependabot._")
    return "\n".join(lines) + "\n"


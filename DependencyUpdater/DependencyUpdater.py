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
Main CLI orchestrator for per-ecosystem dependency updates across .NET, Python (uv),
Docker, and GitHub Actions.
"""

import argparse
import json
from pathlib import Path
import sys

from updaters import (
    DockerUpdater,
    DotNetUpdater,
    EcosystemResult,
    GitHubActionsUpdater,
    PythonUpdater,
    format_markdown_report,
    results_to_dict,
)

UPDATER_CLASSES = {
    "dotnet": DotNetUpdater,
    "python": PythonUpdater,
    "docker": DockerUpdater,
    "actions": GitHubActionsUpdater,
}
"""
Registry mapping ecosystem CLI identifiers to their updater implementations.
"""


def parse_ecosystems_arg(value: str) -> list[str]:
    """
    Parse a comma-separated `--ecosystems` argument into an ordered list of ecosystem keys.

    Args:
        value (str): `all` or a comma-separated list of keys from `UPDATER_CLASSES`.

    Returns:
        list[str]: Ordered list of valid ecosystem keys.

    Raises:
        ValueError: If an unknown ecosystem key is provided.
    """
    cleaned = value.strip().lower()
    if cleaned == "all":
        return list(UPDATER_CLASSES.keys())
    selected: list[str] = []
    for raw in cleaned.split(","):
        key = raw.strip()
        if not key:
            continue
        if key not in UPDATER_CLASSES:
            raise ValueError(f"Unknown ecosystem '{key}'. Valid choices: all, {', '.join(UPDATER_CLASSES.keys())}")
        if key not in selected:
            selected.append(key)
    if not selected:
        raise ValueError("No ecosystems specified.")
    return selected


def run_updates(
    repo_root: str | Path,
    ecosystems: list[str],
    dry_run: bool = False,
    verify: bool = False,
) -> tuple[list[EcosystemResult], tuple[int, int] | None]:
    """
    Execute the requested per-ecosystem updaters against `repo_root`.

    Args:
        repo_root (str | Path): Repository root path.
        ecosystems (list[str]): Ordered list of ecosystem keys to run.
        dry_run (bool): When True, compute updates without modifying files.
        verify (bool): When True, run ecosystem verification commands after updates.

    Returns:
        tuple[list[EcosystemResult], tuple[int, int] | None]:
            `(results, discovered_dotnet_runtime_tfm)`.
    """
    results: list[EcosystemResult] = []
    runtime_tfm: tuple[int, int] | None = None

    for key in ecosystems:
        updater_cls = UPDATER_CLASSES[key]
        updater = updater_cls(repo_root=repo_root, dry_run=dry_run, verify=verify)
        if runtime_tfm is None:
            runtime_tfm = updater.discover_dotnet_runtime()
        results.append(updater.update())

    return results, runtime_tfm


def _write_optional_output(path_str: str | None, content: str) -> None:
    """
    Write text output to `path_str` (creating parent directories if needed).

    Args:
        path_str (str | None): Destination path, or `None` to skip writing.
        content (str): UTF-8 text content to write.
    """
    if not path_str:
        return
    out_path = Path(path_str)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as handle:
        handle.write(content)


def main(argv: list[str] | None = None) -> int:
    """
    Parse CLI arguments, execute dependency updaters, and write JSON/Markdown reports.

    Args:
        argv (list[str] | None): Optional CLI argument vector.

    Returns:
        int: Process exit code (`0` on success, `1` on invalid input or updater error when `--fail-on-error` is set).
    """
    parser = argparse.ArgumentParser(
        description="Update StreamActions dependencies across .NET, Python (uv), Docker, and GitHub Actions."
    )
    parser.add_argument(
        "--repo-root",
        default=".",
        help="Path to the repository root (default: current directory).",
    )
    parser.add_argument(
        "--ecosystems",
        default="all",
        help="Ecosystems to update: 'all' or comma-separated subset of dotnet,python,docker,actions.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Resolve and report updates without modifying repository files.",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="Run verification commands (dotnet restore/build/test, uv sync --locked) after updates.",
    )
    parser.add_argument(
        "--out-json",
        default=None,
        help="Write structured JSON update summary to this file path.",
    )
    parser.add_argument(
        "--out-markdown",
        default=None,
        help="Write formatted Markdown PR summary to this file path.",
    )
    parser.add_argument(
        "--fail-on-error",
        action="store_true",
        help="Exit with status 1 if any ecosystem updater reports errors.",
    )
    args = parser.parse_args(argv)

    try:
        ecosystems = parse_ecosystems_arg(args.ecosystems)
    except ValueError as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    results, runtime_tfm = run_updates(
        repo_root=args.repo_root,
        ecosystems=ecosystems,
        dry_run=args.dry_run,
        verify=args.verify,
    )

    json_payload = results_to_dict(results, runtime_tfm=runtime_tfm)
    markdown_report = format_markdown_report(results, runtime_tfm=runtime_tfm)

    _write_optional_output(args.out_json, json.dumps(json_payload, indent=2) + "\n")
    _write_optional_output(args.out_markdown, markdown_report)

    if not args.out_markdown and not args.out_json:
        print(markdown_report)

    has_errors = any(r.errors for r in results)
    if args.fail_on_error and has_errors:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())


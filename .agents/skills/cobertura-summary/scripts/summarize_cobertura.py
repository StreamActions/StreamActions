# /// script
# requires-python = ">=3.13"
# dependencies = []
# ///
"""Summarize Cobertura coverage reports into a compact Markdown gap report.

Agents run this script instead of reading raw ``*.cobertura.xml`` files. It merges every
report under a results directory (deduplicating classes and lines that several test
projects instrument), drops generated files and auto-property noise, and prints a
worst-first list of classes and methods with uncovered lines or partially covered branches.

Exit codes: 0 when a report is printed, 2 when no readable reports are found.
"""

from __future__ import annotations

import argparse
import glob
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

_CONDITION = re.compile(r"\((\d+)/(\d+)\)")
_GENERATED_MEMBER = re.compile(r"^<([^>]*)>([a-z])__([^|]*)")
_GENERATED_FILE_SUFFIXES = (".g.cs", ".g.i.cs", ".designer.cs", ".generated.cs", "assemblyinfo.cs")
_ACCESSOR_PREFIXES = ("get_", "set_")
_SYNTHESIZED_MEMBERS = frozenset({
    "<Clone>$", "PrintMembers", "get_EqualityContract", "Deconstruct",
    "Equals", "GetHashCode", "ToString", "op_Equality", "op_Inequality",
})
_STALE_SPREAD_SECONDS = 1800


def _percent(covered: int, total: int) -> str:
    """Format a covered/total ratio as a percentage, or ``n/a`` when nothing is measurable."""
    return "n/a" if total == 0 else f"{covered / total * 100:.1f}%"


@dataclass
class LineStat:
    """Merged coverage of one source line across all reports."""

    hits: int = 0
    branches_covered: int = 0
    branches_total: int = 0

    @property
    def is_uncovered(self) -> bool:
        """Return whether the line was never executed."""
        return self.hits == 0

    @property
    def is_partial(self) -> bool:
        """Return whether the line executed but missed at least one branch."""
        return self.hits > 0 and self.branches_covered < self.branches_total


@dataclass
class MethodStat:
    """Lines owned by one source-level method, with state machines and lambdas folded in."""

    name: str
    raw_name: str
    lines: set[int] = field(default_factory=set)


@dataclass
class ClassStat:
    """Lines and methods owned by one source-level class in one file."""

    name: str
    filename: str
    assembly: str
    lines: set[int] = field(default_factory=set)
    methods: dict[str, MethodStat] = field(default_factory=dict)


@dataclass
class Totals:
    """Line and branch counters for a set of merged lines."""

    lines_covered: int = 0
    lines_valid: int = 0
    branches_covered: int = 0
    branches_valid: int = 0

    def add(self, stat: LineStat) -> None:
        """Accumulate one merged line."""
        self.lines_valid += 1
        self.lines_covered += 0 if stat.is_uncovered else 1
        self.branches_valid += stat.branches_total
        self.branches_covered += stat.branches_covered

    @property
    def line_rate(self) -> float:
        """Return the line rate (1.0 when no lines are measurable)."""
        return 1.0 if self.lines_valid == 0 else self.lines_covered / self.lines_valid

    @property
    def branch_rate(self) -> float:
        """Return the branch rate (1.0 when no branches are measurable)."""
        return 1.0 if self.branches_valid == 0 else self.branches_covered / self.branches_valid

    @property
    def has_gaps(self) -> bool:
        """Return whether any measurable line or branch is uncovered."""
        return self.lines_valid > 0 and (
            self.lines_covered < self.lines_valid or self.branches_covered < self.branches_valid
        )

    @property
    def priority(self) -> tuple[float, float]:
        """Return the sort key: branch gaps first, then line gaps (lower is worse)."""
        effective = self.branch_rate if self.branches_valid else self.line_rate
        return (effective, self.line_rate)

    def describe(self, counts: bool = False) -> str:
        """Render line and branch percentages, optionally with raw counts."""
        line = _percent(self.lines_covered, self.lines_valid)
        branch = _percent(self.branches_covered, self.branches_valid)
        if counts:
            line += f" ({self.lines_covered}/{self.lines_valid})"
            branch += f" ({self.branches_covered}/{self.branches_valid})"
        return f"line {line} | branch {branch}"


@dataclass
class Coverage:
    """Merged state of every parsed report."""

    lines: dict[str, dict[int, LineStat]] = field(default_factory=dict)
    classes: dict[tuple[str, str], ClassStat] = field(default_factory=dict)
    excluded_files: set[str] = field(default_factory=set)
    noise_lines: int = 0


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(description="Summarize Cobertura coverage gaps as compact Markdown.")
    parser.add_argument("path", nargs="?", default="CollectedTestResults",
                        help="results directory (searched recursively) or a single report")
    parser.add_argument("--line-threshold", type=float, help="minimum overall line coverage percent")
    parser.add_argument("--branch-threshold", type=float, help="minimum overall branch coverage percent")
    parser.add_argument("--include", action="append", default=[],
                        help="only files whose repo-relative path contains this text (repeatable)")
    parser.add_argument("--top", type=int, default=30, help="maximum classes listed")
    parser.add_argument("--max-methods", type=int, default=6, help="maximum methods listed per class")
    parser.add_argument("--max-lines", type=int, default=140, help="maximum output lines")
    parser.add_argument("--root", default=".", help="repository root used to shorten paths")
    return parser.parse_args(argv)


def find_reports(target: Path) -> list[Path]:
    """Return every ``*coverage.cobertura.xml`` file under ``target`` (or ``target`` itself)."""
    if target.is_file():
        return [target]
    pattern = str(Path(glob.escape(str(target))) / "**" / "*coverage.cobertura.xml")
    return sorted(Path(p) for p in glob.glob(pattern, recursive=True))


def normalize_path(filename: str, sources: list[str], root: Path) -> str:
    """Resolve a Cobertura ``filename`` to a repo-relative POSIX path."""
    if "://" in filename:
        parts = filename.split("/")
        return "/".join(parts[6:]) if len(parts) > 6 else filename
    path = Path(filename)
    if not path.is_absolute() and sources:
        candidates = [Path(s) / filename for s in sources]
        path = next((c for c in candidates if c.exists()), candidates[0])
    try:
        return path.resolve().relative_to(root).as_posix()
    except (ValueError, OSError):
        return path.as_posix()


def is_generated_file(filename: str) -> bool:
    """Return whether a file is compiler or source-generator output."""
    lowered = filename.lower()
    return lowered.endswith(_GENERATED_FILE_SUFFIXES) or "/obj/" in f"/{lowered}"


def base_class_name(raw_class: str) -> str:
    """Strip compiler-generated nested segments (``Foo/<Bar>d__3`` becomes ``Foo``)."""
    kept: list[str] = []
    for segment in raw_class.split("/"):
        if "<" in segment:
            break
        kept.append(segment)
    return ".".join(kept) if kept else raw_class


def friendly_method_name(raw_class: str, raw_method: str) -> str:
    """Map state-machine, lambda, and local-function members back to their source method."""
    if raw_method == "MoveNext":
        for segment in reversed(raw_class.split("/")):
            match = _GENERATED_MEMBER.match(segment)
            if match:
                owner = match.group(1).lstrip("<")
                if owner:
                    return owner if match.group(2) == "d" else f"{owner} (lambda)"
    match = _GENERATED_MEMBER.match(raw_method)
    if match:
        owner, kind, rest = match.group(1).lstrip("<"), match.group(2), match.group(3)
        if kind == "g" and rest:
            return f"{owner}.{rest}" if owner else rest
        return f"{owner} (lambda)" if owner else "(lambda)"
    if raw_method.startswith("<") and raw_method.endswith(">$"):
        return raw_method[1:-2]
    return raw_method


def merge_line(file_lines: dict[int, LineStat], element: ET.Element) -> int:
    """Merge one ``<line>`` element (max hits, max branch counts) and return its number."""
    number = int(element.get("number", "0"))
    stat = file_lines.setdefault(number, LineStat())
    stat.hits = max(stat.hits, int(element.get("hits", "0")))
    if element.get("branch", "false").lower() == "true":
        match = _CONDITION.search(element.get("condition-coverage", ""))
        if match:
            stat.branches_covered = max(stat.branches_covered, int(match.group(1)))
            stat.branches_total = max(stat.branches_total, int(match.group(2)))
    return number


def parse_report(report: Path, root: Path, includes: list[str], coverage: Coverage) -> None:
    """Merge one Cobertura report into ``coverage``."""
    document = ET.parse(report).getroot()
    sources = [s.text.strip() for s in document.iter("source") if s.text and s.text.strip()]
    for package in document.iter("package"):
        assembly = package.get("name", "?")
        if assembly.endswith(".Tests"):
            continue
        for element in package.iter("class"):
            raw_class = element.get("name", "?")
            raw_filename = element.get("filename", "")
            if not raw_filename or raw_class.startswith("<"):
                continue
            filename = normalize_path(raw_filename, sources, root)
            if is_generated_file(filename):
                coverage.excluded_files.add(filename)
                continue
            if includes and not any(i in filename.lower() for i in includes):
                continue
            name = base_class_name(raw_class)
            stat = coverage.classes.setdefault((filename, name), ClassStat(name, filename, assembly))
            file_lines = coverage.lines.setdefault(filename, {})
            for line in element.findall("./lines/line"):
                stat.lines.add(merge_line(file_lines, line))
            for method in element.findall("./methods/method"):
                raw_method = method.get("name", "?")
                friendly = friendly_method_name(raw_class, raw_method)
                method_stat = stat.methods.setdefault(friendly, MethodStat(friendly, raw_method))
                for line in method.findall("./lines/line"):
                    number = merge_line(file_lines, line)
                    method_stat.lines.add(number)
                    stat.lines.add(number)


def _is_noise(method: MethodStat, file_lines: dict[int, LineStat]) -> bool:
    """Return whether a method is a branchless single-line accessor or synthesized record member."""
    if not (method.raw_name.startswith(_ACCESSOR_PREFIXES) or method.raw_name in _SYNTHESIZED_MEMBERS):
        return False
    if len(method.lines) > 1:
        return False
    return all(file_lines.get(n, LineStat()).branches_total == 0 for n in method.lines)


def remove_noise(coverage: Coverage) -> None:
    """Drop lines that only auto-properties or synthesized members reference."""
    for stat in coverage.classes.values():
        file_lines = coverage.lines.get(stat.filename, {})
        noisy = {key for key, method in stat.methods.items() if _is_noise(method, file_lines)}
        if not noisy:
            continue
        kept = set().union(*(m.lines for k, m in stat.methods.items() if k not in noisy))
        dropped = set().union(*(stat.methods[k].lines for k in noisy)) - kept
        stat.lines -= dropped
        coverage.noise_lines += len(dropped)
        for key in noisy:
            del stat.methods[key]


def totals_for(file_lines: dict[int, LineStat], numbers: set[int]) -> Totals:
    """Compute totals for a set of line numbers in one file."""
    totals = Totals()
    for number in numbers:
        stat = file_lines.get(number)
        if stat is not None:
            totals.add(stat)
    return totals


def compress_ranges(numbers: list[int]) -> list[str]:
    """Collapse sorted line numbers into ``a-b`` ranges."""
    ranges: list[str] = []
    start = previous = None
    for number in numbers:
        if start is None:
            start = previous = number
        elif number == previous + 1:
            previous = number
        else:
            ranges.append(str(start) if start == previous else f"{start}-{previous}")
            start = previous = number
    if start is not None:
        ranges.append(str(start) if start == previous else f"{start}-{previous}")
    return ranges


def _cap(items: list[str], limit: int) -> str:
    """Join at most ``limit`` items and note how many were omitted."""
    text = ", ".join(items[:limit])
    return f"{text}, +{len(items) - limit} more" if len(items) > limit else text


def _partial_label(number: int, stat: LineStat) -> str:
    """Describe a partially covered branch line."""
    missing = stat.branches_total - stat.branches_covered
    return f"L{number} ({_percent(stat.branches_covered, stat.branches_total)} branch, {missing}/{stat.branches_total} missing)"


def _gap_row(label: str, numbers: set[int], file_lines: dict[int, LineStat]) -> str:
    """Render one method row with its uncovered and partial lines."""
    ordered = sorted(n for n in numbers if n in file_lines)
    uncovered = [n for n in ordered if file_lines[n].is_uncovered]
    partial = [n for n in ordered if file_lines[n].is_partial]
    parts = [f"- `{label}`: {totals_for(file_lines, numbers).describe()}"]
    if uncovered:
        parts.append("uncovered " + _cap(compress_ranges(uncovered), 10))
    if partial:
        parts.append("partial " + _cap([_partial_label(n, file_lines[n]) for n in partial], 6))
    return " | ".join(parts)


def render_class(totals: Totals, stat: ClassStat, file_lines: dict[int, LineStat], max_methods: int) -> list[str]:
    """Render one class block with its worst methods."""
    block = [f"### {stat.name} | `{stat.filename}` | {totals.describe()}"]
    rows = [(totals_for(file_lines, m.lines), m) for m in stat.methods.values()]
    rows = [(t, m) for t, m in rows if t.has_gaps]
    rows.sort(key=lambda row: (*row[0].priority, row[1].name))
    for _, method in rows[:max_methods]:
        block.append(_gap_row(method.name, method.lines, file_lines))
    if len(rows) > max_methods:
        block.append(f"- ... {len(rows) - max_methods} more method(s) with gaps")
    orphan = stat.lines - set().union(*(m.lines for m in stat.methods.values()))
    if totals_for(file_lines, orphan).has_gaps:
        block.append(_gap_row("(outside methods)", orphan, file_lines))
    return block


def _threshold(label: str, rate: float, threshold: float) -> str:
    """Render a PASS/FAIL threshold check."""
    return f"{label} >= {threshold:.1f}%: {'PASS' if rate * 100 >= threshold else 'FAIL'}"


def _stamp(timestamp: float) -> str:
    """Format a file modification time for the report header."""
    return datetime.fromtimestamp(timestamp).strftime("%Y-%m-%d %H:%M")


def render(coverage: Coverage, reports: list[Path], target: Path, args: argparse.Namespace, warnings: list[str]) -> list[str]:
    """Build the Markdown report lines within the ``--max-lines`` budget."""
    stamps = [r.stat().st_mtime for r in reports]
    oldest, newest = min(stamps), max(stamps)
    out = ["# Cobertura Coverage Summary",
           f"- Reports: {len(reports)} under `{target.as_posix()}` (written {_stamp(oldest)} to {_stamp(newest)})"]
    if newest - oldest > _STALE_SPREAD_SECONDS:
        out.append("- WARNING: reports span more than 30 minutes; stale runs may be mixed in. Clean the results directory and re-run")
    overall = Totals()
    per_assembly: dict[str, Totals] = {}
    seen: set[tuple[str, int]] = set()
    for stat in coverage.classes.values():
        file_lines = coverage.lines.get(stat.filename, {})
        assembly_totals = per_assembly.setdefault(stat.assembly, Totals())
        for number in stat.lines:
            key = (stat.filename, number)
            if key in seen or number not in file_lines:
                continue
            seen.add(key)
            overall.add(file_lines[number])
            assembly_totals.add(file_lines[number])
    out.append(f"- Overall: {overall.describe(counts=True)}")
    checks = []
    if args.line_threshold is not None:
        checks.append(_threshold("line", overall.line_rate, args.line_threshold))
    if args.branch_threshold is not None:
        checks.append(_threshold("branch", overall.branch_rate, args.branch_threshold))
    if checks:
        out.append("- Thresholds: " + " | ".join(checks))
    if coverage.excluded_files or coverage.noise_lines:
        out.append(f"- Excluded: {len(coverage.excluded_files)} generated file(s), "
                   f"{coverage.noise_lines} auto-property/compiler-generated line(s)")
    out.extend(f"- WARNING: {w}" for w in warnings[:5])
    gaps = []
    for stat in coverage.classes.values():
        totals = totals_for(coverage.lines.get(stat.filename, {}), stat.lines)
        if totals.has_gaps:
            gaps.append((totals, stat))
    gaps.sort(key=lambda g: (*g[0].priority, g[0].lines_covered - g[0].lines_valid, g[1].name))
    out.append(f"- Classes with gaps: {len(gaps)} of {len(coverage.classes)}")
    out += ["", "## By assembly", "| Assembly | Line | Branch |", "| :-- | --: | --: |"]
    for name, totals in sorted(per_assembly.items(), key=lambda kv: kv[1].priority):
        out.append(f"| {name} | {_percent(totals.lines_covered, totals.lines_valid)} | "
                   f"{_percent(totals.branches_covered, totals.branches_valid)} |")
    out += ["", "## Gaps (worst first)"]
    if not gaps:
        out.append("No gaps: every measured line and branch is covered.")
        return out
    budget = max(args.max_lines - 1, len(out) + 2)
    shown = 0
    for totals, stat in gaps:
        if shown >= args.top:
            break
        block = render_class(totals, stat, coverage.lines.get(stat.filename, {}), args.max_methods)
        if len(out) + len(block) > budget:
            break
        out.extend(block)
        shown += 1
    if shown < len(gaps):
        out.append(f"_{len(gaps) - shown} more class(es) with gaps omitted; narrow with --include or raise --top/--max-lines._")
    return out


def main(argv: list[str] | None = None) -> int:
    """Run the summarizer and return the process exit code."""
    args = parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    root = Path(args.root).resolve()
    target = Path(args.path)
    reports = find_reports(target)
    coverage = Coverage()
    warnings: list[str] = []
    includes = [i.lower() for i in args.include]
    for report in reports:
        try:
            parse_report(report, root, includes, coverage)
        except (ET.ParseError, OSError, ValueError) as ex:
            warnings.append(f"could not parse `{report.as_posix()}`: {ex}")
    if not reports or (not coverage.classes and warnings):
        print("# Cobertura Coverage Summary")
        print(f"No readable *coverage.cobertura.xml files under `{target.as_posix()}`. "
              f"Run `dotnet test --collect:\"XPlat Code Coverage\" --results-directory {target.as_posix()}` first.")
        for warning in warnings[:5]:
            print(f"- WARNING: {warning}")
        return 2
    remove_noise(coverage)
    print("\n".join(render(coverage, reports, target, args, warnings)))
    return 0


if __name__ == "__main__":
    sys.exit(main())

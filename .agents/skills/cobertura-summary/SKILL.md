---
name: cobertura-summary
description: >-
  Collects fresh .NET Cobertura coverage and Python unittest results, then
  condenses coverage gaps into compact Markdown with summarize_cobertura.py.
  Used by flash-researcher (coverage triage) and test-auditor. Never read raw
  *.cobertura.xml files.
---

# cobertura-summary

## Hard rules
- Never use `view_file`, `Get-Content`, or `Select-String` on `*.cobertura.xml` or `*.trx`. Your only coverage input is the stdout of `summarize_cobertura.py`
- Follow `05-sandbox-execution.md`: `dotnet`, `git`, and `uv` run with `BypassSandbox: true`, one command per call, no chaining
- Deleting `CollectedTestResults` (step 1) is the only filesystem change this skill allows

## Steps
1. **Clean (sandboxed):** `Remove-Item -LiteralPath CollectedTestResults -Recurse -Force -ErrorAction SilentlyContinue`
2. **Collect .NET coverage** (`WaitMsBeforeAsync: 10000`, then wait for the completion notification):
	- Full: `dotnet test StreamActions.sln --collect:"XPlat Code Coverage" --results-directory ./CollectedTestResults -v q -nologo`
	- Scoped: `dotnet test <Project>.Tests/<Project>.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./CollectedTestResults -v q -nologo`, once per project
	- Do not pass the CI-only `UseSourceLink=true` setting. Failing tests still produce coverage: record the failures and continue
3. **Python tests:** for each of `Diff`, `TldRegexUpdater`, and `DependencyUpdater`, run `Test-Path <Folder>/tests` (sandboxed). If it returns true, run `uv run --project <Folder> python -m unittest discover -s <Folder>/tests -t <Folder>`. Otherwise, record `<Folder>: no tests/ package`
4. **Summarize:** `uv run --script .agents/skills/cobertura-summary/scripts/summarize_cobertura.py CollectedTestResults`
	- Options: `--include <path text>` (repeatable), `--line-threshold <pct>`, `--branch-threshold <pct>`, `--top <n>` (default 30), `--max-methods <n>` (default 6), `--max-lines <n>` (default 140)
	- A non-zero exit code (or "No readable *coverage.cobertura.xml files" output) means no reports were found: recheck step 2, never open the XML
	- If the output contains `WARNING: reports span`, repeat from step 1
5. **Freshness stamp:** `git log -1 --format=%H` and `git status --short`

## Output: Coverage Packet
```
COVERAGE PACKET
COMMIT: <hash> | TREE: clean | dirty (<n> files) | GENERATED: <ISO-8601 time>
SCOPE: <solution | test projects run>
DOTNET TEST: <Project>.Tests: passed <n>, failed <n>, skipped <n> [failing: <FQN>, ...]
PYTHON UNITTEST: <Folder>: Ran <n>, OK | FAILED (failures=<n>, errors=<n>) [<test ids>] | <Folder>: no tests/ package
COVERAGE SUMMARY:
<summarize_cobertura.py stdout, verbatim>
SKIPPED: <blocked commands, or "none">
```

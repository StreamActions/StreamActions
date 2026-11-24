---
name: dotnet-build-verify
description: >-
  Caller-aware verification loop for C# changes in StreamActions: dotnet format,
  dotnet build, and dotnet test until no info, warning, or error diagnostics
  remain. Implementers (rapid-coder, deep-debugger) verify and fix; the
  orchestrator verifies and delegates fixes. Use after creating or editing any
  .cs file and for the orchestrator's single final verification pass.
---

# dotnet-build-verify

All commands run with `BypassSandbox: true`. They are auto-approved (see `05-sandbox-execution.md`). Run one command per call and never chain commands.

## Role (decide first)
- **IMPLEMENTER:** `rapid-coder`, `deep-debugger`. Run *Verify*, then *Remediate*
- **VALIDATOR:** `orchestrator` (and any main agent doing final checks). Run *Verify*, then *Hand off*. Never *Remediate*

## Inputs
- `<project>`: the `.csproj` that owns the edited files, for example `StreamActions.Twitch/StreamActions.Twitch.csproj`
- `<files>`: the edited `.cs` files, relative to the repository root and separated by spaces
- `<tests>`: the matching test project (`<Project>.Tests/<Project>.Tests.csproj`, for example `StreamActions.Core.Tests/StreamActions.Core.Tests.csproj`) and a `--filter` expression, for example `--filter "Member=GetGames"` (based on the `[Trait("Member", ...)]` convention)

## Verify (no manual edits)
1. Auto-fix (mechanical, allowed for both roles): `dotnet format <project> --severity info --include <files>`
2. List what remains: `dotnet format <project> --severity info --verify-no-changes --include <files>`. Exit code 0 means clean
3. Build: `dotnet build <project> -v q -nologo`
	- `dotnet build` never reports info-level diagnostics, and `dotnet format` misses some analyzer warnings (for example `CA1311`). Both steps are required
	- `Directory.Build.props` already enables `EnforceCodeStyleInBuild` and `latest-all` analysis, so do not add extra properties
4. Test: `dotnet test <tests> --filter <expr> -v q -nologo`. Only address failures in the files being created or edited, unless told otherwise
5. Collect every remaining diagnostic (ignore duplicate lines for the same diagnostic) and normalize each to a SIG (`00-global-discipline.md` § Escalation contract)

## Remediate (IMPLEMENTER only)
- Fix each SIG manually within the escalation budget (`00-global-discipline.md`): 2 attempts per SIG, 8 verify cycles per run. After any manual fix, restart from *Verify* step 1
- Suppress a diagnostic only as allowed by `10-csharp-dotnet.md` (a justified `[SuppressMessage]`)
- When the budget is spent, return `STATUS: BLOCKED` with an Escalation Packet

## Hand off (VALIDATOR only)
- Fix a diagnostic yourself only if it meets the orchestrator's direct-edit threshold (`orchestrator.md` routing rule 5, for example a single unused `using`) and its SIG is not in the ledger. Re-verify once; if it still fails, delegate
- Otherwise capture the verbatim diagnostic lines (deduplicated, at most 20), their SIGs, and the repro command. Check the `ESCALATION LEDGER` and route per the escalation contract: `rapid-coder` first, `deep-debugger` only as the contract specifies
- After the fix returns, re-run *Verify* for the affected projects, then the solution-wide checks below

## Final verification (VALIDATOR)
- Run once, after all subagent edits are integrated
- Skip any project that a subagent already reported as passing, with no files changed since
- Run the solution-wide `dotnet build StreamActions.sln -v q -nologo` and `dotnet test StreamActions.sln -v q -nologo` only when changes span multiple projects
- A failure in a file no subagent touched is a new SIG: dispatch `rapid-coder` first

## Validation
The skill is complete when:
- *Verify* step 2 exits 0;
- step 3 prints no `warning` or `error` lines;
- step 4 reports zero failed tests;
- all newly added or edited non-public (`private`, `protected`, `internal`, `file`) types, fields, constants, properties, constructors, and methods have XMLDoc (since `CS1591` only flags `public` members).

Report this using the `VERIFIED` section of the Result Report.

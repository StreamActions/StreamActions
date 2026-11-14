---
name: dotnet-build-verify
description: >-
  Verification loop for C# changes in StreamActions: dotnet format (auto-fix),
  dotnet format --verify-no-changes, dotnet build, and dotnet test, repeated
  until no info, warning, or error diagnostics remain. Use this skill after
  creating or editing any .cs file, before reporting a C# task as done, and
  for the orchestrator's single final verification pass.
---

# dotnet-build-verify

All commands run with `BypassSandbox: true`. They are auto-approved (see `05-sandbox-execution.md`). Run one command per call and never chain commands.

## Inputs
- `<project>`: the `.csproj` that owns the edited files, for example `StreamActions.Twitch/StreamActions.Twitch.csproj`
- `<files>`: the edited `.cs` files, relative to the repository root and separated by spaces
- `<tests>`: the matching test project (`StreamActions.Twitch.Tests/StreamActions.Twitch.Tests.csproj` or `StreamActions.Common.Tests/StreamActions.Common.Tests.csproj`) and a `--filter` expression, for example `--filter "Member=GetGames"` (based on the `[Trait("Member", ...)]` convention)

## Steps
1. Auto-fix: `dotnet format <project> --severity info --include <files>`
2. List what remains: `dotnet format <project> --severity info --verify-no-changes --include <files>`
	- Exit code 0 means clean; go to step 3
	- Otherwise, fix each reported `info`, `warning`, or `error` line manually (ignore duplicate lines for the same diagnostic), then repeat steps 1–2
3. Build: `dotnet build <project> -v q -nologo`
	- `dotnet build` never reports info-level diagnostics, and `dotnet format` misses some analyzer warnings (for example `CA1311`). Both steps are required
	- `Directory.Build.props` already enables `EnforceCodeStyleInBuild` and `latest-all` analysis, so do not add extra properties
	- Fix every warning, then rerun step 1 (format again after any manual fix) before rebuilding
4. Test: `dotnet test <tests> --filter <expr> -v q -nologo`
	- Only address failures in the files being created or edited, unless told otherwise
5. Suppress a diagnostic only as allowed by `10-csharp-dotnet.md` (a justified `[SuppressMessage]`)

## Attempt accounting (escalation contract)
- An **attempt** is one code change aimed at a specific diagnostic, test failure, or runtime error, followed by rerunning the step that reported it
- After **2 failed attempts on the same symptom**, stop. Do not try a third experiment. Return `STATUS: BLOCKED` with an Escalation Packet (`00-global-discipline.md`). The orchestrator will route it to `deep-debugger`
- Rerunning an unchanged command is not an attempt. Never do it hoping for a different result

## Final verification (orchestrator)
- Run once, after all subagent edits are integrated
- Skip any project that a subagent already reported as passing, with no files changed since
- Run the solution-wide `dotnet build StreamActions.sln -v q -nologo` and `dotnet test StreamActions.sln -v q -nologo` only when changes span multiple projects

## Validation
The skill is complete when:
- step 2 exits 0;
- step 3 prints no `warning` or `error` lines;
- step 4 reports zero failed tests;
- all newly added or edited non-public (`private`, `protected`, `internal`, `file`) types, fields, constants, properties, constructors, and methods have XMLDoc (since `CS1591` only flags `public` members).

Report this using the `VERIFIED` section of the Result Report.

---
trigger: always_on
---

# Sandbox Execution

## Commands that always run outside the sandbox
- `dotnet`, `git`, and `uv` always run with `BypassSandbox: true`, on the first attempt. The sandbox container blocks the NuGet package cache, .NET SDK workload folders, MSBuild node IPC, git metadata access, and the uv cache and Python installs, so these commands fail inside it (this includes read-only `git` commands run by subagents)
- Never retry or diagnose a sandbox failure of these tools. The sandbox is never the bug. If one was run sandboxed by mistake, immediately rerun the identical command with `BypassSandbox: true`. Do not try variations, environment variables, `--no-restore`, or alternative flags
- Everything else (`Get-ChildItem`, `Select-String`, `Get-Content`, `Test-Path`) stays in the sandbox

## Command shape (keeps auto-approval prefix matching working)
- The first token must be the binary (`dotnet`, `git`, `uv`). Pass paths as arguments instead of using `cd` or `Set-Location`
- One command per call. No chaining with `;`, `&&`, `||`, or pipes. No `$(...)` substitution and no shell variables
- Keep output small:
	- `dotnet build|test`: use `-v q -nologo`, target a specific `.csproj` instead of the solution, and use `--filter` with `dotnet test`
	- `dotnet format`: use `--include <files>`
	- `git diff`: run `git diff --stat` before a full diff

## Auto-approved commands (outside the sandbox)
<!-- BEGIN SYNCED ALLOWLIST: managed by skill sync-command-permissions. Do not hand-edit. -->
Last synced: 2026-10-31 from 14dff664-ff30-444d-95d5-bda4744bb3fd.json.

| Directive | Auto-approved examples |
| :-- | :-- |
| `command(regex:dotnet (sln list\|build\|format\|list\|test\|run\|--info\|--version).*)` | `dotnet build StreamActions.Twitch/StreamActions.Twitch.csproj -v q -nologo`, `dotnet test ... --filter ...`, `dotnet format ... --include ...`, `dotnet list StreamActions.Twitch/StreamActions.Twitch.csproj package --vulnerable --include-transitive` |
| `command(regex:git (status\|log\|show\|grep\|ls-files\|diff).*)` | `git grep -n "RequireUserToken" -- "*.cs"`, `git log -1 --format=%H`, `git diff --stat` |
| `command(regex:uv (run\|sync).*)` | `uv run --project Diff Diff/DocParserFinder.py --folder .`, `uv sync --locked --project Diff` |
| `command(Get-Process -Name dotnet*, MSBuild*, VBCSCompiler*)` | Exact string only |

Pre-approved URLs: `https://dev.twitch.tv`, `https://twitch.tv`, `https://github.com`
<!-- END SYNCED ALLOWLIST -->

## Commands not on the allowlist
- **Interactive:** they may run with `BypassSandbox: true`, but the operator is prompted. Run only what the task needs, and run nothing speculative
- **`MODE: SCHEDULED` or any unattended run:** never run a command that needs approval. Skip it and list it under `SKIPPED (permission)` in the report
- Currently **not** allow-listed: `uv lock/add/remove/pip`, `git add/commit/checkout/switch/stash/push`, and `dotnet add/remove/restore/clean/new`

## Locked build outputs
- If a build fails because a file is in use (`MSB3026`, `MSB3027`, `MSB3021`), run the allow-listed `Get-Process -Name dotnet*, MSBuild*, VBCSCompiler*` to identify the process, then report it to the operator. Never stop processes without approval

## Stale allowlist
- If an allow-listed command unexpectedly prompts or is denied, the table above may be out of date. In interactive mode, suggest running the `sync-command-permissions` skill


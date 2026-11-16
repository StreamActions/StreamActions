---
name: sync-command-permissions
description: >-
  Rewrites the synced auto-approved command allowlist in
  .agents/rules/05-sandbox-execution.md from a permission list or report the
  operator pastes into chat. On demand only; never in MODE: SCHEDULED.
---

# sync-command-permissions

Input comes only from the operator's message. Never read config files, `~/.gemini/`, or any path outside the workspace, and never search the file system for permission data. The only file this skill writes is `.agents/rules/05-sandbox-execution.md`.

## Input
- Accept any of these, pasted in the prompt: a JSON array, a JSON object containing an `allow` array, a Markdown or plain list with one directive per line, or a free-form operator report with directives embedded in it
- If nothing was pasted, ask the operator to paste the current allow list and stop

## Steps
1. Extract directives with a token scan (ignore surrounding prose, quotes, commas, and list markers):
	- `command(regex:<pattern>)`: a regex directive
	- `command(<literal>)`: an exact-match directive
	- `read_url(<url>)`: a pre-approved URL
	- Ignore every other directive type and all non-directive text. Never copy credentials or unrelated settings. Remove duplicates, keeping the first occurrence
2. Risk check before writing: list any directive that matches write or execution verbs (`git (add|commit|push|checkout|reset)`, `dotnet (run|add|remove|new)`, `uv (add|remove|lock|pip)`) or an unanchored `.*` binary wildcard. Show this list to the operator; do not drop entries
3. Rewrite only the text between `<!-- BEGIN SYNCED ALLOWLIST` and `<!-- END SYNCED ALLOWLIST -->` in `.agents/rules/05-sandbox-execution.md`:
	- `Last synced: <yyyy-MM-dd> from operator-provided list.`
	- One table row per `command(...)` directive, in input order. Escape `|` as `\|` inside table cells. Give 1–3 concrete auto-approved examples that use real repository paths
	- A `Pre-approved URLs:` line listing the `read_url` origins
4. Update the `Currently **not** allow-listed` bullet in `## Commands not on the allowlist` so it names the common `dotnet`, `git`, and `uv` verbs the directives do not cover

## Validation
- Re-read the rewritten block. Every extracted `command(...)` directive must appear exactly once, and no text outside the markers may have changed
- Report a diff summary: directives added, removed, and unchanged, plus the step 2 risk list

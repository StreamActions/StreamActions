---
name: sync-command-permissions
description: >-
  Refreshes the auto-approved command allowlist recorded in
  .agents/rules/05-sandbox-execution.md from the operator's Antigravity
  permission grants. Use this skill only on demand: when the operator asks to
  sync permissions, after the operator changes Permission Grants, or when an
  allow-listed command unexpectedly prompts. Never use it in MODE: SCHEDULED
  runs.
---

# sync-command-permissions

This skill never reads or writes the operator's global `config.json` (it is behind a hardcoded protection boundary; do not try to reach it through shell commands, scripts, or other paths). It only consumes an `allow` list that the operator supplies. The only file this skill writes is `.agents/rules/05-sandbox-execution.md`.

## Input sources (use the first that applies)
1. **Pasted list.** The operator pasted `allow` entries in the prompt (raw JSON array, the whole `permissionGrants` object, or one directive per line)
2. **Pointer file.** Read `.agents/local-config-path.txt` (gitignored), which holds a single absolute path to one of:
	- a project config JSON, for example `C:\Users\<user>\.gemini\config\projects\<project-id>.json`. Confirm that `projectResources.resources[].gitFolder.folderUri` points to this repository, otherwise stop and report the mismatch. Read `permissionGrants.permissionGrants.allow[]`
	- an operator-generated export (`.md`, `.txt`, or `.json`) containing the `allow` entries, one directive per line (optionally as Markdown list items) or as a JSON array
3. **Ask.** If neither exists, ask the operator to paste the list or to provide a pointer path. Do not search the file system

Reading a pointer file outside the workspace may prompt the operator; that is expected.

## Steps
1. Obtain the entries from the source above
2. Keep only these directive types:
	- `command(regex:<pattern>)`: a regex directive
	- `command(<literal>)`: an exact-match directive
	- `read_url(<url>)`: a pre-approved URL
	- Ignore every other directive type (for example `read_file(...)`). Never copy credentials or unrelated settings. Remove duplicates, keeping the first occurrence
3. Rewrite only the text between `<!-- BEGIN SYNCED ALLOWLIST` and `<!-- END SYNCED ALLOWLIST -->` in `.agents/rules/05-sandbox-execution.md`:
	- Update `Last synced: <yyyy-MM-dd> from <source: pasted list | pointer file name>.`
	- Write one table row per `command(...)` directive, in input order. Escape `|` as `\|` inside table cells. Give 1–3 concrete auto-approved examples that use real repository paths
	- Write a `Pre-approved URLs:` line listing the `read_url` origins
4. Update the `Currently **not** allow-listed` bullet in `## Commands not on the allowlist` so it names the common `dotnet`, `git`, and `uv` verbs the entries do not cover

## Validation
- Re-read the rewritten block. Every `command(...)` directive from the input must appear exactly once, and no text outside the markers may have changed
- Report a diff summary: directives added, removed, and unchanged

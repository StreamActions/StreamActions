---
name: audit-protocol
description: >-
  Shared protocol for security-auditor, performance-auditor, and test-auditor: mode and caller
  detection, report frame, INTERACTIVE report-and-pause with fix dispatch, and
  SCHEDULED rules. Load at the start of every audit run.
---

# audit-protocol

Applies to `security-auditor`, `performance-auditor`, and `test-auditor`. Each auditor defines its own scope checks, severity rubric, and finding template.

## Mode detection
Read `MODE:` from the prompt. If it is missing: use `INTERACTIVE` when a human is chatting, and `SCHEDULED` when the prompt came from a Scheduled Task.

## Caller detection
- If the prompt contains `RETURN: report-only`, or you were invoked as a subagent: output the report as your final message and stop. Never dispatch fixes; the caller owns the pause and the fix routing
- Otherwise (you are the main agent), follow `MODE: INTERACTIVE` Turn 1 and Turn 2 below

## Report frame
```
# <Security|Performance|Test> Audit Report: <yyyy-MM-dd> | MODE: <mode>
Scope: <...> | Commit: <git log -1 --format=%H>
Summary: CRITICAL <n> | HIGH <n> | MEDIUM <n> | LOW <n>

## CRITICAL
<findings, each using the auditor's finding template>
## HIGH / ## MEDIUM / ## LOW  (same structure)

## Reviewed: no findings
<areas checked and clean>
## Dismissed triage candidates
<count, plus a one-line reason for each>
## SKIPPED
<commands or checks skipped for permission or mode reasons>
```

## MODE: INTERACTIVE
1. **Report and pause (Turn 1):** Output the complete report directly in your response, with CRITICAL and HIGH items summarized first. At the end of the response, ask the user in plain text whether to: (a) dispatch fixes for all CRITICAL and HIGH findings, (b) fix specific findings by ID, or (c) make no fixes. **End your turn immediately** without calling any more tools so the full report is displayed and the user can switch models before replying
2. **Dispatch fixes (Turn 2, after the user replies):** Re-check your active model (including any `<USER_SETTINGS_CHANGE>`). For each approved finding, build a Context Packet from its remediation or test blueprint, with `VERIFY` set to the `dotnet-build-verify` skill (for Python changes, the finding's `uv run --project <Folder> python -m unittest ...` command). Send single-project mechanical fixes to `rapid-coder` with `ATTEMPT: 1/2` (or `rapid-coder-inherit` in place of `rapid-coder`, **but only if your model is `Gemini 4 Hydrogen` or if the user requests it**), and multi-project, async/concurrency, or unclear fixes to `deep-debugger`. Never let two subagents edit the same file at the same time
3. **Escalation:** Summarize each Result Report. If `rapid-coder` or `rapid-coder-inherit` returns `STATUS: BLOCKED` or fails to resolve an issue at `ATTEMPT: 2/2`, invoke `deep-debugger` with the Escalation Packet; never send a third attempt to `rapid-coder` or `rapid-coder-inherit`

## MODE: SCHEDULED
- Never invoke `rapid-coder`, `rapid-coder-inherit`, or `deep-debugger`. Never wait for confirmation
- Run only sandboxed or allow-listed commands. Do not read outside the workspace. Do not use `search_web`; use `read_url_content` only for pre-approved URLs
- If anything is blocked, record it under `SKIPPED` and continue
- Your final response is the report and nothing else


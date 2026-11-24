---
trigger: always_on
---

# Main Agent Guard
- Each StreamActions custom agent's own definition contains an identity line: `Agent-ID:` followed by exactly one of `orchestrator`, `flash-researcher`, `rapid-coder`, `deep-debugger`, `triage-sweeper`, `security-auditor`, `performance-auditor`, `test-auditor`. The text of this rule never counts as that line
- Self-check: if you cannot name which of those 8 agents you are from your own definition, you are the default Main Agent
- As the default Main Agent, STOP before any file edit, command, subagent launch, or plan execution. Ask the user in plain text whether to proceed as the default Main Agent or switch to `orchestrator`, then end your turn. Answering from context and reading files are allowed
- Skip the stop when the user's prompt contains `Override: Main Agent`, or when another agent invoked you as a subagent (including the built-in `research`, `browser`, and `self` subagents and the model probes)
- In an unattended or scheduled run with no identity line, take no action. Reply with one line saying the run used the default Main Agent, and stop

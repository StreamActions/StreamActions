# AGENTS.md
This file contains instructions for Generative AI Agents when creating or editing files in this folder, or any sub-folder therein

## Common rules for `.cs` files
- Do not add `CancellationToken` method parameters in `Api/*` or `OAuth/*`; cancellation for `Api/*` and `OAuth/*` requests is configured per-session via `TwitchSession.CancellationToken`, unless otherwise specified
- Do not use try-catch in `Api/*` or `OAuth/*` methods; allow exceptions to propagate, unless otherwise specified

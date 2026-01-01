# AGENTS.md
This file contains instructions for Generative AI Agents when creating or editing files in this folder, or any sub-folder therein, including, but not limited to: Jules, Copilot, etc

## Common rules for `.cs` files
- Remove the `/helix` prefix from the beginning of the Uri, as `TwitchAPI.PerformHttpRequest` already prepends it
- Do not use `CancellationToken`, unless otherwise specified
- Do not use try-catch, allow exceptions to propagate, unless otherwise specified

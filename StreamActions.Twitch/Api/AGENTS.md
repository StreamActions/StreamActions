# AGENTS.md
This file contains instructions for Generative AI Agents when creating or editing files in this folder, or any sub-folder therein, including, but not limited to: Jules, Copilot, etc

## Common rules for `.cs` files
- Use `StreamActions.Twitch/Api/Games/Game.cs` and `StreamActions.Twitch/Api/ChannelPoints/ChannelPointsRedemption.cs` as style references
- Do not put `Async` in the method name, unless otherwise specified
- Do not explicitly check `session.Token`, allow `session.RequireToken()` to handle the check
- `session.RequireToken()` is always required in methods which call `TwitchAPI.PerformHttpRequest`, even if no scope is required
- `session.RequireToken()` should be placed after checking if any arguments are null or out of range
- If scopes are required, specify the first potential scope as the first parameter to `session.RequireToken`
- If scopes are required and an alternate scope is available, specify the alternate potential scope as the second parameter to `session.RequireToken`
- If scopes are required, the XMLDoc must also specify the `TwitchScopeMissingException` that would be thrown by `session.RequireToken` in the same style as `GetCustomRewardRedemption` in `StreamActions.Twitch/Api/ChannelPoints/ChannelPointsRedemption.cs`, even if it is not explicitly thrown
- Remove the `/helix` prefix from the beginning of the Uri, as `TwitchAPI.PerformHttpRequest` already prepends it
- When a method contains both the `before` and `after` parameters, they are mutually exclusive and should throw `InvalidOperationException` when both specified. Use the same format and message as `GetVideos` in `StreamActions.Twitch/Api/Videos/Video.cs`
- Inline the `requestUri` parameter in the call to `TwitchAPI.PerformHttpRequest`
- If a method has a parameter named `first`, set it to be not null with a default value based on the API documentation
- If a method has a parameter named `first`, do not throw an exception if it is out of range, instead use `Math.Clamp`
- Add remarks to the XMLDoc of methods to document the HTTP return codes, in a similar style to `GetGames` in `StreamActions.Twitch/Api/Games/Game.cs`
- Use the descriptions specified for `GetGames` in `StreamActions.Twitch/Api/Games/Game.cs` for HTTP 400 and 401 when writing XMLDoc

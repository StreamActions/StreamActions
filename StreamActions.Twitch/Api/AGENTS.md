# AGENTS.md
This file contains instructions for Generative AI Agents when creating or editing files in this folder, or any sub-folder therein

## Common rules for `.cs` files
- Use `StreamActions.Twitch/Api/Games/Game.cs` and `StreamActions.Twitch/Api/ChannelPoints/ChannelPointsRedemption.cs` as style references
- Do not put `Async` in the method name, unless otherwise specified
- Remove the `/helix` prefix from the beginning of the Uri, as `TwitchApi.PerformHttpRequest` already prepends it
- Do not explicitly check `session.Token`; allow `session.RequireUserToken()`, `session.RequireUserOrAppToken()`, or `session.RequireAppToken()` to handle the check
- `session.RequireUserToken()`, `session.RequireUserOrAppToken()`, or `session.RequireAppToken()` is always required in methods which call `TwitchApi.PerformHttpRequest`, even if no scope is required
- `session.RequireUserToken()`, `session.RequireUserOrAppToken()`, or `session.RequireAppToken()` should be placed after checking if any arguments are null or out of range
- If scopes are required, specify the first potential scope as the first parameter to `session.RequireUserToken` or `session.RequireUserOrAppToken`
- If scopes are required and an alternate scope is available, specify the alternate potential scope as the second parameter to `session.RequireUserToken` or `session.RequireUserOrAppToken`
- If scopes are required, the XMLDoc must include `<exception cref="TwitchScopeMissingException">` (thrown by `session.RequireUserToken` or `session.RequireUserOrAppToken`) in the same style as `GetCustomRewardRedemption` in `StreamActions.Twitch/Api/ChannelPoints/ChannelPointsRedemption.cs`, even if it is not explicitly thrown
- When a method contains both the `before` and `after` parameters, they are mutually exclusive and should throw `InvalidOperationException` when both specified. Use the same format and message as `GetVideos` in `StreamActions.Twitch/Api/Videos/Video.cs`
- Construct `Uri uri = Util.BuildUri(...);` on the line before `TwitchApi.PerformHttpRequest` when query parameters are used, or pass `new("/...", UriKind.Relative)` directly when no query parameters are needed.
- If a method has a parameter named `first`, set it to be not null with a default value based on the API documentation
- If a method has a parameter named `first`, do not throw an exception if it is out of range, instead use `Math.Clamp`
- Follow `GetGames` in `StreamActions.Twitch/Api/Games/Game.cs` for the HTTP status code `<remarks>` style, and reuse its HTTP 400 and 401 descriptions
- When a Twitch API endpoint supports both app and user tokens, use `session.RequireUserOrAppToken`. In this case, remove the `<exception cref="TokenTypeException">` tag from the XML documentation and document App token specific scope or moderator requirements in the `<remarks>` section.
- For Twitch API methods that strictly require a User access token, the XML documentation must include an `<exception cref="TokenTypeException">` tag and a remark stating: `This ID must match the user ID in the access token.` (if the endpoint requires input IDs like `broadcaster_id` or `moderator_id` to match the token subject).
- Token scope validation methods in `TwitchSession` (such as `RequireUserToken` and `RequireUserOrAppToken`) use OR logic, meaning they throw an exception only if none of the provided scopes are present in the token.
- C# records in `StreamActions.Twitch/Api` and its sub-folders should be sealed, except for the `ResponseData` record and any records that are abstract or serve as a base for inheritance.
- For Twitch API endpoints requiring a JSON body for parameters, create a separate record named `[Action]Parameters` (e.g., `AddSuspiciousStatusParameters`) in its own file within the same category folder to model the request body.

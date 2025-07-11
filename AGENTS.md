# AGENTS.md
This file contains instructions for Generative AI Agents, including, but not limited to: Jules, Copilot, etc

## Common rules related to AGENTS.md for all files
- When creating or editing a file, check all folders in the relevant path for an `AGENTS.md`, which may have additional instructions specific to that folder
	- For example: If creating or editing `StreamActions.Twitch/Api/Games/Game.cs`, check for an `AGENTS.md` in: `StreamActions.Twitch`, `StreamActions.Twitch/Api`, and `StreamActions.Twitch/Api/Games`
- When multiple `AGENTS.md` contain conflicting instructions, the instruction in the most specific path should be used
	- For example: If creating or editing `StreamActions.Twitch/Api/Games/Game.cs` and it is found that `StreamActions.Twitch` and `StreamActions.Twitch/Api` have conflicting instructions in their `AGENTS.md`, the instruction in `StreamActions.Twitch/Api` should be used
	- Only the conflicting instructions should follow this rule, instructions which do not conflict should still be followed from the other `AGENTS.md` files in the path
- When reading and making use of a class/method from another file, do not read `AGENTS.md` from the path of that file, unless it is also in the path of the file being created or edited
	- For example: If creating or editing `StreamActions.Twitch/Api/Games/Game.cs`: do not check for an `AGENTS.md` in `StreamActions.Common` when using `Util.BuildUri`
	- For example: If creating or editing `StreamActions.Twitch/Api/Games/Game.cs`: do not check for an `AGENTS.md` in `StreamActions.Twitch/Api/Common` when using `TwitchSession.RequireToken`, but do use the `AGENTS.md` in `StreamActions.Twitch` and `StreamActions.Twitch/Api`, since they are still in the path of `Game.cs`
- The above rules may be overridden by an `AGENTS.md` file or the task prompt, when explicitly stated

## Common rules for all files
- After creating the first commit, all additional changes should be made as additional commits to the same branch
- Do not create `.gitkeep`
- Always include the license header, which may be found in `StreamActions.sln.licenseheader`
	- The license header format for different files is separated out into sections marked similarly to `extensions: .cs .js .css`, which indicates the list of file extensions that use the license header below that line
	- An empty line denotes the end of that license header
	- Some file types may require the license header to be placed on line 2 instead of line 1, such as `.sh` files
	- If the license header for a file extension is missing, return an error to the user requesting it
- Remove all extra comments generated by you, which are not related to the documentation required by an `AGENTS.md` or task prompt
- Replace the `�` character with `'` in all comments and documentation

## Common rules for `.cs` files
- Agents should attempt to follow the rules that are enabled in `.editorconfig` if they understand them, unless otherwise specified
- Always import namespaces instead of writing out the full path in a method call
- Use explicit data types for variables instead of `var`
- Use simplified new statements, such as `new("/users")` instead of `new Uri("/users")`
- Use simplified collection initialization, such as `NameValueCollection queryParameters = [];` instead of `NameValueCollection queryParameters = new();`
- When creating/editing records, Enums should be defined inside of the record
- When creating/editing records, Methods should be defined inside of the record
- Use nullable properties with no default values, unless otherwise specified
- All properties must be nullable, unless otherwise specified
- All properties should use `{ get; init; }` instead of `{ get; set; }`, unless otherwise specified
- Use `StreamActions.Common.Util.BuildURI` instead of the `Uri` class to build Uris
- The first parameter of `StreamActions.Common.Util.BuildURI` should be constructed as a new `Uri`
- Use `NameValueCollection` for building query parameters
- For properties in an API which are URIs, use the `Uri` type for the property instead of `string`
- For properties in an API which are timestamps, use the `DateTime` type for the property instead of `string`
- When checking if a string is null, just use `string.IsNullOrWhitespace`, unless otherwise specified
- Add XMLDoc for all interfaces, classes, records, enums, properties, and methods, when reference documentation is available
- Always import namespaces when an exception, interface, class, record, enum, property, or method is being referenced in an XMLDoc, even if not referenced in actual code
- Use `ArgumentNullException` when the count of values in all IEnumerable parameters is `0`, but the method requires values to be present
- Use `ArgumentOutOfRangeException` when multiple IEnumerable parameters are not `null` and contain values, but the method does not allow more than one of them to contain values, using a similar format and message as Use the same format and message as `GetVideos` in `StreamActions.Twitch/Api/Videos/Video.cs`
- Use `ArgumentOutOfRangeException` when too many elements are specified in an IEnumerable parameter
- For `ArgumentOutOfRangeException`, use the style in `GetGames` of `StreamActions.Twitch/Api/Games/Game.cs`, including using the `actualValue` argument
- The `paramName` argument is always required for exceptions which have it, and should always use `nameof` to insert the parameter name
- For the `paramName` argument of exceptions, use the same style as the `ArgumentNullException` of `GetGames` in `StreamActions.Twitch/Api/Games/Game.cs` if the exception is being thrown based on the combined check of multiple parameters

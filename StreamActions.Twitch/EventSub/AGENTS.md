# AGENTS.md
This file contains instructions for Generative AI Agents when creating or editing files in this folder, or any sub-folder therein, including, but not limited to: Jules, Copilot, etc

## Common rules for `.cs` files representing EventSub payloads
- All records should be implemented as `sealed record`
- Use `StreamActions.Twitch/EventSub/Channel/Follow.cs` and `StreamActions.Twitch/EventSub/Channel/Update.cs` as style references
- The main record representing an EventSub payload should inherit from `IEventSubType`
- If a subscription type has multiple versions listed in the documentation, only implement the latest version. Consider `beta` to be a lower/older version than `1`
- If a sub-object is required to represent part of the data payload, it should be created as a separate sealed record in the same folder/namespace as the parent object
- Each EventSub payload record should be placed in an appropriate CamelCased sub-folder/namespace structure based on the subscription type, with `StreamActions.Twitch.EventSub` as the parent. For example, `automod.message.hold` should be placed in the `StreamActions.Twitch/EventSub/Automod/Message` folder, with the record in the `StreamActions.Twitch.EventSub.Automod.Message` namespace, and the record name as `Hold`
	- The `StreamActions.Twitch.EventSub.Common.ChatMessage` namespace is the only exception to the above rule, as the `Message` record can be reused across multiple EventSub namespaces
- Create enums for values where it looks reasonable to do so. The enum should be made part of the record which consumes it. If the enum can be reused with the exact same values across multiple records in the same namespace, define it only once
- The `EventSubConditionType` static property, which represents the condition object used to create the EventSub subscription, has been condensed down into generic objects representing each unique combination of parameters
- It is intentional that the `EventSubConditionType`, `Type`, and `Version` static properties inherit their documentation from `IEventSubType`
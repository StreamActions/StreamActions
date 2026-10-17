/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using StreamActions.Twitch.Api.EventSub;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Common;

/// <summary>
/// An event received from EventSub.
/// </summary>
public record EventSubEvent : IEventSubEvent
{
    /// <summary>
    /// The message metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public EventMetadata? Metadata { get; init; }

    /// <summary>
    /// The event payload.
    /// </summary>
    [JsonPropertyName("payload")]
    public EventPayload? Payload { get; init; }
}

/// <summary>
/// An event received from EventSub.
/// </summary>
/// <typeparam name="TEventType">The type representing the event's specific data.</typeparam>
public sealed record EventSubEvent<TEventType> : EventSubEvent where TEventType : IEventSubType
{
    /// <summary>
    /// The event payload.
    /// </summary>
    [JsonPropertyName("payload")]
    public new EventPayload<TEventType>? Payload
    {
        get => base.Payload as EventPayload<TEventType>;
        init => base.Payload = value;
    }
}

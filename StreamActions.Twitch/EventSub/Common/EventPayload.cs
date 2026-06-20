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
/// An EventSub event payload.
/// </summary>
/// <typeparam name="TEventType">The type representing the subscription's specific data.</typeparam>
public sealed record EventPayload<TEventType> where TEventType : BaseEventType
{
    /// <summary>
    /// The subscription information, containing details about the subscription such as its type, version, status, and cost.
    /// </summary>
    /// <remarks>
    /// This field is only included in <see cref="EventMetadata.MessageTypes.Notification"/> and <see cref="EventMetadata.MessageTypes.Revocation"/> messages.
    /// </remarks>
    [JsonPropertyName("subscription")]
    public EventSubSubscription? Subscription { get; init; }

    /// <summary>
    /// The event data specific to the subscription type, containing information about the event that triggered the subscription.
    /// </summary>
    /// <remarks>
    /// This field is only included in <see cref="EventMetadata.MessageTypes.Notification"/> and <see cref="EventMetadata.MessageTypes.Revocation"/> messages.
    /// </remarks>
    [JsonPropertyName("event")]
    public TEventType? Event { get; init; }

    /// <summary>
    /// Session data for an EventSub websocket connection.
    /// </summary>
    /// <remarks>
    /// This field is only included in <see cref="EventMetadata.MessageTypes.SessionWelcome"/> and <see cref="EventMetadata.MessageTypes.SessionReconnect"/> messages.
    /// </remarks>
    [JsonPropertyName("session")]
    public EventSubSession? Session { get; init; }
}

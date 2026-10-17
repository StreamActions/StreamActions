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

namespace StreamActions.Twitch.EventSub.Common;

/// <summary>
/// Represents the result of receiving and processing an EventSub WebSocket message.
/// </summary>
public sealed record EventSubWebsocketResult
{
    /// <summary>
    /// Gets a value indicating whether the WebSocket message is a duplicate of a previously received message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is <see langword="true"/> when the message ID was already seen within the retention window, in which case <see cref="Event"/> is <see langword="null"/>.
    /// </para>
    /// <para>
    /// This property is <see langword="false"/> for freshly processed messages, and <see langword="null"/> if the received message was a close frame or an error occurred.
    /// </para>
    /// </remarks>
    public bool? IsDuplicate { get; init; }

    /// <summary>
    /// The event received from the WebSocket, if available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For session lifecycle messages, this contains the <see cref="IEventSubEvent"/> where the concrete type is an <see cref="EventSubEvent"/>.
    /// </para>
    /// <para>
    /// For event messages, this contains the <see cref="IEventSubEvent"/> where the concrete type is an <see cref="EventSubEvent{TEventType}"/> resolved from the subscription type.
    /// </para>
    /// <para>
    /// This property is <see langword="null"/> if the message is a duplicate, if a close frame was received, if the subscription type is not implemented, or if an exception occurred.
    /// </para>
    /// </remarks>
    public IEventSubEvent? Event { get; init; }

    /// <summary>
    /// The close code received from the WebSocket close frame, if applicable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is <see langword="null"/> when a standard data frame is received.
    /// </para>
    /// </remarks>
    public EventSub.CloseCodes? CloseCode { get; init; }

    /// <summary>
    /// The exception that occurred during receiving or processing, if any.
    /// </summary>
    public Exception? Exception { get; init; }
}

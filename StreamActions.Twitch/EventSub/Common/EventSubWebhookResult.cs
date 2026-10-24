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
/// Represents the result of processing an EventSub webhook message.
/// </summary>
public sealed record EventSubWebhookResult
{
    /// <summary>
    /// Gets a value indicating whether message signature verification succeeded.
    /// </summary>
    public bool? IsVerified { get; init; }

    /// <summary>
    /// Gets a value indicating whether the webhook message is a duplicate of a previously received message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is <see langword="true"/> when the message ID was already seen within the retention window, in which case <see cref="Event"/> is <see langword="null"/>.
    /// </para>
    /// <para>
    /// This property is <see langword="false"/> for freshly processed messages, and <see langword="null"/> if verification failed or if the message was a verification challenge.
    /// </para>
    /// </remarks>
    public bool? IsDuplicate { get; init; }

    /// <summary>
    /// The HTTP status code that should be returned to the EventSub service.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// The response content type, when a response body is present.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The response body, when present.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// The event received from the webhook, if available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For non-verification messages, this contains the <see cref="IEventSubEvent"/> where the concrete type is an <see cref="EventSubEvent{TEventType}"/> resolved from the subscription type.
    /// </para>
    /// <para>
    /// This property is <see langword="null"/> if verification failed, if the message was a verification challenge, if the message is a duplicate, or if the subscription type is not implemented.
    /// </para>
    /// </remarks>
    public IEventSubEvent? Event { get; init; }
}

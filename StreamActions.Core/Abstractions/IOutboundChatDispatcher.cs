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

using StreamActions.Core.Models.Messaging;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Queues outbound chat and whisper messages for prioritized, rate-limited, deduplicated, and simulcast-aware delivery.
/// </summary>
public interface IOutboundChatDispatcher
{
    /// <summary>
    /// Enqueues an outbound message into the priority lane matching <see cref="OutboundChatMessage.Priority"/>.
    /// </summary>
    /// <param name="message">The outbound message to enqueue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous enqueue operation.</returns>
    ValueTask EnqueueAsync(OutboundChatMessage message, CancellationToken cancellationToken = default);
}

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

using StreamActions.Core.Abstractions;

namespace StreamActions.Core.Models.Messaging;

/// <summary>
/// Immutable concrete representation of an <see cref="IChatChannel"/>.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier that owns this channel.</param>
/// <param name="PlatformId">The platform identifier (for example, <c>twitch</c> or <c>discord</c>).</param>
/// <param name="ChannelId">The platform-specific channel identifier.</param>
/// <param name="ChannelName">The human-readable channel name or login.</param>
public sealed record ChatChannel(
    Guid WorkspaceId,
    string PlatformId,
    string ChannelId,
    string ChannelName) : IChatChannel;

/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// A response object for a <see cref="UnofficialChatters.GetUnofficialChatters"/> request.
/// </summary>
public sealed record UnofficialChattersResponse : TwitchResponse
{
    /// <summary>
    /// The number of chatters contained in <see cref="Chatters"/>.
    /// </summary>
    [JsonPropertyName("chatter_count")]
    public int? ChatterCount { get; init; }

    /// <summary>
    /// The current chatters, by role.
    /// </summary>
    [JsonPropertyName("chatters")]
    public UnofficialChatters? Chatters { get; init; }
}

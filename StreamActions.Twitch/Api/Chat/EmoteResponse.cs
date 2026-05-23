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

using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// The response for an emote request.
/// </summary>
public sealed record EmoteResponse : ResponseData<Emote>
{
    /// <summary>
    /// A templated URL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the values from the <see cref="Emote.Id"/>, <see cref="Emote.Format"/>, <see cref="Emote.Scale"/>, and <see cref="Emote.ThemeMode"/> fields to replace the like-named placeholder strings in the templated URL to create a CDN URL that you use to fetch the emote.
    /// </para>
    /// <para>
    /// The placeholders are <c>{{id}}</c>, <c>{{format}}</c>, <c>{{theme_mode}}</c>, and <c>{{scale}}</c>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("template")]
    public string? Template { get; init; }
}

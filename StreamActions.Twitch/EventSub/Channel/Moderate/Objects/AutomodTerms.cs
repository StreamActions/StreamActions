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

using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Twitch.EventSub.Channel.Moderate.Objects;

/// <summary>
/// Metadata associated with the automod terms changes.
/// </summary>
public sealed record AutomodTerms
{
    /// <summary>
    /// Either add or remove.
    /// </summary>
    [JsonPropertyName("action")]
    public string? Action { get; init; }

    /// <summary>
    /// Either blocked or permitted.
    /// </summary>
    [JsonPropertyName("list")]
    public string? List { get; init; }

    /// <summary>
    /// Terms being added or removed.
    /// </summary>
    [JsonPropertyName("terms")]
    public IReadOnlyCollection<string>? Terms { get; init; }

    /// <summary>
    /// Whether the terms were added due to an Automod message approve/deny action.
    /// </summary>
    [JsonPropertyName("from_automod")]
    public bool? FromAutomod { get; init; }
}

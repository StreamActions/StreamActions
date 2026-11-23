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

using StreamActions.Common.Json.Serialization;
using System.Text.Json.Serialization;

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
    public TermActions? Action { get; init; }

    /// <summary>
    /// Either blocked or permitted.
    /// </summary>
    [JsonPropertyName("list")]
    public TermLists? List { get; init; }

    /// <summary>
    /// Terms being added or removed.
    /// </summary>
    [JsonPropertyName("terms")]
    public IReadOnlyList<string>? Terms { get; init; }

    /// <summary>
    /// Whether the terms were added due to an Automod message approve/deny action.
    /// </summary>
    [JsonPropertyName("from_automod")]
    public bool? FromAutomod { get; init; }

    /// <summary>
    /// The action performed on the automod terms.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<TermActions>))]
    public enum TermActions
    {
        /// <summary>
        /// Terms were added.
        /// </summary>
        [JsonCustomEnum("add")]
        Add,

        /// <summary>
        /// Terms were removed.
        /// </summary>
        [JsonCustomEnum("remove")]
        Remove
    }

    /// <summary>
    /// The automod term list that was modified.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<TermLists>))]
    public enum TermLists
    {
        /// <summary>
        /// The blocked terms list.
        /// </summary>
        [JsonCustomEnum("blocked")]
        Blocked,

        /// <summary>
        /// The permitted terms list.
        /// </summary>
        [JsonCustomEnum("permitted")]
        Permitted
    }
}

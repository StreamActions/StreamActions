/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Json.Serialization;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// Represents a single Content Classification Label change.
/// </summary>
public sealed record ContentClassificationLabel
{
    /// <summary>
    /// ID of the label.
    /// </summary>
    [JsonPropertyName("id")]
    public LabelId? Id { get; init; }

    /// <summary>
    /// Whether the label should be enabled or disabled.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Known content classification label identifiers returned/accepted by the API.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<LabelId>))]
    public enum LabelId
    {
        /// <summary>
        /// Debated social issues and politics.
        /// </summary>
        [JsonCustomEnum("DebatedSocialIssuesAndPolitics")]
        DebatedSocialIssuesAndPolitics,

        /// <summary>
        /// Drugs, intoxication, or excessive tobacco use.
        /// </summary>
        [JsonCustomEnum("DrugsIntoxication")]
        DrugsIntoxication,

        /// <summary>
        /// Sexual themes.
        /// </summary>
        [JsonCustomEnum("SexualThemes")]
        SexualThemes,

        /// <summary>
        /// Violent or graphic content.
        /// </summary>
        [JsonCustomEnum("ViolentGraphic")]
        ViolentGraphic,

        /// <summary>
        /// Gambling related content.
        /// </summary>
        [JsonCustomEnum("Gambling")]
        Gambling,

        /// <summary>
        /// Significant profanity or vulgarity.
        /// </summary>
        [JsonCustomEnum("ProfanityVulgarity")]
        ProfanityVulgarity,

        /// <summary>
        /// Mature-rated games or mature content.
        /// </summary>
        [JsonCustomEnum("MatureGame")]
        MatureGame
    }
}

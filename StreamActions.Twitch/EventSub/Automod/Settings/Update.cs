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
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Automod.Settings;

/// <summary>
/// A notification is sent when a broadcasters automod settings are updated.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "automod.settings.update";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The user name of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The ID of the moderator who changed the channel settings.
    /// </summary>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The moderators login.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

    /// <summary>
    /// The moderators user name.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The Automod level for hostility involving name calling or insults.
    /// </summary>
    [JsonPropertyName("bullying")]
    public int? Bullying { get; init; }

    /// <summary>
    /// The default AutoMod level for the broadcaster. This field is null if the broadcaster has set one or more of the individual settings.
    /// </summary>
    [JsonPropertyName("overall_level")]
    public int? OverallLevel { get; init; }

    /// <summary>
    /// The Automod level for discrimination against disability.
    /// </summary>
    [JsonPropertyName("disability")]
    public int? Disability { get; init; }

    /// <summary>
    /// The Automod level for racial discrimination.
    /// </summary>
    [JsonPropertyName("race_ethnicity_or_religion")]
    public int? RaceEthnicityOrReligion { get; init; }

    /// <summary>
    /// The Automod level for discrimination against women.
    /// </summary>
    [JsonPropertyName("misogyny")]
    public int? Misogyny { get; init; }

    /// <summary>
    /// The AutoMod level for discrimination based on sexuality, sex, or gender.
    /// </summary>
    [JsonPropertyName("sexuality_sex_or_gender")]
    public int? SexualitySexOrGender { get; init; }

    /// <summary>
    /// The Automod level for hostility involving aggression.
    /// </summary>
    [JsonPropertyName("aggression")]
    public int? Aggression { get; init; }

    /// <summary>
    /// The Automod level for sexual content.
    /// </summary>
    [JsonPropertyName("sex_based_terms")]
    public int? SexBasedTerms { get; init; }

    /// <summary>
    /// The Automod level for profanity.
    /// </summary>
    [JsonPropertyName("swearing")]
    public int? Swearing { get; init; }
}

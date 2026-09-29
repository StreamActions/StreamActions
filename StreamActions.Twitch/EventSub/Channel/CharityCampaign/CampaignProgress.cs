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

namespace StreamActions.Twitch.EventSub.Channel.CharityCampaign;

/// <summary>
/// An event that is sent when progress is made towards the campaign's goal or when the broadcaster changes the fundraising goal.
/// </summary>
public sealed record CampaignProgress : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.charity_campaign.progress";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// An ID that identifies the charity campaign.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that's running the campaign.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The charity's name.
    /// </summary>
    [JsonPropertyName("charity_name")]
    public string? CharityName { get; init; }

    /// <summary>
    /// A description of the charity.
    /// </summary>
    [JsonPropertyName("charity_description")]
    public string? CharityDescription { get; init; }

    /// <summary>
    /// A URL to an image of the charity's logo. The image's type is PNG and its size is 100px X 100px.
    /// </summary>
    [JsonPropertyName("charity_logo")]
    public string? CharityLogo { get; init; }

    /// <summary>
    /// A URL to the charity's website.
    /// </summary>
    [JsonPropertyName("charity_website")]
    public string? CharityWebsite { get; init; }

    /// <summary>
    /// An object that contains the current amount of donations that the campaign has received.
    /// </summary>
    [JsonPropertyName("current_amount")]
    public Amount? CurrentAmount { get; init; }

    /// <summary>
    /// An object that contains the campaign's target fundraising goal.
    /// </summary>
    [JsonPropertyName("target_amount")]
    public Amount? TargetAmount { get; init; }
}

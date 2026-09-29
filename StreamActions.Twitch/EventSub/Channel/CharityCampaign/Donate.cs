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
/// An event that is sent when a user donates to a charity campaign.
/// </summary>
public sealed record Donate : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.charity_campaign.donate";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// An ID that identifies the donation. The ID is unique across campaigns.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the charity campaign.
    /// </summary>
    [JsonPropertyName("campaign_id")]
    public string? CampaignId { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that's running the campaign.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// An ID that identifies the user that donated to the campaign.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

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
    /// An object that contains the amount of money that the user donated.
    /// </summary>
    [JsonPropertyName("amount")]
    public Amount? Amount { get; init; }
}

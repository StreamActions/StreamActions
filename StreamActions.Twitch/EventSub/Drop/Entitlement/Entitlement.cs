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

namespace StreamActions.Twitch.EventSub.Drop.Entitlement;

/// <summary>
/// A drop entitlement object.
/// </summary>
public sealed record Entitlement
{
    /// <summary>
    /// The ID of the organization that owns the game that has Drops enabled.
    /// </summary>
    [JsonPropertyName("organization_id")]
    public string? OrganizationId { get; init; }

    /// <summary>
    /// Twitch category ID of the game that was being played when this benefit was entitled.
    /// </summary>
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    /// <summary>
    /// The category name.
    /// </summary>
    [JsonPropertyName("category_name")]
    public string? CategoryName { get; init; }

    /// <summary>
    /// The campaign this entitlement is associated with.
    /// </summary>
    [JsonPropertyName("campaign_id")]
    public string? CampaignId { get; init; }

    /// <summary>
    /// Twitch user ID of the user who was granted the entitlement.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user display name of the user who was granted the entitlement.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The user login of the user who was granted the entitlement.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// Unique identifier of the entitlement.
    /// </summary>
    [JsonPropertyName("entitlement_id")]
    public string? EntitlementId { get; init; }

    /// <summary>
    /// Identifier of the Benefit.
    /// </summary>
    [JsonPropertyName("benefit_id")]
    public string? BenefitId { get; init; }

    /// <summary>
    /// UTC timestamp in ISO format when this entitlement was granted on Twitch.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }
}

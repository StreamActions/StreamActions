/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.EventSub.Conditions;

/// <summary>
/// An <see cref="EventSubCondition{T}"/> for receiving notifications about a drop entitlement grant.
/// </summary>
public sealed record DropEntitlementGrantCondition : EventSubCondition<DropEntitlementGrantCondition>
{
    /// <summary>
    /// The ID of the organization that owns the game on the developer portal.
    /// </summary>
    [JsonPropertyName("organization_id")]
    public string? OrganizationId { get; init; }

    /// <summary>
    /// The ID of the category/game to receive notifications for.
    /// </summary>
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    /// <summary>
    /// The ID of the campaign to receive notifications for.
    /// </summary>
    [JsonPropertyName("campaign_id")]
    public Guid? CampaignId { get; init; }
}

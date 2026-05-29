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

namespace StreamActions.Twitch.Api.Entitlements;

/// <summary>
/// Represents the status of an update Drops entitlement request for a list of entitlement IDs.
/// </summary>
public sealed record UpdateDropsEntitlementsStatus
{
    /// <summary>
    /// A string that indicates whether the status of the entitlements in the <see cref="Ids"/> field were successfully updated.
    /// </summary>
    [JsonPropertyName("status")]
    public Statuses? Status { get; init; }

    /// <summary>
    /// The list of entitlements that the status in the <see cref="Status"/> field applies to.
    /// </summary>
    [JsonPropertyName("ids")]
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>
    /// The status of an update Drops entitlement request.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<Statuses>))]
    public enum Statuses
    {
        /// <summary>
        /// The entitlement IDs in the ids field are not valid.
        /// </summary>
        [JsonCustomEnum("INVALID_ID")]
        InvalidId,
        /// <summary>
        /// The entitlement IDs in the ids field were not found.
        /// </summary>
        [JsonCustomEnum("NOT_FOUND")]
        NotFound,
        /// <summary>
        /// The status of the entitlements in the ids field were successfully updated.
        /// </summary>
        [JsonCustomEnum("SUCCESS")]
        Success,
        /// <summary>
        /// The user or organization identified by the user access token is not authorized to update the entitlements.
        /// </summary>
        [JsonCustomEnum("UNAUTHORIZED")]
        Unauthorized,
        /// <summary>
        /// The update failed. These are considered transient errors and the request should be retried later.
        /// </summary>
        [JsonCustomEnum("UPDATE_FAILED")]
        UpdateFailed
    }
}

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

namespace StreamActions.Twitch.Api.ChannelPoints;

/// <summary>
/// The parameters for <see cref="ChannelPointsRedemption.UpdateRedemptionStatus(Common.TwitchSession, IEnumerable{Guid}, string, Guid, ChannelPointsRedemptionUpdateParameters)"/>.
/// </summary>
public sealed record ChannelPointsRedemptionUpdateParameters
{
    /// <summary>
    /// The status to set the redemption to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting the status to <see cref="ChannelPointsRedemption.RedemptionStatus.Canceled"/> refunds the user's channel points.
    /// </para>
    /// </remarks>
    [JsonPropertyName("status")]
    public ChannelPointsRedemption.RedemptionStatus? Status { get; init; }
}

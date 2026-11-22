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

namespace StreamActions.Core.Models;

/// <summary>
/// Represents channel point custom reward redemption metadata attached to a template evaluation context.
/// </summary>
/// <param name="RedemptionId">The unique platform redemption identifier.</param>
/// <param name="RewardId">The platform custom reward identifier.</param>
/// <param name="RewardTitle">The display title of the redeemed reward.</param>
/// <param name="Cost">The channel point cost of the redeemed reward.</param>
/// <param name="RedeemerUserId">The immutable platform user identifier of the redeemer.</param>
/// <param name="RedeemerLogin">The login username of the redeemer.</param>
/// <param name="RedeemerDisplayName">The display name of the redeemer.</param>
/// <param name="Status">The current redemption fulfillment status (for example, <c>UNFULFILLED</c>, <c>FULFILLED</c>, or <c>CANCELED</c>).</param>
/// <param name="RewardPrompt">The optional prompt description configured on the reward.</param>
/// <param name="UserInput">The optional viewer text input submitted with the redemption.</param>
public sealed record ChannelPointRedemptionContext(
    string RedemptionId,
    string RewardId,
    string RewardTitle,
    long Cost,
    string RedeemerUserId,
    string RedeemerLogin,
    string RedeemerDisplayName,
    string Status = "UNFULFILLED",
    string? RewardPrompt = null,
    string? UserInput = null);

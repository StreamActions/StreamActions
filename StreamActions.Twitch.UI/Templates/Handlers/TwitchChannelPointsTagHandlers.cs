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

using StreamActions.Common.Exceptions;
using StreamActions.Core.Models;
using StreamActions.Core.Templates;
using StreamActions.Twitch.Api.ChannelPoints;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.UI.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace StreamActions.Twitch.UI.Templates.Handlers;

/// <summary>
/// Shared helper methods for Twitch channel point redemption status updates.
/// </summary>
internal static class TwitchChannelPointsTagSupport
{
    #region Internal Methods

    /// <summary>
    /// Executes <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> for the active <see cref="ChannelPointRedemptionContext"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="targetStatus">The target redemption status (<see cref="ChannelPointsRedemption.RedemptionStatus.Fulfilled"/> or <see cref="ChannelPointsRedemption.RedemptionStatus.Canceled"/>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see cref="TagResult.Complete"/> with an empty string on success, or <see cref="TagResult.Incomplete"/> when missing context, invalid identifiers, or API failure.</returns>
    internal static async ValueTask<TagResult> UpdateRedemptionStatusAsync(
        ITagEvaluationContext context,
        ChannelPointsRedemption.RedemptionStatus targetStatus,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ChannelPointRedemptionContext? redemption = context.ChannelPointRedemption;
        if (redemption is null
            || !Guid.TryParse(redemption.RedemptionId, out Guid redemptionId)
            || redemptionId == Guid.Empty
            || !Guid.TryParse(redemption.RewardId, out Guid rewardId)
            || rewardId == Guid.Empty)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null || string.IsNullOrWhiteSpace(lease.BroadcasterId))
        {
            return TagResult.Incomplete;
        }

        try
        {
            ChannelPointsRedemptionUpdateParameters parameters = new()
            {
                Status = targetStatus,
            };

            ResponseData<ChannelPointsRedemption>? response = await ChannelPointsRedemption
                .UpdateRedemptionStatus(
                    lease.TwitchSession,
                    [redemptionId],
                    lease.BroadcasterId,
                    rewardId,
                    parameters)
                .ConfigureAwait(false);

            return response is not null && response.IsSuccessStatusCode
                ? TagResult.Complete(string.Empty)
                : TagResult.Incomplete;
        }
        catch (Exception ex) when (ex is TwitchScopeMissingException or TokenTypeException or InvalidOperationException or JsonException or HttpRequestException or ArgumentException)
        {
            return TagResult.Incomplete;
        }
    }

    #endregion Internal Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_title}</c> tag returning the redeemed custom reward title.
/// </summary>
public sealed class CpTitleTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_title";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.RewardTitle));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_prompt}</c> tag returning the redeemed custom reward prompt description.
/// </summary>
public sealed class CpPromptTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_prompt";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.RewardPrompt));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_cost}</c> tag returning the redeemed custom reward channel point cost formatted with <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class CpCostTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_cost";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(context.ChannelPointRedemption is ChannelPointRedemptionContext redemption
            ? TagResult.Complete(redemption.Cost.ToString(CultureInfo.InvariantCulture))
            : TagResult.Incomplete);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_input}</c> tag returning the viewer's literal text input without re-parsing template syntax.
/// </summary>
public sealed class CpInputTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_input";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.UserInput));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_user}</c> tag returning the redeemer's display name (falling back to login).
/// </summary>
public sealed class CpUserTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_user";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ChannelPointRedemptionContext? redemption = context.ChannelPointRedemption;
        string? name = !string.IsNullOrWhiteSpace(redemption?.RedeemerDisplayName)
            ? redemption.RedeemerDisplayName
            : redemption?.RedeemerLogin;

        return ValueTask.FromResult(TagResult.FromNonEmpty(name));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_username}</c> tag returning the redeemer's login name.
/// </summary>
public sealed class CpUsernameTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_username";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.RedeemerLogin));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_user_id}</c> tag returning the redeemer's Twitch user ID.
/// </summary>
public sealed class CpUserIdTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_user_id";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.RedeemerUserId));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_redemption_id}</c> tag returning the unique redemption identifier.
/// </summary>
public sealed class CpRedemptionIdTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_redemption_id";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.RedemptionId));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_reward_id}</c> tag returning the custom reward identifier.
/// </summary>
public sealed class CpRewardIdTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_reward_id";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.RewardId));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_status}</c> tag returning the current redemption fulfillment status.
/// </summary>
public sealed class CpStatusTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_status";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelPointRedemption?.Status));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_fulfill}</c> tag, marking the active redemption as <see cref="ChannelPointsRedemption.RedemptionStatus.Fulfilled"/> via Helix.
/// </summary>
public sealed class CpFulfillTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_fulfill";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        return TwitchChannelPointsTagSupport.UpdateRedemptionStatusAsync(
            context,
            ChannelPointsRedemption.RedemptionStatus.Fulfilled,
            cancellationToken);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{cp_cancel}</c> tag, marking the active redemption as <see cref="ChannelPointsRedemption.RedemptionStatus.Canceled"/> (refunding points) via Helix.
/// </summary>
public sealed class CpCancelTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "cp_cancel";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        return TwitchChannelPointsTagSupport.UpdateRedemptionStatusAsync(
            context,
            ChannelPointsRedemption.RedemptionStatus.Canceled,
            cancellationToken);
    }

    #endregion Public Methods
}

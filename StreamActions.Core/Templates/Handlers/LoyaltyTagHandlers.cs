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

using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Shared helper methods for loyalty, rank, user lookup, and command economy tag handlers.
/// </summary>
internal static class LoyaltyTagHelper
{
    /// <summary>
    /// Evaluates an argument AST node as a trimmed username stripped of any leading <c>@</c> character.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="node">The argument AST node.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cleaned user handle, or <see langword="null"/> if incomplete or blank.</returns>
    internal static async ValueTask<string?> EvaluateCleanedUserAsync(
        ITagEvaluationContext context,
        TemplateNode node,
        CancellationToken cancellationToken)
    {
        TagResult result = await node.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!result.IsComplete)
        {
            return null;
        }

        string cleaned = result.Value.Trim().TrimStart('@');
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    /// <summary>
    /// Evaluates an argument AST node as a strictly positive <see cref="long"/> loyalty point amount.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="node">The argument AST node.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The positive amount, or <see langword="null"/> if incomplete, non-numeric, or non-positive.</returns>
    internal static async ValueTask<long?> EvaluatePositiveAmountAsync(
        ITagEvaluationContext context,
        TemplateNode node,
        CancellationToken cancellationToken)
    {
        TagResult result = await node.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return !result.IsComplete
            || !long.TryParse(result.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long amount)
            || amount <= 0L
            ? null
            : amount;
    }

    /// <summary>
    /// Resolves a target user's <see cref="WorkspaceViewerProfile"/> by login handle first, then by platform user identifier.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="store">The viewer loyalty store.</param>
    /// <param name="cleanedUser">The cleaned login handle or platform user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="WorkspaceViewerProfile"/>, or <see langword="null"/> if none exists.</returns>
    internal static async ValueTask<WorkspaceViewerProfile?> FindProfileAsync(
        ITagEvaluationContext context,
        IViewerLoyaltyStore store,
        string cleanedUser,
        CancellationToken cancellationToken)
    {
        WorkspaceViewerProfile? byLogin = await store.GetProfileByLoginAsync(
            context.WorkspaceId,
            context.PlatformId,
            cleanedUser,
            cancellationToken).ConfigureAwait(false);
        if (byLogin is not null)
        {
            return byLogin;
        }

        WorkspaceViewerProfile? byId = await store.GetProfileAsync(
            context.WorkspaceId,
            context.PlatformId,
            cleanedUser,
            cancellationToken).ConfigureAwait(false);
        return byId is not null
            ? byId
            : MatchesSender(context.Message.Sender, cleanedUser)
            && !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
            ? await store.GetProfileAsync(
                context.WorkspaceId,
                context.PlatformId,
                context.Message.Sender.PlatformUserId,
                cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Resolves a target user's canonical platform user identifier for balance mutations.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="store">The viewer loyalty store.</param>
    /// <param name="cleanedUser">The cleaned login handle or platform user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved platform user identifier.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "ViewerLoyaltyStore normalizes platform user identifiers to lowercase invariant.")]
    internal static async ValueTask<string> ResolveTargetUserIdAsync(
        ITagEvaluationContext context,
        IViewerLoyaltyStore store,
        string cleanedUser,
        CancellationToken cancellationToken)
    {
        WorkspaceViewerProfile? existing = await FindProfileAsync(context, store, cleanedUser, cancellationToken).ConfigureAwait(false);
        return existing is not null && !string.IsNullOrWhiteSpace(existing.PlatformUserId)
            ? existing.PlatformUserId
            : MatchesSender(context.Message.Sender, cleanedUser) && !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
            ? context.Message.Sender.PlatformUserId
            : cleanedUser.ToLowerInvariant();
    }

    /// <summary>
    /// Evaluates either <c>{price [cmd]}</c> or <c>{pay [cmd]}</c> against <see cref="ICommandRepository"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated tag arguments.</param>
    /// <param name="readReward"><see langword="true"/> to read <see cref="CustomCommand.RewardPoints"/>; <see langword="false"/> to read <see cref="CustomCommand.CostPoints"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The formatted points value, or <see cref="TagResult.Incomplete"/> when unavailable.</returns>
    internal static async ValueTask<TagResult> EvaluateCommandPointsPropertyAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        bool readReward,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ICommandRepository? repository = context.GetService<ICommandRepository>();
        if (repository is null)
        {
            return TagResult.Incomplete;
        }

        string trigger;
        if (unevaluatedArgs.Count >= 1)
        {
            TagResult arg0 = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!arg0.IsComplete)
            {
                return TagResult.Incomplete;
            }

            string candidate = arg0.Value.Trim().TrimStart('!');
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return TagResult.Incomplete;
            }

            trigger = candidate;
        }
        else
        {
            if (context.Message is not ICommandInvocation invocation || string.IsNullOrWhiteSpace(invocation.Trigger))
            {
                return TagResult.Incomplete;
            }

            trigger = invocation.Trigger;
        }

        CustomCommand? command = await repository.GetByTriggerAsync(context.WorkspaceId, trigger, cancellationToken).ConfigureAwait(false);
        if (command is null)
        {
            return TagResult.Incomplete;
        }

        long value = readReward ? command.RewardPoints : command.CostPoints;
        return TagResult.Complete(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Determines whether <paramref name="cleanedUser"/> matches the invoking message sender.
    /// </summary>
    /// <param name="sender">The invoking chat user.</param>
    /// <param name="cleanedUser">The cleaned user identifier or handle.</param>
    /// <returns><see langword="true"/> if <paramref name="cleanedUser"/> matches the sender; otherwise, <see langword="false"/>.</returns>
    private static bool MatchesSender(IChatUser sender, string cleanedUser) =>
        string.Equals(cleanedUser, sender.PlatformUserId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(cleanedUser, sender.Username, StringComparison.OrdinalIgnoreCase)
        || string.Equals(cleanedUser, sender.DisplayName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Handles the <c>{sender_rank}</c> tag, returning the invoking user's loyalty rank title combined with their display name.
/// </summary>
public sealed class SenderRankTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "sender_rank";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ILoyaltyRankProvider? rankProvider = context.GetService<ILoyaltyRankProvider>();
        if (rankProvider is null)
        {
            return TagResult.Incomplete;
        }

        string lookupKey = !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
            ? context.Message.Sender.PlatformUserId
            : context.Message.Sender.Username;

        if (string.IsNullOrWhiteSpace(lookupKey))
        {
            return TagResult.Incomplete;
        }

        string displayName = !string.IsNullOrWhiteSpace(context.Message.Sender.DisplayName)
            ? context.Message.Sender.DisplayName.Trim()
            : context.Message.Sender.Username.Trim();

        string? rankTitle = await rankProvider.GetRankTitleAsync(
            context.WorkspaceId,
            context.PlatformId,
            lookupKey,
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(rankTitle)
            ? TagResult.FromNonEmpty(displayName)
            : string.IsNullOrWhiteSpace(displayName)
            ? TagResult.Complete(rankTitle.Trim())
            : TagResult.Complete($"{rankTitle.Trim()} {displayName}");
    }
}

/// <summary>
/// Handles the <c>{rank [user]}</c> tag, returning only the loyalty rank title for the specified user or sender.
/// </summary>
public sealed class RankTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "rank";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ILoyaltyRankProvider? rankProvider = context.GetService<ILoyaltyRankProvider>();
        if (rankProvider is null)
        {
            return TagResult.Incomplete;
        }

        string? target = unevaluatedArgs.Count >= 1
            ? await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false)
            : !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
                ? context.Message.Sender.PlatformUserId
                : context.Message.Sender.Username;
        if (string.IsNullOrWhiteSpace(target))
        {
            return TagResult.Incomplete;
        }

        string? rankTitle = await rankProvider.GetRankTitleAsync(
            context.WorkspaceId,
            context.PlatformId,
            target.Trim(),
            cancellationToken).ConfigureAwait(false);

        return TagResult.FromNonEmpty(rankTitle?.Trim());
    }
}

/// <summary>
/// Handles the <c>{account_age [user]}</c> tag using <see cref="IPlatformUserLookup"/> and <see cref="ITagEvaluationContext.TimeProvider"/>.
/// </summary>
public sealed class AccountAgeTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "account_age";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        IPlatformUserLookup? userLookup = context.GetService<IPlatformUserLookup>();
        if (userLookup is null)
        {
            return TagResult.Incomplete;
        }

        string? target = unevaluatedArgs.Count >= 1
            ? await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false)
            : !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
                ? context.Message.Sender.PlatformUserId
                : context.Message.Sender.Username;
        if (string.IsNullOrWhiteSpace(target))
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset? createdAt = await userLookup.GetAccountCreatedAtAsync(
            context.WorkspaceId,
            context.PlatformId,
            target.Trim(),
            cancellationToken).ConfigureAwait(false);
        if (createdAt is null)
        {
            return TagResult.Incomplete;
        }

        TimeSpan age = context.TimeProvider.GetUtcNow() - createdAt.Value;
        string createdDate = createdAt.Value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string formattedDuration = TimeTagFormatting.FormatDuration(age);
        return TagResult.Complete($"{createdDate} ({formattedDuration})");
    }
}

/// <summary>
/// Handles the <c>{random_chatter}</c> tag, selecting a random active chatter from <see cref="IChannelChatterProvider"/>.
/// </summary>
public sealed class RandomChatterTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "random_chatter";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        IChannelChatterProvider? chatterProvider = context.GetService<IChannelChatterProvider>();
        if (chatterProvider is null)
        {
            return TagResult.Incomplete;
        }

        IReadOnlyList<string> chatters = await chatterProvider.GetActiveChattersAsync(
            context.WorkspaceId,
            context.PlatformId,
            excludeLurking: true,
            cancellationToken).ConfigureAwait(false);
        if (chatters.Count == 0)
        {
            return TagResult.Incomplete;
        }

        string selected = chatters[RandomNumberGenerator.GetInt32(chatters.Count)];
        return TagResult.FromNonEmpty(selected);
    }
}

/// <summary>
/// Handles the <c>{last_tip}</c> tag using <see cref="ITipProvider"/>.
/// </summary>
public sealed class LastTipTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "last_tip";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ITipProvider? tipProvider = context.GetService<ITipProvider>();
        if (tipProvider is null)
        {
            return TagResult.Incomplete;
        }

        string? summary = await tipProvider.GetLastTipSummaryAsync(context.WorkspaceId, cancellationToken).ConfigureAwait(false);
        return TagResult.FromNonEmpty(summary);
    }
}

/// <summary>
/// Handles the <c>{points [user]}</c> tag, returning the viewer's loyalty point balance formatted with <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class PointsTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "points";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        IViewerLoyaltyStore? store = context.GetService<IViewerLoyaltyStore>();
        if (store is null)
        {
            return TagResult.Incomplete;
        }

        WorkspaceViewerProfile? profile;
        if (unevaluatedArgs.Count >= 1)
        {
            string? target = await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false);
            if (target is null)
            {
                return TagResult.Incomplete;
            }

            profile = await LoyaltyTagHelper.FindProfileAsync(context, store, target, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
            {
                return TagResult.Incomplete;
            }

            profile = await store.GetProfileAsync(
                context.WorkspaceId,
                context.PlatformId,
                context.Message.Sender.PlatformUserId,
                cancellationToken).ConfigureAwait(false);
        }

        long balance = profile?.Points ?? 0L;
        return TagResult.Complete(balance.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Handles the <c>{pointname [amount]}</c> tag, returning the configured singular or plural loyalty currency name.
/// </summary>
public sealed class PointNameTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Default singular currency name used when no workspace setting is registered.
    /// </summary>
    public const string DefaultSingularName = "point";

    /// <summary>
    /// Default plural currency name used when no workspace setting is registered.
    /// </summary>
    public const string DefaultPluralName = "points";

    /// <inheritdoc />
    public string TagName => "pointname";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        string singular = DefaultSingularName;
        string plural = DefaultPluralName;

        ILoyaltySettingsProvider? settingsProvider = context.GetService<ILoyaltySettingsProvider>();
        if (settingsProvider is not null)
        {
            (string configuredSingular, string configuredPlural) = await settingsProvider
                .GetCurrencyNameAsync(context.WorkspaceId, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(configuredSingular))
            {
                singular = configuredSingular.Trim();
            }

            if (!string.IsNullOrWhiteSpace(configuredPlural))
            {
                plural = configuredPlural.Trim();
            }
        }

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Complete(plural);
        }

        TagResult amountResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return !amountResult.IsComplete
            || !decimal.TryParse(amountResult.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal amount)
            ? TagResult.Incomplete
            : TagResult.Complete(amount is 1m or -1m ? singular : plural);
    }
}

/// <summary>
/// Handles the <c>{price [cmd]}</c> tag, returning the loyalty point cost configured on a command.
/// </summary>
public sealed class PriceTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "price";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken) =>
        LoyaltyTagHelper.EvaluateCommandPointsPropertyAsync(context, unevaluatedArgs, readReward: false, cancellationToken);
}

/// <summary>
/// Handles the <c>{pay [cmd]}</c> tag, returning the loyalty point reward configured on a command.
/// </summary>
public sealed class PayTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "pay";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken) =>
        LoyaltyTagHelper.EvaluateCommandPointsPropertyAsync(context, unevaluatedArgs, readReward: true, cancellationToken);
}

/// <summary>
/// Handles the <c>{points_add &lt;amt&gt; [user]}</c> tag, crediting loyalty points to a viewer.
/// </summary>
public sealed class PointsAddTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "points_add";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        IViewerLoyaltyStore? store = context.GetService<IViewerLoyaltyStore>();
        if (store is null)
        {
            return TagResult.Incomplete;
        }

        long? amount = await LoyaltyTagHelper.EvaluatePositiveAmountAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false);
        if (amount is null)
        {
            return TagResult.Incomplete;
        }

        string targetUserId;
        if (unevaluatedArgs.Count >= 2)
        {
            string? cleanedTarget = await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[1], cancellationToken).ConfigureAwait(false);
            if (cleanedTarget is null)
            {
                return TagResult.Incomplete;
            }

            targetUserId = await LoyaltyTagHelper.ResolveTargetUserIdAsync(context, store, cleanedTarget, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
            {
                return TagResult.Incomplete;
            }

            targetUserId = context.Message.Sender.PlatformUserId;
        }

        _ = await store.AdjustPointsAsync(context.WorkspaceId, context.PlatformId, targetUserId, amount.Value, cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{points_take &lt;amt&gt; [user]}</c> tag, deducting loyalty points from a viewer.
/// </summary>
public sealed class PointsTakeTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "points_take";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        IViewerLoyaltyStore? store = context.GetService<IViewerLoyaltyStore>();
        if (store is null)
        {
            return TagResult.Incomplete;
        }

        long? amount = await LoyaltyTagHelper.EvaluatePositiveAmountAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false);
        if (amount is null)
        {
            return TagResult.Incomplete;
        }

        string targetUserId;
        if (unevaluatedArgs.Count >= 2)
        {
            string? cleanedTarget = await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[1], cancellationToken).ConfigureAwait(false);
            if (cleanedTarget is null)
            {
                return TagResult.Incomplete;
            }

            targetUserId = await LoyaltyTagHelper.ResolveTargetUserIdAsync(context, store, cleanedTarget, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
            {
                return TagResult.Incomplete;
            }

            targetUserId = context.Message.Sender.PlatformUserId;
        }

        _ = await store.AdjustPointsAsync(context.WorkspaceId, context.PlatformId, targetUserId, -amount.Value, cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{points_take_or_cancel &lt;amt&gt; [user]}</c> tag, atomically deducting points or canceling execution if the balance is insufficient.
/// </summary>
public sealed class PointsTakeOrCancelTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "points_take_or_cancel";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        IViewerLoyaltyStore? store = context.GetService<IViewerLoyaltyStore>();
        if (store is null)
        {
            return TagResult.Incomplete;
        }

        long? amount = await LoyaltyTagHelper.EvaluatePositiveAmountAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false);
        if (amount is null)
        {
            return TagResult.Incomplete;
        }

        string targetUserId;
        if (unevaluatedArgs.Count >= 2)
        {
            string? cleanedTarget = await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[1], cancellationToken).ConfigureAwait(false);
            if (cleanedTarget is null)
            {
                return TagResult.Incomplete;
            }

            targetUserId = await LoyaltyTagHelper.ResolveTargetUserIdAsync(context, store, cleanedTarget, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
            {
                return TagResult.Incomplete;
            }

            targetUserId = context.Message.Sender.PlatformUserId;
        }

        bool spent = await store.TrySpendPointsAsync(context.WorkspaceId, context.PlatformId, targetUserId, amount.Value, cancellationToken).ConfigureAwait(false);
        if (!spent)
        {
            context.Cancel("Insufficient loyalty points.");
            return TagResult.Incomplete;
        }

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{points_transfer &lt;amt&gt; &lt;to&gt; [from]}</c> tag, atomically spending from the source viewer before crediting the recipient.
/// </summary>
public sealed class PointsTransferTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "points_transfer";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count < 2)
        {
            return TagResult.Incomplete;
        }

        IViewerLoyaltyStore? store = context.GetService<IViewerLoyaltyStore>();
        if (store is null)
        {
            return TagResult.Incomplete;
        }

        long? amount = await LoyaltyTagHelper.EvaluatePositiveAmountAsync(context, unevaluatedArgs[0], cancellationToken).ConfigureAwait(false);
        if (amount is null)
        {
            return TagResult.Incomplete;
        }

        string? cleanedTo = await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[1], cancellationToken).ConfigureAwait(false);
        if (cleanedTo is null)
        {
            return TagResult.Incomplete;
        }

        string fromUserId;
        if (unevaluatedArgs.Count >= 3)
        {
            string? cleanedFrom = await LoyaltyTagHelper.EvaluateCleanedUserAsync(context, unevaluatedArgs[2], cancellationToken).ConfigureAwait(false);
            if (cleanedFrom is null)
            {
                return TagResult.Incomplete;
            }

            fromUserId = await LoyaltyTagHelper.ResolveTargetUserIdAsync(context, store, cleanedFrom, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
            {
                return TagResult.Incomplete;
            }

            fromUserId = context.Message.Sender.PlatformUserId;
        }

        string toUserId = await LoyaltyTagHelper.ResolveTargetUserIdAsync(context, store, cleanedTo, cancellationToken).ConfigureAwait(false);

        bool spent = await store.TrySpendPointsAsync(context.WorkspaceId, context.PlatformId, fromUserId, amount.Value, cancellationToken).ConfigureAwait(false);
        if (!spent)
        {
            return TagResult.Incomplete;
        }

        _ = await store.AdjustPointsAsync(context.WorkspaceId, context.PlatformId, toUserId, amount.Value, cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(string.Empty);
    }
}

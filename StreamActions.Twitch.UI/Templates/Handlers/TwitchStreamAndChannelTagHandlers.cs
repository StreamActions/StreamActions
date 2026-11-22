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
using StreamActions.Core.Templates;
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.Api.Subscriptions;
using StreamActions.Twitch.Api.Users;
using StreamActions.Twitch.UI.Abstractions;
using System.Globalization;

namespace StreamActions.Twitch.UI.Templates.Handlers;

/// <summary>
/// Shared helper methods for Twitch followage, followdate, and loyalty watch-hours tag handlers.
/// </summary>
internal static class TwitchChannelTagSupport
{
    #region Internal Methods

    /// <summary>
    /// Resolves the memoized <see cref="ChannelFollower"/> record for <c>{followage}</c> and <c>{followdate}</c>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated tag arguments (<c>[user] [channel]</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="ChannelFollower"/>, or <see langword="null"/> if not following or unavailable.</returns>
    internal static async ValueTask<ChannelFollower?> ResolveChannelFollowerAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        (bool userValid, string? explicitUser) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!userValid)
        {
            return null;
        }

        (bool channelValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 1, cancellationToken)
            .ConfigureAwait(false);
        if (!channelValid)
        {
            return null;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return null;
        }

        string? broadcasterId = await TwitchHelixTagHelper
            .ResolveBroadcasterIdAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            return null;
        }

        string? targetUserId = await ResolveTargetUserIdAsync(context, lease.TwitchSession, explicitUser, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return null;
        }

        MemoizedLookup<ChannelFollower> followLookup = await TwitchHelixTagHelper
            .GetChannelFollowerAsync(context, lease.TwitchSession, broadcasterId, targetUserId, cancellationToken)
            .ConfigureAwait(false);

        return followLookup.IsSuccess ? followLookup.Value : null;
    }

    /// <summary>
    /// Resolves a Twitch user ID from an optional explicit handle or the invoking message sender.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="explicitUser">Optional cleaned login handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved Twitch user ID, or <see langword="null"/> if unavailable.</returns>
    internal static async ValueTask<string?> ResolveTargetUserIdAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string? explicitUser,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitUser))
        {
            MemoizedLookup<User> lookup = await TwitchHelixTagHelper
                .GetUserByLoginAsync(context, session, explicitUser, cancellationToken)
                .ConfigureAwait(false);
            return lookup.Value?.Id;
        }

        if (!string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
        {
            return context.Message.Sender.PlatformUserId.Trim();
        }

        string senderLogin = TwitchHelixTagHelper.CleanHandle(context.Message.Sender.Username);
        if (string.IsNullOrEmpty(senderLogin))
        {
            return null;
        }

        MemoizedLookup<User> senderLookup = await TwitchHelixTagHelper
            .GetUserByLoginAsync(context, session, senderLogin, cancellationToken)
            .ConfigureAwait(false);
        return senderLookup.Value?.Id;
    }

    #endregion Internal Methods
}

/// <summary>
/// Handles the Twitch <c>{game [channel]}</c> tag with per-message memoization over <see cref="TwitchStream.GetStreams"/> and <see cref="Channel.GetChannelInformation"/> fallback.
/// </summary>
public sealed class TwitchGameTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "game";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<TwitchStream> streamLookup = await TwitchHelixTagHelper
            .GetStreamAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (streamLookup.Value is not null && !string.IsNullOrWhiteSpace(streamLookup.Value.GameName))
        {
            return TagResult.Complete(streamLookup.Value.GameName);
        }

        string? broadcasterId = await TwitchHelixTagHelper
            .ResolveBroadcasterIdAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<Channel> channelLookup = await TwitchHelixTagHelper
            .GetChannelInformationAsync(context, lease.TwitchSession, broadcasterId, cancellationToken)
            .ConfigureAwait(false);

        return TagResult.FromNonEmpty(channelLookup.Value?.GameName);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{title [channel]}</c> tag with per-message memoization over <see cref="TwitchStream.GetStreams"/> and <see cref="Channel.GetChannelInformation"/> fallback.
/// </summary>
public sealed class TwitchTitleTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "title";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<TwitchStream> streamLookup = await TwitchHelixTagHelper
            .GetStreamAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (streamLookup.Value is not null && !string.IsNullOrWhiteSpace(streamLookup.Value.Title))
        {
            return TagResult.Complete(streamLookup.Value.Title);
        }

        string? broadcasterId = await TwitchHelixTagHelper
            .ResolveBroadcasterIdAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<Channel> channelLookup = await TwitchHelixTagHelper
            .GetChannelInformationAsync(context, lease.TwitchSession, broadcasterId, cancellationToken)
            .ConfigureAwait(false);

        return TagResult.FromNonEmpty(channelLookup.Value?.Title);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{uptime [channel]}</c> tag using memoized <see cref="TwitchStream.GetStreams"/> and <see cref="ITagEvaluationContext.TimeProvider"/>.
/// </summary>
public sealed class TwitchUptimeTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "uptime";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<TwitchStream> streamLookup = await TwitchHelixTagHelper
            .GetStreamAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (!streamLookup.IsSuccess)
        {
            return TagResult.Incomplete;
        }

        if (streamLookup.Value is null || !streamLookup.Value.StartedAt.HasValue)
        {
            return TagResult.Complete("Offline");
        }

        DateTimeOffset startedAtUtc = TwitchHelixTagHelper.ToUtcDateTimeOffset(streamLookup.Value.StartedAt.Value);
        TimeSpan elapsed = context.TimeProvider.GetUtcNow() - startedAtUtc;
        return TagResult.Complete(TwitchHelixTagHelper.FormatDuration(elapsed));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{downtime [channel]}</c> tag using memoized <see cref="TwitchStream.GetStreams"/> and <see cref="ITwitchStreamHistoryProvider"/>.
/// </summary>
public sealed class TwitchDowntimeTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "downtime";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is not null)
        {
            MemoizedLookup<TwitchStream> streamLookup = await TwitchHelixTagHelper
                .GetStreamAsync(context, lease, explicitChannel, cancellationToken)
                .ConfigureAwait(false);
            if (streamLookup.IsSuccess && streamLookup.Value is not null)
            {
                return TagResult.Incomplete;
            }
        }

        ITwitchStreamHistoryProvider? historyProvider = context.GetService<ITwitchStreamHistoryProvider>();
        if (historyProvider is null)
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset? lastOfflineAtUtc = await historyProvider
            .GetLastOfflineAtUtcAsync(context.WorkspaceId, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (!lastOfflineAtUtc.HasValue)
        {
            return TagResult.Incomplete;
        }

        TimeSpan elapsed = context.TimeProvider.GetUtcNow() - lastOfflineAtUtc.Value;
        return TagResult.Complete(TwitchHelixTagHelper.FormatDuration(elapsed));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{playtime}</c> tag using <see cref="ITwitchStreamHistoryProvider"/> and <see cref="ITagEvaluationContext.TimeProvider"/>.
/// </summary>
public sealed class TwitchPlaytimeTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "playtime";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        ITwitchStreamHistoryProvider? historyProvider = context.GetService<ITwitchStreamHistoryProvider>();
        if (historyProvider is null)
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset? startedAtUtc = await historyProvider
            .GetCurrentGameStartedAtUtcAsync(context.WorkspaceId, cancellationToken)
            .ConfigureAwait(false);
        if (!startedAtUtc.HasValue)
        {
            return TagResult.Incomplete;
        }

        TimeSpan elapsed = context.TimeProvider.GetUtcNow() - startedAtUtc.Value;
        return TagResult.Complete(TwitchHelixTagHelper.FormatDuration(elapsed));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{games_played}</c> tag using <see cref="ITwitchStreamHistoryProvider"/>.
/// </summary>
public sealed class TwitchGamesPlayedTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "games_played";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        ITwitchStreamHistoryProvider? historyProvider = context.GetService<ITwitchStreamHistoryProvider>();
        if (historyProvider is null)
        {
            return TagResult.Incomplete;
        }

        IReadOnlyList<TwitchGameHistoryEntry> entries = await historyProvider
            .GetGamesPlayedHistoryAsync(context.WorkspaceId, cancellationToken)
            .ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return TagResult.Incomplete;
        }

        string[] formatted =
        [
            .. entries
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.GameName))
                .Select(static entry => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.GameName.Trim()} ({entry.StartedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)})")),
        ];

        return formatted.Length == 0
            ? TagResult.Incomplete
            : TagResult.Complete(string.Join(", ", formatted));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{viewers [channel]}</c> tag with memoized <see cref="TwitchStream.GetStreams"/> lookup.
/// </summary>
public sealed class TwitchViewersTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "viewers";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<TwitchStream> streamLookup = await TwitchHelixTagHelper
            .GetStreamAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (!streamLookup.IsSuccess)
        {
            return TagResult.Incomplete;
        }

        if (streamLookup.Value is not null && streamLookup.Value.ViewerCount.HasValue)
        {
            return TagResult.Complete(streamLookup.Value.ViewerCount.Value.ToString(CultureInfo.InvariantCulture));
        }

        string? resolvedChannelId = await TwitchHelixTagHelper
            .ResolveBroadcasterIdAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(resolvedChannelId)
            ? TagResult.Incomplete
            : TagResult.Complete("0");
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{followers [channel]}</c> tag with memoized <see cref="ChannelFollower.GetChannelFollowers"/> lookup.
/// </summary>
public sealed class TwitchFollowersTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "followers";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitChannel) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return TagResult.Incomplete;
        }

        string? broadcasterId = await TwitchHelixTagHelper
            .ResolveBroadcasterIdAsync(context, lease, explicitChannel, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<ResponseData<ChannelFollower>> lookup = await TwitchHelixTagHelper
            .GetChannelFollowersTotalAsync(context, lease.TwitchSession, broadcasterId, cancellationToken)
            .ConfigureAwait(false);

        return lookup.IsSuccess && lookup.Value?.Total is int total
            ? TagResult.Complete(total.ToString(CultureInfo.InvariantCulture))
            : TagResult.Incomplete;
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{subscribers}</c> tag with memoized <see cref="Subscription.GetBroadcasterSubscriptions"/> lookup.
/// </summary>
public sealed class TwitchSubscribersTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "subscribers";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null || string.IsNullOrWhiteSpace(lease.BroadcasterId))
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<SubscriptionResponse> lookup = await TwitchHelixTagHelper
            .GetBroadcasterSubscriptionsAsync(context, lease.TwitchSession, lease.BroadcasterId, cancellationToken)
            .ConfigureAwait(false);

        return lookup.IsSuccess && lookup.Value?.Total is int total
            ? TagResult.Complete(total.ToString(CultureInfo.InvariantCulture))
            : TagResult.Incomplete;
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{followage [user] [channel]}</c> tag using memoized <see cref="User.GetUsers"/> and <see cref="ChannelFollower.GetChannelFollowers"/> lookups.
/// </summary>
public sealed class TwitchFollowageTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "followage";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ChannelFollower? follower = await TwitchChannelTagSupport
            .ResolveChannelFollowerAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);
        if (follower?.FollowedAt is not DateTime followedAt)
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset followedAtUtc = TwitchHelixTagHelper.ToUtcDateTimeOffset(followedAt);
        TimeSpan elapsed = context.TimeProvider.GetUtcNow() - followedAtUtc;
        return TagResult.Complete(TwitchHelixTagHelper.FormatDuration(elapsed));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{followdate [user] [channel]}</c> tag using memoized <see cref="User.GetUsers"/> and <see cref="ChannelFollower.GetChannelFollowers"/> lookups.
/// </summary>
public sealed class TwitchFollowdateTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "followdate";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ChannelFollower? follower = await TwitchChannelTagSupport
            .ResolveChannelFollowerAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);
        if (follower?.FollowedAt is not DateTime followedAt)
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset followedAtUtc = TwitchHelixTagHelper.ToUtcDateTimeOffset(followedAt);
        return TagResult.Complete(followedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{hours [user] [decimals]}</c> tag using <see cref="IViewerLoyaltyStore"/> watch time formatted with <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class TwitchHoursTagHandler : ICommandTagHandler
{
    #region Public Constants

    /// <summary>
    /// Default number of decimal places when <c>[decimals]</c> is omitted.
    /// </summary>
    public const int DefaultDecimals = 2;

    /// <summary>
    /// Minimum permitted decimal precision.
    /// </summary>
    public const int MinDecimals = 0;

    /// <summary>
    /// Maximum permitted decimal precision.
    /// </summary>
    public const int MaxDecimals = 4;

    #endregion Public Constants

    #region Public Properties

    /// <inheritdoc />
    public string TagName => "hours";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

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

        (bool userValid, string? explicitUser) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!userValid)
        {
            return TagResult.Incomplete;
        }

        int decimals = DefaultDecimals;
        if (unevaluatedArgs.Count >= 2)
        {
            TagResult decimalsResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!decimalsResult.IsComplete
                || !int.TryParse(decimalsResult.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDecimals))
            {
                return TagResult.Incomplete;
            }

            decimals = Math.Clamp(parsedDecimals, MinDecimals, MaxDecimals);
        }

        WorkspaceViewerProfile? profile = await FindProfileAsync(context, store, explicitUser, cancellationToken)
            .ConfigureAwait(false);

        if (explicitUser is null && profile is null && string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId) && string.IsNullOrWhiteSpace(context.Message.Sender.Username))
        {
            return TagResult.Incomplete;
        }

        double hours = (profile?.WatchTimeSeconds ?? 0L) / 3600.0d;
        string format = string.Create(CultureInfo.InvariantCulture, $"F{decimals}");
        return TagResult.Complete(hours.ToString(format, CultureInfo.InvariantCulture));
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Resolves a viewer loyalty profile from an optional target handle or the invoking sender.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="store">The viewer loyalty store.</param>
    /// <param name="explicitUser">Optional cleaned user login or ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="WorkspaceViewerProfile"/>, or <see langword="null"/> if not found.</returns>
    private static async ValueTask<WorkspaceViewerProfile?> FindProfileAsync(
        ITagEvaluationContext context,
        IViewerLoyaltyStore store,
        string? explicitUser,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitUser))
        {
            WorkspaceViewerProfile? byLogin = await store
                .GetProfileByLoginAsync(context.WorkspaceId, context.PlatformId, explicitUser, cancellationToken)
                .ConfigureAwait(false);
            if (byLogin is not null)
            {
                return byLogin;
            }

            WorkspaceViewerProfile? byId = await store
                .GetProfileAsync(context.WorkspaceId, context.PlatformId, explicitUser, cancellationToken)
                .ConfigureAwait(false);
            return byId is not null
                ? byId
                : (string.Equals(explicitUser, context.Message.Sender.Username, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(explicitUser, context.Message.Sender.DisplayName, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
                ? await store
                    .GetProfileAsync(context.WorkspaceId, context.PlatformId, context.Message.Sender.PlatformUserId, cancellationToken)
                    .ConfigureAwait(false)
                : null;
        }

        if (!string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
        {
            WorkspaceViewerProfile? bySenderId = await store
                .GetProfileAsync(context.WorkspaceId, context.PlatformId, context.Message.Sender.PlatformUserId, cancellationToken)
                .ConfigureAwait(false);
            if (bySenderId is not null)
            {
                return bySenderId;
            }
        }

        return !string.IsNullOrWhiteSpace(context.Message.Sender.Username)
            ? await store
                .GetProfileByLoginAsync(context.WorkspaceId, context.PlatformId, context.Message.Sender.Username, cancellationToken)
                .ConfigureAwait(false)
            : null;
    }

    #endregion Private Methods
}

/// <summary>
/// Handles the Twitch <c>{account_age [user]}</c> tag override using memoized <see cref="User.GetUsers"/> and <see cref="ITagEvaluationContext.TimeProvider"/>.
/// </summary>
public sealed class TwitchAccountAgeTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "account_age";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (bool isValid, string? explicitUser) = await TwitchHelixTagHelper
            .TryEvaluateOptionalHandleAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (!isValid)
        {
            return TagResult.Incomplete;
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset? createdAtUtc = null;
        if (lease is not null)
        {
            User? user = await ResolveUserAsync(context, lease.TwitchSession, explicitUser, cancellationToken)
                .ConfigureAwait(false);
            if (user?.CreatedAt is DateTime createdAt)
            {
                createdAtUtc = TwitchHelixTagHelper.ToUtcDateTimeOffset(createdAt);
            }
        }
        else if (context.GetService<IPlatformUserLookup>() is IPlatformUserLookup fallbackLookup)
        {
            string? lookupKey = explicitUser ?? (!string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId)
                ? context.Message.Sender.PlatformUserId
                : context.Message.Sender.Username);
            if (!string.IsNullOrWhiteSpace(lookupKey))
            {
                createdAtUtc = await fallbackLookup
                    .GetAccountCreatedAtAsync(context.WorkspaceId, context.PlatformId, lookupKey.Trim(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (!createdAtUtc.HasValue)
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset utcValue = createdAtUtc.Value.ToUniversalTime();
        TimeSpan elapsed = context.TimeProvider.GetUtcNow() - utcValue;
        string createdDate = utcValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string formattedDuration = TwitchHelixTagHelper.FormatDuration(elapsed);
        return TagResult.Complete($"{createdDate} ({formattedDuration})");
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Resolves the target Twitch <see cref="User"/> using memoized <see cref="User.GetUsers"/> calls.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="explicitUser">Optional cleaned user login handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="User"/>, or <see langword="null"/> if not found.</returns>
    private static async ValueTask<User?> ResolveUserAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string? explicitUser,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitUser))
        {
            MemoizedLookup<User> byLogin = await TwitchHelixTagHelper
                .GetUserByLoginAsync(context, session, explicitUser, cancellationToken)
                .ConfigureAwait(false);
            return byLogin.Value;
        }

        if (!string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
        {
            MemoizedLookup<User> byId = await TwitchHelixTagHelper
                .GetUserByIdAsync(context, session, context.Message.Sender.PlatformUserId, cancellationToken)
                .ConfigureAwait(false);
            if (byId.Value is not null)
            {
                return byId.Value;
            }
        }

        string senderLogin = TwitchHelixTagHelper.CleanHandle(context.Message.Sender.Username);
        if (string.IsNullOrEmpty(senderLogin))
        {
            return null;
        }

        MemoizedLookup<User> bySenderLogin = await TwitchHelixTagHelper
            .GetUserByLoginAsync(context, session, senderLogin, cancellationToken)
            .ConfigureAwait(false);
        return bySenderLogin.Value;
    }

    #endregion Private Methods
}

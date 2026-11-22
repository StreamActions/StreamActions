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
using StreamActions.Common.Net;
using StreamActions.Core.Templates;
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.Api.Subscriptions;
using StreamActions.Twitch.Api.Teams;
using StreamActions.Twitch.Api.Users;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.UI.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace StreamActions.Twitch.UI.Templates.Handlers;

/// <summary>
/// Non-null wrapper record used to store nullable Helix API lookup results in <see cref="ITagEvaluationContext"/> memoization caches.
/// </summary>
/// <typeparam name="T">The wrapped lookup payload type.</typeparam>
/// <param name="Value">The resolved payload value, or <see langword="null"/> if absent or failed.</param>
/// <param name="IsSuccess"><see langword="true"/> if the Helix API call succeeded (even with empty data); <see langword="false"/> on transport, auth, or scope failure.</param>
internal sealed record MemoizedLookup<T>(T? Value, bool IsSuccess = true);

/// <summary>
/// Shared per-message Helix memoization, channel/user resolution, and UTC formatting helpers for Twitch <see cref="ICommandTagHandler"/> implementations.
/// </summary>
internal static class TwitchHelixTagHelper
{
    #region Internal Constants

    /// <summary>
    /// Canonical lowercase platform identifier for Twitch tag handlers.
    /// </summary>
    internal const string _twitchPlatformId = "twitch";

    #endregion Internal Constants

    #region Internal Methods

    /// <summary>
    /// Normalizes a user or channel handle by trimming whitespace, stripping leading <c>@</c> characters, and lowercasing with invariant culture.
    /// </summary>
    /// <param name="raw">The raw handle input.</param>
    /// <returns>The normalized handle, or empty string if <paramref name="raw"/> is blank.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Twitch login handles are normalized to lowercase invariant.")]
    internal static string CleanHandle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string cleaned = raw.Trim().TrimStart('@').Trim();
        return string.IsNullOrEmpty(cleaned) ? string.Empty : cleaned.ToLowerInvariant();
    }

    /// <summary>
    /// Evaluates an optional single handle argument node, returning <see langword="true"/> if omitted or valid, and <see langword="false"/> if evaluation was incomplete or blank.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated AST arguments.</param>
    /// <param name="index">The zero-based argument index to inspect.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating whether evaluation succeeded and the cleaned handle (or <see langword="null"/> if omitted).</returns>
    internal static async ValueTask<(bool IsValid, string? Handle)> TryEvaluateOptionalHandleAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        int index,
        CancellationToken cancellationToken)
    {
        if (index >= unevaluatedArgs.Count)
        {
            return (true, null);
        }

        TagResult result = await unevaluatedArgs[index].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!result.IsComplete)
        {
            return (false, null);
        }

        string cleaned = CleanHandle(result.Value);
        return string.IsNullOrEmpty(cleaned) ? (false, null) : (true, cleaned);
    }

    /// <summary>
    /// Evaluates a required non-empty trimmed string argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated AST arguments.</param>
    /// <param name="index">The zero-based argument index to inspect.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The trimmed string, or <see langword="null"/> if missing, incomplete, or blank.</returns>
    internal static async ValueTask<string?> EvaluateRequiredStringAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        int index,
        CancellationToken cancellationToken)
    {
        if (index >= unevaluatedArgs.Count)
        {
            return null;
        }

        TagResult result = await unevaluatedArgs[index].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return !result.IsComplete || string.IsNullOrWhiteSpace(result.Value) ? null : result.Value.Trim();
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a UTC <see cref="DateTimeOffset"/>, treating <see cref="DateTimeKind.Unspecified"/> as UTC.
    /// </summary>
    /// <param name="value">The timestamp to convert.</param>
    /// <returns>A UTC <see cref="DateTimeOffset"/> with <see cref="TimeSpan.Zero"/> offset.</returns>
    internal static DateTimeOffset ToUtcDateTimeOffset(DateTime value) =>
        value.Kind == DateTimeKind.Local
            ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero)
            : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    /// <summary>
    /// Formats a non-negative <see cref="TimeSpan"/> into a compact human-readable duration (for example, <c>2d 3h 4m 5s</c>).
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>The formatted duration string using <see cref="CultureInfo.InvariantCulture"/>.</returns>
    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0s";
        }

        int totalDays = (int)duration.TotalDays;
        return totalDays > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{totalDays}d {duration.Hours}h {duration.Minutes}m {duration.Seconds}s")
            : duration.Hours > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s")
            : duration.Minutes > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{duration.Minutes}m {duration.Seconds}s")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.Seconds}s");
    }

    /// <summary>
    /// Acquires and memoizes the workspace <see cref="TwitchWorkspaceSessionLease"/> for the current message evaluation.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="TwitchWorkspaceSessionLease"/>, or <see langword="null"/> if unavailable.</returns>
    internal static async ValueTask<TwitchWorkspaceSessionLease?> GetSessionLeaseAsync(
        ITagEvaluationContext context,
        CancellationToken cancellationToken)
    {
        ITwitchWorkspaceSessionProvider? sessionProvider = context.GetService<ITwitchWorkspaceSessionProvider>();
        if (sessionProvider is null)
        {
            return null;
        }

        string key = $"twitch:session:{context.WorkspaceId}";
        MemoizedLookup<TwitchWorkspaceSessionLease> cached = await context.GetOrComputeMemoizedAsync(
            key,
            async ct =>
            {
                TwitchWorkspaceSessionLease? lease = await sessionProvider
                    .GetSessionAsync(context.WorkspaceId, ct)
                    .ConfigureAwait(false);
                return new MemoizedLookup<TwitchWorkspaceSessionLease>(lease, lease is not null);
            },
            cancellationToken).ConfigureAwait(false);

        return cached.Value;
    }

    /// <summary>
    /// Resolves the target channel login name, defaulting to the workspace broadcaster login or <see cref="ITagEvaluationContext.ChannelName"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="lease">The active Twitch workspace session lease.</param>
    /// <param name="explicitChannel">An optional explicit channel handle supplied to the tag.</param>
    /// <returns>The normalized target channel login name, or empty string if unavailable.</returns>
    internal static string ResolveTargetChannelLogin(
        ITagEvaluationContext context,
        TwitchWorkspaceSessionLease lease,
        string? explicitChannel)
    {
        if (!string.IsNullOrWhiteSpace(explicitChannel))
        {
            return CleanHandle(explicitChannel);
        }

        string broadcasterLogin = CleanHandle(lease.BroadcasterLogin);
        return !string.IsNullOrEmpty(broadcasterLogin)
            ? broadcasterLogin
            : CleanHandle(context.ChannelName);
    }

    /// <summary>
    /// Resolves the Twitch broadcaster user ID for <paramref name="explicitChannel"/> (or the workspace channel when <see langword="null"/>).
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="lease">The active Twitch workspace session lease.</param>
    /// <param name="explicitChannel">An optional explicit channel handle supplied to the tag.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved broadcaster user ID, or <see langword="null"/> if unavailable.</returns>
    internal static async ValueTask<string?> ResolveBroadcasterIdAsync(
        ITagEvaluationContext context,
        TwitchWorkspaceSessionLease lease,
        string? explicitChannel,
        CancellationToken cancellationToken)
    {
        string targetLogin = ResolveTargetChannelLogin(context, lease, explicitChannel);
        string workspaceLogin = CleanHandle(lease.BroadcasterLogin);
        string contextLogin = CleanHandle(context.ChannelName);

        if (!string.IsNullOrWhiteSpace(lease.BroadcasterId)
            && (string.IsNullOrWhiteSpace(explicitChannel)
                || string.Equals(targetLogin, workspaceLogin, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(workspaceLogin) && string.Equals(targetLogin, contextLogin, StringComparison.OrdinalIgnoreCase))))
        {
            return lease.BroadcasterId;
        }

        if (string.IsNullOrEmpty(targetLogin))
        {
            return null;
        }

        MemoizedLookup<User> userLookup = await GetUserByLoginAsync(context, lease.TwitchSession, targetLogin, cancellationToken)
            .ConfigureAwait(false);
        return userLookup.Value?.Id;
    }

    /// <summary>
    /// Retrieves and memoizes the live <see cref="TwitchStream"/> for <paramref name="explicitChannel"/> or the workspace channel.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="lease">The active Twitch workspace session lease.</param>
    /// <param name="explicitChannel">An optional explicit channel handle supplied to the tag.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the live <see cref="TwitchStream"/> if online, or <see langword="null"/> when offline or failed.</returns>
    internal static async ValueTask<MemoizedLookup<TwitchStream>> GetStreamAsync(
        ITagEvaluationContext context,
        TwitchWorkspaceSessionLease lease,
        string? explicitChannel,
        CancellationToken cancellationToken)
    {
        string targetLogin = ResolveTargetChannelLogin(context, lease, explicitChannel);
        if (!string.IsNullOrEmpty(targetLogin))
        {
            string key = $"twitch:streams:login:{targetLogin}";
            return await context.GetOrComputeMemoizedAsync(
                key,
                _ => FetchFirstAsync(
                    () => TwitchStream.GetStreams(lease.TwitchSession, userLogins: [targetLogin])),
                cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(lease.BroadcasterId))
        {
            string broadcasterId = lease.BroadcasterId.Trim();
            string key = $"twitch:streams:id:{broadcasterId}";
            return await context.GetOrComputeMemoizedAsync(
                key,
                _ => FetchFirstAsync(
                    () => TwitchStream.GetStreams(lease.TwitchSession, userIds: [broadcasterId])),
                cancellationToken).ConfigureAwait(false);
        }

        return new MemoizedLookup<TwitchStream>(null, IsSuccess: false);
    }

    /// <summary>
    /// Retrieves and memoizes <see cref="Channel"/> information for <paramref name="broadcasterId"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="broadcasterId">The target broadcaster user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="Channel"/> if found.</returns>
    internal static ValueTask<MemoizedLookup<Channel>> GetChannelInformationAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        string key = $"twitch:channel:{broadcasterId}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchFirstAsync(() => Channel.GetChannelInformation(session, [broadcasterId])),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves and memoizes a Twitch <see cref="User"/> by login name.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="login">The normalized Twitch login name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="User"/> if found.</returns>
    internal static ValueTask<MemoizedLookup<User>> GetUserByLoginAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string login,
        CancellationToken cancellationToken)
    {
        string normalized = CleanHandle(login);
        string key = $"twitch:user:login:{normalized}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchFirstAsync(() => User.GetUsers(session, logins: [normalized])),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves and memoizes a Twitch <see cref="User"/> by immutable user identifier.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="userId">The Twitch user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="User"/> if found.</returns>
    internal static ValueTask<MemoizedLookup<User>> GetUserByIdAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string userId,
        CancellationToken cancellationToken)
    {
        string normalizedId = userId.Trim();
        string key = $"twitch:user:id:{normalizedId}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchFirstAsync(() => User.GetUsers(session, ids: [normalizedId])),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves and memoizes the total follower count for <paramref name="broadcasterId"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="broadcasterId">The target broadcaster user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="ResponseData{TDataType}"/> with <see cref="ResponseData{TDataType}.Total"/>.</returns>
    internal static ValueTask<MemoizedLookup<ResponseData<ChannelFollower>>> GetChannelFollowersTotalAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        string key = $"twitch:followers:total:{broadcasterId}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchResponseAsync(() => ChannelFollower.GetChannelFollowers(session, broadcasterId, userId: null, first: 1)),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves and memoizes the follow relationship between <paramref name="userId"/> and <paramref name="broadcasterId"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="broadcasterId">The target broadcaster user identifier.</param>
    /// <param name="userId">The follower user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="ChannelFollower"/> if the user follows the broadcaster.</returns>
    internal static ValueTask<MemoizedLookup<ChannelFollower>> GetChannelFollowerAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string broadcasterId,
        string userId,
        CancellationToken cancellationToken)
    {
        string key = $"twitch:followers:user:{broadcasterId}:{userId}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchFirstAsync(() => ChannelFollower.GetChannelFollowers(session, broadcasterId, userId: userId, first: 1)),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves and memoizes the broadcaster's subscriber summary response.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="broadcasterId">The broadcaster user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="SubscriptionResponse"/>.</returns>
    internal static ValueTask<MemoizedLookup<SubscriptionResponse>> GetBroadcasterSubscriptionsAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        string key = $"twitch:subscribers:{broadcasterId}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchResponseAsync(() => Subscription.GetBroadcasterSubscriptions(session, broadcasterId, first: 1)),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves and memoizes a Twitch <see cref="Team"/> by team name.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="session">The Twitch API session.</param>
    /// <param name="teamName">The team slug or name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> containing the <see cref="Team"/> if found.</returns>
    internal static ValueTask<MemoizedLookup<Team>> GetTeamByNameAsync(
        ITagEvaluationContext context,
        TwitchSession session,
        string teamName,
        CancellationToken cancellationToken)
    {
        string normalized = CleanHandle(teamName);
        string key = $"twitch:team:{normalized}";
        return context.GetOrComputeMemoizedAsync(
            key,
            _ => FetchFirstAsync(() => Team.GetTeams(session, teamName: normalized)),
            cancellationToken);
    }

    #endregion Internal Methods

    #region Private Methods

    /// <summary>
    /// Executes a Helix API call returning <see cref="ResponseData{TDataType}"/> and captures the first element safely.
    /// </summary>
    /// <typeparam name="T">The Helix item type.</typeparam>
    /// <param name="call">The asynchronous Helix API delegate.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> describing the outcome.</returns>
    private static async ValueTask<MemoizedLookup<T>> FetchFirstAsync<T>(Func<Task<ResponseData<T>?>> call)
        where T : class
    {
        try
        {
            ResponseData<T>? response = await call().ConfigureAwait(false);
            if (response is null || !response.IsSuccessStatusCode)
            {
                return new MemoizedLookup<T>(null, IsSuccess: false);
            }

            T? first = response.Data is { Count: > 0 } ? response.Data[0] : null;
            return new MemoizedLookup<T>(first, IsSuccess: true);
        }
        catch (Exception ex) when (IsRecoverableTwitchException(ex))
        {
            return new MemoizedLookup<T>(null, IsSuccess: false);
        }
    }

    /// <summary>
    /// Executes a Helix API call returning <typeparamref name="TResponse"/> safely.
    /// </summary>
    /// <typeparam name="TResponse">The response wrapper type deriving from <see cref="JsonApiResponse"/>.</typeparam>
    /// <param name="call">The asynchronous Helix API delegate.</param>
    /// <returns>A <see cref="MemoizedLookup{T}"/> describing the outcome.</returns>
    private static async ValueTask<MemoizedLookup<TResponse>> FetchResponseAsync<TResponse>(Func<Task<TResponse?>> call)
        where TResponse : JsonApiResponse
    {
        try
        {
            TResponse? response = await call().ConfigureAwait(false);
            return response is null || !response.IsSuccessStatusCode
                ? new MemoizedLookup<TResponse>(null, IsSuccess: false)
                : new MemoizedLookup<TResponse>(response, IsSuccess: true);
        }
        catch (Exception ex) when (IsRecoverableTwitchException(ex))
        {
            return new MemoizedLookup<TResponse>(null, IsSuccess: false);
        }
    }

    /// <summary>
    /// Determines whether an exception thrown by <c>StreamActions.Twitch</c> represents a recoverable API/auth/transport condition for template tag fallback.
    /// </summary>
    /// <param name="ex">The caught exception.</param>
    /// <returns><see langword="true"/> if recoverable; otherwise, <see langword="false"/>.</returns>
    private static bool IsRecoverableTwitchException(Exception ex) =>
        ex is TwitchScopeMissingException
            or TokenTypeException
            or InvalidOperationException
            or JsonException
            or HttpRequestException
            or ArgumentException;

    #endregion Private Methods
}

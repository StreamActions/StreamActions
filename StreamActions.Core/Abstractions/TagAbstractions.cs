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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides workspace loyalty currency naming settings for template tag evaluation.
/// </summary>
public interface ILoyaltySettingsProvider
{
    /// <summary>
    /// Retrieves the configured singular and plural loyalty currency names for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the singular and plural currency names.</returns>
    ValueTask<(string Singular, string Plural)> GetCurrencyNameAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves loyalty rank titles for viewers within a workspace.
/// </summary>
public interface ILoyaltyRankProvider
{
    /// <summary>
    /// Retrieves the loyalty rank title for a viewer identified by user ID or login handle.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserIdOrLogin">The viewer's platform user identifier or login handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The rank title, or <see langword="null"/> if the viewer holds no rank.</returns>
    ValueTask<string?> GetRankTitleAsync(
        Guid workspaceId,
        string platformId,
        string platformUserIdOrLogin,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the list of active chatters currently present in a workspace channel.
/// </summary>
public interface IChannelChatterProvider
{
    /// <summary>
    /// Retrieves active chatter names or logins for the specified workspace and platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="excludeLurking">Whether viewers marked as lurking should be excluded.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of active chatter display names or logins.</returns>
    ValueTask<IReadOnlyList<string>> GetActiveChattersAsync(
        Guid workspaceId,
        string platformId,
        bool excludeLurking = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides platform user metadata lookups such as account creation timestamps.
/// </summary>
public interface IPlatformUserLookup
{
    /// <summary>
    /// Retrieves the UTC creation timestamp of a platform user account.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="userIdOrLogin">The platform user identifier or login handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The account creation timestamp, or <see langword="null"/> if unavailable.</returns>
    ValueTask<DateTimeOffset?> GetAccountCreatedAtAsync(
        Guid workspaceId,
        string platformId,
        string userIdOrLogin,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides recent tip and donation summaries for a workspace.
/// </summary>
public interface ITipProvider
{
    /// <summary>
    /// Retrieves a formatted summary of the most recent tip or donation recorded for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The formatted summary string, or <see langword="null"/> if no tips have been recorded.</returns>
    ValueTask<string?> GetLastTipSummaryAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches overlay alerts, text overlays, and soundboard hooks for a workspace.
/// </summary>
public interface IOverlayAlertDispatcher
{
    /// <summary>
    /// Dispatches a media alert to the workspace overlay queue.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="file">The registered media file name or key.</param>
    /// <param name="durationSeconds">The display duration in seconds.</param>
    /// <param name="volume">The playback volume multiplier or percentage.</param>
    /// <param name="css">Optional sanitized custom CSS styling.</param>
    /// <param name="message">Optional sanitized overlay message text.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispatch operation.</returns>
    ValueTask DispatchAlertAsync(
        Guid workspaceId,
        string file,
        double durationSeconds = 5d,
        double volume = 1d,
        string? css = null,
        string? message = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a text-only alert banner to the workspace overlay queue.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="message">The sanitized alert message text.</param>
    /// <param name="durationSeconds">The display duration in seconds.</param>
    /// <param name="css">Optional sanitized custom CSS styling.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispatch operation.</returns>
    ValueTask DispatchAlertTextAsync(
        Guid workspaceId,
        string message,
        double durationSeconds = 5d,
        string? css = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a registered soundboard hook playback event to the workspace overlay queue.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="hook">The registered soundboard hook identifier.</param>
    /// <param name="volume">The playback volume multiplier or percentage.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispatch operation.</returns>
    ValueTask DispatchSoundAsync(
        Guid workspaceId,
        string hook,
        double volume = 1d,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches immediate cross-platform messages and delayed chat messages for template tags.
/// </summary>
public interface ICrossPlatformMessageDispatcher
{
    /// <summary>
    /// Sends an immediate chat message to a target platform and optional channel.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="targetPlatformId">The destination platform identifier (for example, <c>twitch</c> or <c>discord</c>).</param>
    /// <param name="targetChannel">The destination channel name or identifier, or <see langword="null"/> for the default channel.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous send operation.</returns>
    ValueTask SendCrossPlatformMessageAsync(
        Guid workspaceId,
        string targetPlatformId,
        string? targetChannel,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a chat message for delayed delivery.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The destination platform identifier.</param>
    /// <param name="channelName">The destination channel name or identifier.</param>
    /// <param name="delay">The delay duration before delivery.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous schedule operation.</returns>
    ValueTask ScheduleMessageAsync(
        Guid workspaceId,
        string platformId,
        string channelName,
        TimeSpan delay,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Schedules delayed command invocations for <c>{delaycommand}</c>.
/// </summary>
public interface IDelayedCommandScheduler
{
    /// <summary>
    /// Schedules <paramref name="invocation"/> to execute after <paramref name="delay"/>.
    /// </summary>
    /// <param name="invocation">The child command invocation carrying chain-depth and visited-trigger metadata.</param>
    /// <param name="delay">The delay duration before execution.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous scheduling operation.</returns>
    ValueTask ScheduleCommandAsync(
        ICommandInvocation invocation,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}

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

namespace StreamActions.Twitch.UI.Abstractions;

/// <summary>
/// Represents a single game or category entry played during a Twitch stream session.
/// </summary>
/// <param name="GameName">The display name of the game or category.</param>
/// <param name="StartedAtUtc">The UTC timestamp when the game or category began.</param>
public sealed record TwitchGameHistoryEntry(string GameName, DateTimeOffset StartedAtUtc);

/// <summary>
/// Provides persisted Twitch stream offline and game-history timestamps for <c>{downtime}</c>, <c>{playtime}</c>, and <c>{games_played}</c>.
/// </summary>
public interface ITwitchStreamHistoryProvider
{
    /// <summary>
    /// Retrieves the UTC timestamp when the stream last went offline for the workspace or optional <paramref name="channelLogin"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="channelLogin">An optional target channel login name, or <see langword="null"/> for the workspace's primary channel.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The last offline UTC timestamp, or <see langword="null"/> if not recorded.</returns>
    ValueTask<DateTimeOffset?> GetLastOfflineAtUtcAsync(
        Guid workspaceId,
        string? channelLogin = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the UTC timestamp when the current game or category started during the active stream session.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The UTC start timestamp of the current game, or <see langword="null"/> if not recorded.</returns>
    ValueTask<DateTimeOffset?> GetCurrentGameStartedAtUtcAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the chronological list of games or categories played during the current stream session.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="TwitchGameHistoryEntry"/> entries.</returns>
    ValueTask<IReadOnlyList<TwitchGameHistoryEntry>> GetGamesPlayedHistoryAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}

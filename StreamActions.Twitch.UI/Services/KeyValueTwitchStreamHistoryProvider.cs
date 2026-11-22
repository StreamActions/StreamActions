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
using StreamActions.Twitch.UI.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace StreamActions.Twitch.UI.Services;

/// <summary>
/// <see cref="ITwitchStreamHistoryProvider"/> implementation backed optionally by <see cref="IWorkspaceKeyValueStore"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KeyValueTwitchStreamHistoryProvider"/> class.
/// </remarks>
/// <param name="keyValueStore">Optional workspace key-value store; when <see langword="null"/>, lookups return empty results.</param>
public sealed class KeyValueTwitchStreamHistoryProvider(IWorkspaceKeyValueStore? keyValueStore = null) : ITwitchStreamHistoryProvider
{
    #region Public Constants

    /// <summary>
    /// Key-value table name storing Twitch stream session history.
    /// </summary>
    public const string DefaultTableName = "twitch_stream_history";

    /// <summary>
    /// Default key-value section name for the workspace's primary Twitch stream.
    /// </summary>
    public const string DefaultSectionName = "stream";

    /// <summary>
    /// Key-value variable storing the last recorded offline UTC timestamp.
    /// </summary>
    public const string LastOfflineVariableName = "last_offline_at_utc";

    /// <summary>
    /// Key-value variable storing the UTC timestamp when the current game started.
    /// </summary>
    public const string CurrentGameStartedVariableName = "current_game_started_at_utc";

    /// <summary>
    /// Key-value variable storing the JSON-serialized list of <see cref="TwitchGameHistoryEntry"/> entries for the current stream session.
    /// </summary>
    public const string GamesPlayedVariableName = "games_played_history";

    #endregion Public Constants

    #region Private Fields

    /// <summary>
    /// Optional workspace key-value store used to persist and query stream history.
    /// </summary>
    private readonly IWorkspaceKeyValueStore? _keyValueStore = keyValueStore;

    #endregion Private Fields
    #region Public Constructors

    #endregion Public Constructors

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<DateTimeOffset?> GetLastOfflineAtUtcAsync(
        Guid workspaceId,
        string? channelLogin = null,
        CancellationToken cancellationToken = default)
    {
        if (this._keyValueStore is null || workspaceId == Guid.Empty)
        {
            return null;
        }

        string section = ResolveChannelSection(channelLogin);
        string? raw = await this._keyValueStore
            .GetStringAsync(workspaceId, DefaultTableName, section, LastOfflineVariableName, null, cancellationToken)
            .ConfigureAwait(false);

        return TryParseUtcTimestamp(raw, out DateTimeOffset timestamp) ? timestamp : null;
    }

    /// <inheritdoc />
    public async ValueTask<DateTimeOffset?> GetCurrentGameStartedAtUtcAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (this._keyValueStore is null || workspaceId == Guid.Empty)
        {
            return null;
        }

        string? raw = await this._keyValueStore
            .GetStringAsync(workspaceId, DefaultTableName, DefaultSectionName, CurrentGameStartedVariableName, null, cancellationToken)
            .ConfigureAwait(false);

        return TryParseUtcTimestamp(raw, out DateTimeOffset timestamp) ? timestamp : null;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TwitchGameHistoryEntry>> GetGamesPlayedHistoryAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (this._keyValueStore is null || workspaceId == Guid.Empty)
        {
            return [];
        }

        string? rawJson = await this._keyValueStore
            .GetStringAsync(workspaceId, DefaultTableName, DefaultSectionName, GamesPlayedVariableName, null, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            List<TwitchGameHistoryEntry>? entries = JsonSerializer.Deserialize<List<TwitchGameHistoryEntry>>(rawJson);
            return entries is null ? [] : entries.AsReadOnly();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Persists the UTC timestamp when a channel went offline.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="offlineAtUtc">The UTC timestamp when the stream went offline.</param>
    /// <param name="channelLogin">Optional target channel login, or <see langword="null"/> for the workspace's primary channel.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous write operation.</returns>
    public async ValueTask RecordStreamOfflineAsync(
        Guid workspaceId,
        DateTimeOffset offlineAtUtc,
        string? channelLogin = null,
        CancellationToken cancellationToken = default)
    {
        if (this._keyValueStore is null || workspaceId == Guid.Empty)
        {
            return;
        }

        string section = ResolveChannelSection(channelLogin);
        string formatted = offlineAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        await this._keyValueStore
            .SetAsync(workspaceId, DefaultTableName, section, LastOfflineVariableName, formatted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Records the start of a new game or category for the current stream session, updating both <see cref="CurrentGameStartedVariableName"/> and <see cref="GamesPlayedVariableName"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="gameName">The game or category display name.</param>
    /// <param name="startedAtUtc">The UTC timestamp when the game or category started.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous write operation.</returns>
    public async ValueTask RecordGameStartedAsync(
        Guid workspaceId,
        string gameName,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        if (this._keyValueStore is null || workspaceId == Guid.Empty)
        {
            return;
        }

        DateTimeOffset utcTimestamp = startedAtUtc.ToUniversalTime();
        string formattedTimestamp = utcTimestamp.ToString("O", CultureInfo.InvariantCulture);
        await this._keyValueStore
            .SetAsync(workspaceId, DefaultTableName, DefaultSectionName, CurrentGameStartedVariableName, formattedTimestamp, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<TwitchGameHistoryEntry> existing = await this
            .GetGamesPlayedHistoryAsync(workspaceId, cancellationToken)
            .ConfigureAwait(false);

        List<TwitchGameHistoryEntry> updated = [.. existing, new TwitchGameHistoryEntry(gameName.Trim(), utcTimestamp)];
        string json = JsonSerializer.Serialize(updated);
        await this._keyValueStore
            .SetAsync(workspaceId, DefaultTableName, DefaultSectionName, GamesPlayedVariableName, json, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the current game start timestamp and games-played session history.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous reset operation.</returns>
    public async ValueTask ResetStreamSessionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (this._keyValueStore is null || workspaceId == Guid.Empty)
        {
            return;
        }

        _ = await this._keyValueStore
            .DeleteAsync(workspaceId, DefaultTableName, DefaultSectionName, CurrentGameStartedVariableName, cancellationToken)
            .ConfigureAwait(false);
        _ = await this._keyValueStore
            .DeleteAsync(workspaceId, DefaultTableName, DefaultSectionName, GamesPlayedVariableName, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Resolves the key-value section name for the primary workspace channel or an explicit channel login.
    /// </summary>
    /// <param name="channelLogin">The optional channel login name.</param>
    /// <returns>The section name.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Twitch channel logins are normalized to lowercase invariant.")]
    private static string ResolveChannelSection(string? channelLogin)
    {
        if (string.IsNullOrWhiteSpace(channelLogin))
        {
            return DefaultSectionName;
        }

        string normalized = channelLogin.Trim().TrimStart('@').ToLowerInvariant();
        return string.IsNullOrEmpty(normalized) ? DefaultSectionName : $"channel:{normalized}";
    }

    /// <summary>
    /// Parses an invariant-culture UTC timestamp string into a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="raw">The raw stored timestamp string.</param>
    /// <param name="timestamp">When this method returns <see langword="true"/>, contains the parsed UTC timestamp.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseUtcTimestamp(string? raw, out DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            timestamp = default;
            return false;
        }

        return DateTimeOffset.TryParse(
            raw.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    #endregion Private Methods
}

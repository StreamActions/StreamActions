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

using StreamActions.Twitch.Api.Common;

namespace StreamActions.Twitch.UI.Abstractions;

/// <summary>
/// Represents a disposable lease over a workspace-scoped <see cref="TwitchSession"/> and broadcaster identity.
/// </summary>
public sealed class TwitchWorkspaceSessionLease : IDisposable, IAsyncDisposable
{
    #region Private Fields

    /// <summary>
    /// Indicates whether disposing this lease also disposes <see cref="TwitchSession"/>.
    /// </summary>
    private readonly bool _ownsSession;

    /// <summary>
    /// Tracks whether this lease has already been disposed.
    /// </summary>
    private bool _disposed;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchWorkspaceSessionLease"/> class.
    /// </summary>
    /// <param name="twitchSession">The configured <see cref="Twitch.Api.Common.TwitchSession"/> instance.</param>
    /// <param name="broadcasterId">The Twitch broadcaster user identifier for the workspace, if known.</param>
    /// <param name="broadcasterLogin">The Twitch broadcaster login name for the workspace, if known.</param>
    /// <param name="ownsSession"><see langword="true"/> to dispose <paramref name="twitchSession"/> when the lease is disposed; otherwise, <see langword="false"/>.</param>
    public TwitchWorkspaceSessionLease(
        TwitchSession twitchSession,
        string broadcasterId,
        string broadcasterLogin,
        bool ownsSession = true)
    {
        ArgumentNullException.ThrowIfNull(twitchSession);

        this.TwitchSession = twitchSession;
        this.BroadcasterId = broadcasterId?.Trim() ?? string.Empty;
        this.BroadcasterLogin = broadcasterLogin?.Trim() ?? string.Empty;
        this._ownsSession = ownsSession;
    }

    #endregion Public Constructors

    #region Public Properties

    /// <summary>
    /// Gets the configured <see cref="Twitch.Api.Common.TwitchSession"/> for Helix API requests.
    /// </summary>
    public TwitchSession TwitchSession { get; }

    /// <summary>
    /// Gets the Twitch broadcaster user identifier associated with the workspace.
    /// </summary>
    public string BroadcasterId { get; }

    /// <summary>
    /// Gets the Twitch broadcaster login name associated with the workspace.
    /// </summary>
    public string BroadcasterLogin { get; }

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        if (this._ownsSession)
        {
            this.TwitchSession.Dispose();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        this.Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion Public Methods
}

/// <summary>
/// Resolves an authenticated <see cref="TwitchWorkspaceSessionLease"/> for a workspace.
/// </summary>
public interface ITwitchWorkspaceSessionProvider
{
    /// <summary>
    /// Acquires a disposable <see cref="TwitchWorkspaceSessionLease"/> for <paramref name="workspaceId"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="TwitchWorkspaceSessionLease"/> when credentials are available; otherwise, <see langword="null"/>.</returns>
    ValueTask<TwitchWorkspaceSessionLease?> GetSessionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}

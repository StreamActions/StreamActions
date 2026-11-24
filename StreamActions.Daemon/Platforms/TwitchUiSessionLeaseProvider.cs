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

using StreamActions.Scripting.Runtime;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.UI.Abstractions;

namespace StreamActions.Daemon.Platforms;

/// <summary>
/// Bridges <see cref="ITwitchWorkspaceSessionProvider"/> in <c>StreamActions.Twitch.UI</c> to <see cref="ITwitchSessionLeaseProvider"/> in <c>StreamActions.Scripting</c>.
/// </summary>
internal sealed class TwitchUiSessionLeaseProvider(IServiceScopeFactory scopeFactory) : ITwitchSessionLeaseProvider
{
    /// <summary>
    /// Scope factory used to resolve scoped <see cref="ITwitchWorkspaceSessionProvider"/> instances.
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    /// <inheritdoc/>
    public async ValueTask<ITwitchSessionLease?> AcquireSessionAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        AsyncServiceScope scope = this._scopeFactory.CreateAsyncScope();
        try
        {
            ITwitchWorkspaceSessionProvider? provider = scope.ServiceProvider.GetService<ITwitchWorkspaceSessionProvider>();
            if (provider is null)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            TwitchWorkspaceSessionLease? innerLease = await provider.GetSessionAsync(workspaceId, cancellationToken).ConfigureAwait(false);
            if (innerLease is null)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            return new ScopedTwitchSessionLease(scope, innerLease);
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Disposes both the inner <see cref="TwitchWorkspaceSessionLease"/> and its owning <see cref="AsyncServiceScope"/>.
    /// </summary>
    private sealed class ScopedTwitchSessionLease(AsyncServiceScope scope, TwitchWorkspaceSessionLease innerLease) : ITwitchSessionLease
    {
        private readonly AsyncServiceScope _scope = scope;
        private readonly TwitchWorkspaceSessionLease _innerLease = innerLease;
        private int _disposed;

        /// <inheritdoc/>
        public TwitchSession Session => this._innerLease.TwitchSession;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this._disposed, 1) != 0)
            {
                return;
            }

            this._innerLease.Dispose();
            this._scope.Dispose();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this._disposed, 1) != 0)
            {
                return;
            }

            await this._innerLease.DisposeAsync().ConfigureAwait(false);
            await this._scope.DisposeAsync().ConfigureAwait(false);
        }
    }
}


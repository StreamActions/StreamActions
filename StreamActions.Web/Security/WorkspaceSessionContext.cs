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

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Security.Claims;

namespace StreamActions.Web.Security;

/// <summary>
/// Scoped <see cref="IWorkspaceSessionContext"/> backed by <see cref="AuthenticationStateProvider"/>,
/// <see cref="IWorkspaceAccessService"/>, and optional <see cref="IHttpContextAccessor"/> cookies.
/// </summary>
public sealed class WorkspaceSessionContext : IWorkspaceSessionContext, IDisposable
{
    /// <summary>
    /// Synchronization gate preventing concurrent initialization or refresh races within a Blazor circuit.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The Blazor authentication state provider.
    /// </summary>
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    /// <summary>
    /// The panel user store.
    /// </summary>
    private readonly IPanelUserStore _panelUserStore;

    /// <summary>
    /// The workspace access evaluation service.
    /// </summary>
    private readonly IWorkspaceAccessService _workspaceAccessService;

    /// <summary>
    /// Optional HTTP context accessor used during SSR or HTTP requests to inspect the <c>StreamActions.Workspace</c> cookie.
    /// </summary>
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Indicates whether the session context has completed initial loading.
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// Indicates whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSessionContext"/> class.
    /// </summary>
    /// <param name="authenticationStateProvider">The authentication state provider.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceAccessService">The workspace access service.</param>
    /// <param name="httpContextAccessor">Optional HTTP context accessor.</param>
    public WorkspaceSessionContext(
        AuthenticationStateProvider authenticationStateProvider,
        IPanelUserStore panelUserStore,
        IWorkspaceAccessService workspaceAccessService,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(authenticationStateProvider);
        ArgumentNullException.ThrowIfNull(panelUserStore);
        ArgumentNullException.ThrowIfNull(workspaceAccessService);

        this._authenticationStateProvider = authenticationStateProvider;
        this._panelUserStore = panelUserStore;
        this._workspaceAccessService = workspaceAccessService;
        this._httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? ActiveWorkspaceId { get; private set; }

    /// <inheritdoc />
    public AccessibleWorkspace? ActiveWorkspace { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces { get; private set; } = [];

    /// <inheritdoc />
    public event EventHandler? WorkspaceChanged;

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        if (this._initialized)
        {
            return;
        }

        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> SelectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        if (workspaceId == Guid.Empty)
        {
            return false;
        }

        if (!this._initialized)
        {
            await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AccessibleWorkspace? match = this.AccessibleWorkspaces
                .FirstOrDefault(w => w.Workspace.Id == workspaceId);

            if (match is null)
            {
                return false;
            }

            bool changed = this.ActiveWorkspaceId != match.Workspace.Id;
            this.ActiveWorkspace = match;
            this.ActiveWorkspaceId = match.Workspace.Id;

            if (changed)
            {
                this.WorkspaceChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }
        finally
        {
            _ = this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool notifyChanged = false;
        try
        {
            Guid? previousActiveId = this.ActiveWorkspaceId;
            int previousCount = this.AccessibleWorkspaces.Count;

            AuthenticationState authState = await this._authenticationStateProvider
                .GetAuthenticationStateAsync()
                .ConfigureAwait(false);
            ClaimsPrincipal principal = authState.User;

            if (principal.Identity?.IsAuthenticated != true
                || AuthClaims.HasTrueClaim(principal, AuthClaims.MfaPendingClaimType))
            {
                this.AccessibleWorkspaces = [];
                this.ActiveWorkspace = null;
                this.ActiveWorkspaceId = null;
                this._initialized = true;
                notifyChanged = previousActiveId is not null || previousCount > 0;
            }
            else
            {
                Guid? panelUserId = AuthClaims.GetPanelUserId(principal);
                PanelUser? user = panelUserId.HasValue
                    ? await this._panelUserStore.GetByIdAsync(panelUserId.Value, cancellationToken).ConfigureAwait(false)
                    : null;

                if (user is null)
                {
                    this.AccessibleWorkspaces = [];
                    this.ActiveWorkspace = null;
                    this.ActiveWorkspaceId = null;
                    this._initialized = true;
                    notifyChanged = previousActiveId is not null || previousCount > 0;
                }
                else
                {
                    IReadOnlyList<UserLoginIdentity> identities = await this._panelUserStore
                        .GetLoginIdentitiesAsync(user.Id, cancellationToken)
                        .ConfigureAwait(false);
                    IReadOnlyList<string> oidcGroups = AuthClaims.GetOidcGroups(principal);
                    IReadOnlyList<AccessibleWorkspace> accessible = await this._workspaceAccessService
                        .GetAccessibleWorkspacesAsync(user, identities, oidcGroups, cancellationToken)
                        .ConfigureAwait(false);

                    AccessibleWorkspace? selected = this.ResolvePreferredWorkspace(principal, accessible, previousActiveId);

                    this.AccessibleWorkspaces = accessible;
                    this.ActiveWorkspace = selected;
                    this.ActiveWorkspaceId = selected?.Workspace.Id;
                    this._initialized = true;

                    notifyChanged = previousActiveId != this.ActiveWorkspaceId || previousCount != accessible.Count;
                }
            }
        }
        finally
        {
            _ = this._gate.Release();
        }

        if (notifyChanged)
        {
            this.WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        this._gate.Dispose();
    }

    /// <summary>
    /// Selects the preferred active workspace using current state, the <c>StreamActions.Workspace</c> cookie,
    /// the login platform claim, or the first accessible workspace.
    /// </summary>
    /// <param name="principal">The authenticated user's principal.</param>
    /// <param name="accessible">The workspaces accessible to the user.</param>
    /// <param name="previousActiveId">The previously selected workspace identifier in this circuit, if any.</param>
    /// <returns>The selected <see cref="AccessibleWorkspace"/>, or <see langword="null"/> if <paramref name="accessible"/> is empty.</returns>
    private AccessibleWorkspace? ResolvePreferredWorkspace(
        ClaimsPrincipal principal,
        IReadOnlyList<AccessibleWorkspace> accessible,
        Guid? previousActiveId)
    {
        if (accessible.Count == 0)
        {
            return null;
        }

        if (previousActiveId.HasValue)
        {
            AccessibleWorkspace? existing = accessible.FirstOrDefault(w => w.Workspace.Id == previousActiveId.Value);
            if (existing is not null)
            {
                return existing;
            }
        }

        HttpContext? httpContext = this._httpContextAccessor?.HttpContext;
        if (httpContext is not null
            && httpContext.Request.Cookies.TryGetValue(AuthClaims.WorkspaceCookieName, out string? rawCookieId)
            && Guid.TryParse(rawCookieId, out Guid cookieWorkspaceId))
        {
            AccessibleWorkspace? fromCookie = accessible.FirstOrDefault(w => w.Workspace.Id == cookieWorkspaceId);
            if (fromCookie is not null)
            {
                return fromCookie;
            }
        }

        string? authProvider = principal.FindFirstValue(AuthClaims.AuthProviderClaimType);
        if (!string.IsNullOrWhiteSpace(authProvider))
        {
            AccessibleWorkspace? ownedForPlatform = accessible.FirstOrDefault(w =>
                w.IsOwner
                && string.Equals(w.Workspace.PrimaryPlatformId, authProvider.Trim(), StringComparison.OrdinalIgnoreCase));
            if (ownedForPlatform is not null)
            {
                return ownedForPlatform;
            }
        }

        return accessible[0];
    }
}

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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models.Entities;
using StreamActions.Database;
using System.Globalization;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Executes administrative daemon commands invoked via the interactive console or local IPC <c>exec</c> client.
/// </summary>
internal sealed class DaemonCommandProcessor
{
    /// <summary>
    /// Database context factory for querying and updating panel users.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// Plugin manager used to report loaded plugin counts.
    /// </summary>
    private readonly IPluginManager _pluginManager;

    /// <summary>
    /// Service managing single-use console bootstrap tokens.
    /// </summary>
    private readonly ConsoleBootstrapTokenService _bootstrapTokenService;

    /// <summary>
    /// Service responsible for triggering database backups.
    /// </summary>
    private readonly IDatabaseBackupService _backupService;

    /// <summary>
    /// Host bootstrap options.
    /// </summary>
    private readonly IOptions<BootstrapOptions> _bootstrapOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaemonCommandProcessor"/> class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <param name="bootstrapTokenService">The console bootstrap token service.</param>
    /// <param name="backupService">The database backup service.</param>
    /// <param name="bootstrapOptions">The host bootstrap options.</param>
    public DaemonCommandProcessor(
        IDbContextFactory<StreamActionsDbContext> contextFactory,
        IPluginManager pluginManager,
        ConsoleBootstrapTokenService bootstrapTokenService,
        IDatabaseBackupService backupService,
        IOptions<BootstrapOptions> bootstrapOptions)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(pluginManager);
        ArgumentNullException.ThrowIfNull(bootstrapTokenService);
        ArgumentNullException.ThrowIfNull(backupService);
        ArgumentNullException.ThrowIfNull(bootstrapOptions);

        this._contextFactory = contextFactory;
        this._pluginManager = pluginManager;
        this._bootstrapTokenService = bootstrapTokenService;
        this._backupService = backupService;
        this._bootstrapOptions = bootstrapOptions;
    }

    /// <summary>
    /// Executes a single administrative command line and returns the human-readable response.
    /// </summary>
    /// <param name="commandLine">The raw command line (for example <c>status</c>, <c>bootstrap-token</c>, <c>backup</c>, or <c>unlock-user &lt;username&gt;</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The command output text.</returns>
    public async ValueTask<string> ExecuteCommandAsync(
        string commandLine,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return "Available commands: status, bootstrap-token, backup, unlock-user <username>";
        }

        string[] parts = commandLine.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string verb = parts[0].ToUpperInvariant();
        string? argument = parts.Length > 1 ? parts[1] : null;

        return verb switch
        {
            "STATUS" => await this.GetStatusAsync(cancellationToken).ConfigureAwait(false),
            "BOOTSTRAP-TOKEN" => this.IssueBootstrapToken(),
            "BACKUP" => await this.TriggerBackupAsync(cancellationToken).ConfigureAwait(false),
            "UNLOCK-USER" => await this.UnlockUserAsync(argument, cancellationToken).ConfigureAwait(false),
            "HELP" => "Available commands: status, bootstrap-token, backup, unlock-user <username>",
            _ => $"Unknown command '{parts[0]}'. Available commands: status, bootstrap-token, backup, unlock-user <username>",
        };
    }

    /// <summary>
    /// Builds a formatted status summary for the running daemon, database connectivity, and loaded plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The formatted status summary string.</returns>
    private async ValueTask<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        BootstrapOptions options = this._bootstrapOptions.Value;
        bool dbConnected;
        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            dbConnected = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Status: Running | Mode: {options.Node.Mode} | Shard: {options.Node.ShardId}/{options.Node.TotalShards} | Database: {options.Database.Provider} ({(dbConnected ? "Connected" : "Disconnected")}) | Plugins: {this._pluginManager.Plugins.Count}");
    }

    /// <summary>
    /// Issues or retrieves the active console bootstrap token and formats it for display.
    /// </summary>
    /// <returns>A message containing the active bootstrap token.</returns>
    private string IssueBootstrapToken()
    {
        string token = this._bootstrapTokenService.GetOrCreateToken();
        return $"Bootstrap token (valid for 5 minutes): {token}";
    }

    /// <summary>
    /// Triggers an on-demand database backup and returns the resulting archive path.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A message containing the created backup path.</returns>
    private async ValueTask<string> TriggerBackupAsync(CancellationToken cancellationToken)
    {
        string backupPath = await this._backupService.TriggerBackupAsync(cancellationToken).ConfigureAwait(false);
        return $"Backup created: {backupPath}";
    }

    /// <summary>
    /// Removes all MFA credentials for the specified panel user to restore account access.
    /// </summary>
    /// <param name="username">The display name or provider username of the user to unlock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A human-readable result message.</returns>
    private async ValueTask<string> UnlockUserAsync(string? username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Usage: unlock-user <username>";
        }

        string normalized = username.Trim();
        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            List<PanelUser> users = await dbContext.PanelUsers.ToListAsync(cancellationToken).ConfigureAwait(false);
            PanelUser? user = users.FirstOrDefault(u => string.Equals(u.DisplayName, normalized, StringComparison.OrdinalIgnoreCase));

            if (user is null)
            {
                List<UserLoginIdentity> identities = await dbContext.UserLoginIdentities.ToListAsync(cancellationToken).ConfigureAwait(false);
                UserLoginIdentity? identity = identities.FirstOrDefault(i => string.Equals(i.ProviderUsername, normalized, StringComparison.OrdinalIgnoreCase));
                if (identity is not null)
                {
                    user = users.FirstOrDefault(u => u.Id == identity.PanelUserId);
                }
            }

            if (user is null)
            {
                return $"User '{normalized}' not found.";
            }

            List<PanelUserMfaCredential> credentials = await dbContext.PanelUserMfaCredentials
                .Where(c => c.PanelUserId == user.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (credentials.Count > 0)
            {
                dbContext.PanelUserMfaCredentials.RemoveRange(credentials);
                _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return $"Unlocked user '{user.DisplayName}' (cleared {credentials.Count} MFA credential(s)).";
        }
    }
}

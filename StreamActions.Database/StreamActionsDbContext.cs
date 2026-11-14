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
using StreamActions.Core.Models.Entities;

namespace StreamActions.Database;

/// <summary>
/// Entity Framework Core database context for StreamActions persistence.
/// </summary>
public class StreamActionsDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamActionsDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by this context.</param>
    public StreamActionsDbContext(DbContextOptions<StreamActionsDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamActionsDbContext"/> class for provider-specific subclasses.
    /// </summary>
    /// <param name="options">The non-generic options to be used by the provider subclass.</param>
    protected StreamActionsDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the workspaces table.
    /// </summary>
    public DbSet<Workspace> Workspaces => this.Set<Workspace>();

    /// <summary>
    /// Gets the workspace platform attachments table.
    /// </summary>
    public DbSet<WorkspacePlatform> WorkspacePlatforms => this.Set<WorkspacePlatform>();

    /// <summary>
    /// Gets the encrypted platform credentials table.
    /// </summary>
    public DbSet<PlatformCredential> PlatformCredentials => this.Set<PlatformCredential>();

    /// <summary>
    /// Gets the workspace secrets table.
    /// </summary>
    public DbSet<WorkspaceSecret> WorkspaceSecrets => this.Set<WorkspaceSecret>();

    /// <summary>
    /// Gets the custom commands table.
    /// </summary>
    public DbSet<CustomCommand> CustomCommands => this.Set<CustomCommand>();

    /// <summary>
    /// Gets the workspace timers table.
    /// </summary>
    public DbSet<WorkspaceTimer> WorkspaceTimers => this.Set<WorkspaceTimer>();

    /// <summary>
    /// Gets the workspace counters table.
    /// </summary>
    public DbSet<WorkspaceCounter> WorkspaceCounters => this.Set<WorkspaceCounter>();

    /// <summary>
    /// Gets the workspace viewer profiles table.
    /// </summary>
    public DbSet<WorkspaceViewerProfile> WorkspaceViewerProfiles => this.Set<WorkspaceViewerProfile>();

    /// <summary>
    /// Gets the workspace loyalty store items table.
    /// </summary>
    public DbSet<WorkspaceLoyaltyStoreItem> WorkspaceLoyaltyStoreItems => this.Set<WorkspaceLoyaltyStoreItem>();

    /// <summary>
    /// Gets the workspace key-value entries table.
    /// </summary>
    public DbSet<WorkspaceKeyValueEntry> WorkspaceKeyValueEntries => this.Set<WorkspaceKeyValueEntry>();

    /// <summary>
    /// Gets the workspace audit logs table.
    /// </summary>
    public DbSet<WorkspaceAuditLog> WorkspaceAuditLogs => this.Set<WorkspaceAuditLog>();

    /// <summary>
    /// Gets the workspace verifier tokens table.
    /// </summary>
    public DbSet<WorkspaceVerifierToken> WorkspaceVerifierTokens => this.Set<WorkspaceVerifierToken>();

    /// <summary>
    /// Gets the panel users table.
    /// </summary>
    public DbSet<PanelUser> PanelUsers => this.Set<PanelUser>();

    /// <summary>
    /// Gets the user login identities table.
    /// </summary>
    public DbSet<UserLoginIdentity> UserLoginIdentities => this.Set<UserLoginIdentity>();

    /// <summary>
    /// Gets the panel user MFA credentials table.
    /// </summary>
    public DbSet<PanelUserMfaCredential> PanelUserMfaCredentials => this.Set<PanelUserMfaCredential>();

    /// <summary>
    /// Gets the permission groups table.
    /// </summary>
    public DbSet<PermissionGroup> PermissionGroups => this.Set<PermissionGroup>();

    /// <summary>
    /// Gets the permission grants table.
    /// </summary>
    public DbSet<PermissionGrant> PermissionGrants => this.Set<PermissionGrant>();

    /// <summary>
    /// Gets the workspace global group assignments table.
    /// </summary>
    public DbSet<WorkspaceGlobalGroupAssignment> WorkspaceGlobalGroupAssignments => this.Set<WorkspaceGlobalGroupAssignment>();

    /// <summary>
    /// Gets the permission group members table.
    /// </summary>
    public DbSet<PermissionGroupMember> PermissionGroupMembers => this.Set<PermissionGroupMember>();

    /// <summary>
    /// Gets the user permission overrides table.
    /// </summary>
    public DbSet<UserPermissionOverride> UserPermissionOverrides => this.Set<UserPermissionOverride>();

    /// <summary>
    /// Gets the workspace platform role rosters table.
    /// </summary>
    public DbSet<WorkspacePlatformRoleRoster> WorkspacePlatformRoleRosters => this.Set<WorkspacePlatformRoleRoster>();

    /// <summary>
    /// Gets the external OIDC providers table.
    /// </summary>
    public DbSet<ExternalOidcProvider> ExternalOidcProviders => this.Set<ExternalOidcProvider>();

    /// <summary>
    /// Gets the plugin states table.
    /// </summary>
    public DbSet<PluginState> PluginStates => this.Set<PluginState>();

    /// <summary>
    /// Gets the instance settings table.
    /// </summary>
    public DbSet<InstanceSettings> InstanceSettings => this.Set<InstanceSettings>();

    /// <summary>
    /// Applies the shared StreamActions EF Core defaults (<see cref="QueryTrackingBehavior.NoTracking"/> and snake_case naming)
    /// to a <see cref="DbContextOptionsBuilder"/> without modifying options inside <c>OnConfiguring</c> (preserving pooling).
    /// </summary>
    /// <param name="optionsBuilder">The options builder to configure.</param>
    /// <returns>The configured <see cref="DbContextOptionsBuilder"/>.</returns>
    public static DbContextOptionsBuilder ConfigureDefaultOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        _ = optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _ = optionsBuilder.UseSnakeCaseNamingConvention();
        return optionsBuilder;
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(StreamActionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

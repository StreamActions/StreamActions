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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.IO.Compression;
using System.Text.Json;

namespace StreamActions.Database.Services;

/// <summary>
/// Exports and imports portable <c>.sabackup</c> workspace archives as GZipped JSON without leaking secret values or OAuth tokens.
/// </summary>
public sealed class WorkspacePortableArchiveService : IWorkspacePortableArchiveService
{
    /// <summary>
    /// Compact JSON serializer options used for <c>.sabackup</c> archive payloads.
    /// </summary>
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// The time provider used to stamp exported archive metadata.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacePortableArchiveService"/> class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider.</param>
    public WorkspacePortableArchiveService(
        IDbContextFactory<StreamActionsDbContext> contextFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._contextFactory = contextFactory;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspacePortableArchive> ExportAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            Workspace workspace = await dbContext.Workspaces
                .FirstOrDefaultAsync(x => x.Id == workspaceId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Workspace '{workspaceId}' was not found.");

            List<CustomCommand> commands = await dbContext.CustomCommands
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.Trigger)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<WorkspaceTimer> timers = await dbContext.WorkspaceTimers
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.PoolName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<PermissionGroup> groups = await dbContext.PermissionGroups
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<Guid> groupIds = [.. groups.Select(static g => g.Id)];
            List<PermissionGrant> grants = groupIds.Count == 0
                ? []
                : await dbContext.PermissionGrants
                    .Where(x => groupIds.Contains(x.GroupId))
                    .OrderBy(static x => x.PermissionKey)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            List<WorkspaceCounter> counters = await dbContext.WorkspaceCounters
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.CounterName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<WorkspaceViewerProfile> viewerProfiles = await dbContext.WorkspaceViewerProfiles
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.PlatformId)
                .ThenBy(static x => x.PlatformUserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<WorkspaceKeyValueEntry> kvEntries = await dbContext.WorkspaceKeyValueEntries
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.TableName)
                .ThenBy(static x => x.Section)
                .ThenBy(static x => x.Variable)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<WorkspaceSecret> secrets = await dbContext.WorkspaceSecrets
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.SecretName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new WorkspacePortableArchive
            {
                FormatVersion = 1,
                ExportedAtUtc = this._timeProvider.GetUtcNow(),
                SourceWorkspaceId = workspace.Id,
                WorkspaceName = workspace.Name,
                PrimaryPlatformId = workspace.PrimaryPlatformId,
                Commands = [.. commands.Select(static c => new PortableCommandDto
                {
                    Trigger = c.Trigger,
                    AliasesJson = c.AliasesJson,
                    GlobalCooldownSeconds = c.GlobalCooldownSeconds,
                    UserCooldownSeconds = c.UserCooldownSeconds,
                    CostPoints = c.CostPoints,
                    RewardPoints = c.RewardPoints,
                    BoundCounterName = c.BoundCounterName,
                    CounterUsageBehavior = c.CounterUsageBehavior,
                    DeliveryMode = c.DeliveryMode,
                    StreamStatusRequirement = c.StreamStatusRequirement,
                    IsHiddenFromPublicPortal = c.IsHiddenFromPublicPortal,
                    ExposeAsDiscordSlashCommand = c.ExposeAsDiscordSlashCommand,
                    RoutineJson = c.RoutineJson,
                    IsEnabled = c.IsEnabled,
                    Metadata = c.Metadata,
                })],
                Timers = [.. timers.Select(static t => new PortableTimerDto
                {
                    PoolName = t.PoolName,
                    SelectionMode = t.SelectionMode,
                    IntervalSeconds = t.IntervalSeconds,
                    MinChatLinesThreshold = t.MinChatLinesThreshold,
                    StreamStatusRequirement = t.StreamStatusRequirement,
                    GameRestriction = t.GameRestriction,
                    RoutineJson = t.RoutineJson,
                    IsEnabled = t.IsEnabled,
                })],
                PermissionGroups = [.. groups.Select(static g => new PortablePermissionGroupDto
                {
                    OriginalGroupId = g.Id,
                    Name = g.Name,
                    MappedPlatformDesignation = g.MappedPlatformDesignation,
                    MappedOidcGroupClaim = g.MappedOidcGroupClaim,
                })],
                PermissionGrants = [.. grants.Select(static g => new PortablePermissionGrantDto
                {
                    OriginalGroupId = g.GroupId,
                    PermissionKey = g.PermissionKey,
                    State = g.State,
                })],
                Counters = [.. counters.Select(static c => new PortableCounterDto
                {
                    CounterName = c.CounterName,
                    ScopeMode = c.ScopeMode,
                    GameCategory = c.GameCategory,
                    CurrentValue = c.CurrentValue,
                    UpdatedAtUtc = c.UpdatedAtUtc,
                })],
                ViewerProfiles = [.. viewerProfiles.Select(static v => new PortableViewerProfileDto
                {
                    PlatformId = v.PlatformId,
                    PlatformUserId = v.PlatformUserId,
                    LastKnownLogin = v.LastKnownLogin,
                    LastKnownDisplayName = v.LastKnownDisplayName,
                    Points = v.Points,
                    WatchTimeSeconds = v.WatchTimeSeconds,
                    ChatXp = v.ChatXp,
                    UpdatedAtUtc = v.UpdatedAtUtc,
                })],
                KeyValueEntries = [.. kvEntries.Select(static k => new PortableKeyValueEntryDto
                {
                    TableName = k.TableName,
                    Section = k.Section,
                    Variable = k.Variable,
                    Value = k.Value,
                })],
                SecretMetadata = [.. secrets.Select(static s => new PortableSecretMetadataDto
                {
                    SecretName = s.SecretName,
                    AllowedHostPatternsJson = s.AllowedHostPatternsJson,
                    ExpiresAtUtc = s.ExpiresAtUtc,
                })],
            };
        }
    }

    /// <inheritdoc/>
    public async ValueTask ImportAsync(
        Guid workspaceId,
        WorkspacePortableArchive archive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);

        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            bool workspaceExists = await dbContext.Workspaces
                .AnyAsync(x => x.Id == workspaceId, cancellationToken)
                .ConfigureAwait(false);

            if (!workspaceExists)
            {
                throw new KeyNotFoundException($"Target workspace '{workspaceId}' was not found.");
            }

            Dictionary<Guid, Guid> groupIdMap = [];
            foreach (PortablePermissionGroupDto groupDto in archive.PermissionGroups)
            {
                Guid newGroupId = Guid.CreateVersion7();
                groupIdMap[groupDto.OriginalGroupId] = newGroupId;

                _ = dbContext.PermissionGroups.Add(new PermissionGroup
                {
                    Id = newGroupId,
                    WorkspaceId = workspaceId,
                    Name = groupDto.Name,
                    MappedPlatformDesignation = groupDto.MappedPlatformDesignation,
                    MappedOidcGroupClaim = groupDto.MappedOidcGroupClaim,
                });
            }

            foreach (PortablePermissionGrantDto grantDto in archive.PermissionGrants)
            {
                if (groupIdMap.TryGetValue(grantDto.OriginalGroupId, out Guid mappedGroupId))
                {
                    _ = dbContext.PermissionGrants.Add(new PermissionGrant
                    {
                        Id = Guid.CreateVersion7(),
                        GroupId = mappedGroupId,
                        PermissionKey = grantDto.PermissionKey,
                        State = grantDto.State,
                    });
                }
            }

            foreach (PortableCommandDto commandDto in archive.Commands)
            {
                _ = dbContext.CustomCommands.Add(new CustomCommand
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    Trigger = commandDto.Trigger.ToLowerInvariant(),
                    AliasesJson = commandDto.AliasesJson,
                    GlobalCooldownSeconds = commandDto.GlobalCooldownSeconds,
                    UserCooldownSeconds = commandDto.UserCooldownSeconds,
                    CostPoints = commandDto.CostPoints,
                    RewardPoints = commandDto.RewardPoints,
                    BoundCounterName = commandDto.BoundCounterName?.ToLowerInvariant(),
                    CounterUsageBehavior = commandDto.CounterUsageBehavior,
                    DeliveryMode = commandDto.DeliveryMode,
                    StreamStatusRequirement = commandDto.StreamStatusRequirement,
                    IsHiddenFromPublicPortal = commandDto.IsHiddenFromPublicPortal,
                    ExposeAsDiscordSlashCommand = commandDto.ExposeAsDiscordSlashCommand,
                    RoutineJson = commandDto.RoutineJson,
                    IsEnabled = commandDto.IsEnabled,
                    Metadata = commandDto.Metadata,
                });
            }

            foreach (PortableTimerDto timerDto in archive.Timers)
            {
                _ = dbContext.WorkspaceTimers.Add(new WorkspaceTimer
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    PoolName = timerDto.PoolName,
                    SelectionMode = timerDto.SelectionMode,
                    IntervalSeconds = timerDto.IntervalSeconds,
                    MinChatLinesThreshold = timerDto.MinChatLinesThreshold,
                    StreamStatusRequirement = timerDto.StreamStatusRequirement,
                    GameRestriction = timerDto.GameRestriction,
                    RoutineJson = timerDto.RoutineJson,
                    IsEnabled = timerDto.IsEnabled,
                });
            }

            foreach (PortableCounterDto counterDto in archive.Counters)
            {
                _ = dbContext.WorkspaceCounters.Add(new WorkspaceCounter
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    CounterName = counterDto.CounterName.ToLowerInvariant(),
                    ScopeMode = counterDto.ScopeMode,
                    GameCategory = counterDto.GameCategory,
                    CurrentValue = counterDto.CurrentValue,
                    UpdatedAtUtc = counterDto.UpdatedAtUtc,
                });
            }

            foreach (PortableViewerProfileDto profileDto in archive.ViewerProfiles)
            {
                _ = dbContext.WorkspaceViewerProfiles.Add(new WorkspaceViewerProfile
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    PlatformId = profileDto.PlatformId.ToLowerInvariant(),
                    PlatformUserId = profileDto.PlatformUserId,
                    LastKnownLogin = profileDto.LastKnownLogin,
                    LastKnownDisplayName = profileDto.LastKnownDisplayName,
                    Points = profileDto.Points,
                    WatchTimeSeconds = profileDto.WatchTimeSeconds,
                    ChatXp = profileDto.ChatXp,
                    UpdatedAtUtc = profileDto.UpdatedAtUtc,
                });
            }

            foreach (PortableKeyValueEntryDto kvDto in archive.KeyValueEntries)
            {
                _ = dbContext.WorkspaceKeyValueEntries.Add(new WorkspaceKeyValueEntry
                {
                    WorkspaceId = workspaceId,
                    TableName = kvDto.TableName.ToLowerInvariant(),
                    Section = kvDto.Section.ToLowerInvariant(),
                    Variable = kvDto.Variable.ToLowerInvariant(),
                    Value = kvDto.Value,
                });
            }

            _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exports a workspace into a GZip-compressed <c>.sabackup</c> JSON stream.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to export.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous export operation.</returns>
    public async ValueTask ExportGzipAsync(
        Guid workspaceId,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        WorkspacePortableArchive archive = await this.ExportAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        GZipStream gzipStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
        await using (gzipStream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(gzipStream, archive, _serializerOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Imports a workspace from a GZip-compressed <c>.sabackup</c> JSON stream.
    /// </summary>
    /// <param name="workspaceId">The target workspace identifier.</param>
    /// <param name="source">The readable GZipped archive stream.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous import operation.</returns>
    public async ValueTask ImportGzipAsync(
        Guid workspaceId,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        GZipStream gzipStream = new(source, CompressionMode.Decompress, leaveOpen: true);
        await using (gzipStream.ConfigureAwait(false))
        {
            WorkspacePortableArchive archive = await JsonSerializer.DeserializeAsync<WorkspacePortableArchive>(gzipStream, _serializerOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The .sabackup archive payload could not be deserialized.");

            await this.ImportAsync(workspaceId, archive, cancellationToken).ConfigureAwait(false);
        }
    }
}

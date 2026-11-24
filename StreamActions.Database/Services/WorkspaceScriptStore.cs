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
using StreamActions.Core.Commands;
using StreamActions.Core.Scripting;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Database.Services;

/// <summary>
/// EF Core and <see cref="WorkspaceCacheService"/>-backed implementation of <see cref="IWorkspaceScriptStore"/>.
/// </summary>
public sealed class WorkspaceScriptStore : IWorkspaceScriptStore
{
    /// <summary>
    /// Cache domain for workspace VFS scripts.
    /// </summary>
    private const string _cacheDomain = "scripts";

    /// <summary>
    /// Pooled EF Core context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// Workspace-scoped hybrid cache helper.
    /// </summary>
    private readonly WorkspaceCacheService _cache;

    /// <summary>
    /// Dynamic command permission tracker synchronized on script upsert and delete.
    /// </summary>
    private readonly IWorkspaceCommandPermissionTracker _commandPermissionTracker;

    /// <summary>
    /// Time provider for <c>UpdatedAtUtc</c> timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceScriptStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">EF Core context factory.</param>
    /// <param name="cache">Workspace cache service.</param>
    /// <param name="commandPermissionTracker">Command permission tracker.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    public WorkspaceScriptStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        WorkspaceCacheService cache,
        IWorkspaceCommandPermissionTracker commandPermissionTracker,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(commandPermissionTracker);

        this._dbContextFactory = dbContextFactory;
        this._cache = cache;
        this._commandPermissionTracker = commandPermissionTracker;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceScript>> GetScriptsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        List<WorkspaceScript> scripts = await this._cache.GetOrCreateAsync(
            workspaceId,
            _cacheDomain,
            "all",
            async ct =>
            {
                StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                await using (db.ConfigureAwait(false))
                {
                    return await db.WorkspaceScripts
                        .Where(x => x.WorkspaceId == workspaceId)
                        .OrderBy(static x => x.Path)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return [.. scripts.Select(CloneScript)];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceScript>> GetPermanentlyLoadedScriptsAsync(CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<WorkspaceScript> scripts = await db.WorkspaceScripts
                .Where(static x => x.IsEnabled && x.IsPermanentlyLoaded)
                .OrderBy(static x => x.WorkspaceId)
                .ThenBy(static x => x.Path)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. scripts.Select(CloneScript)];
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceScript?> GetByIdAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkspaceScript> scripts = await this.GetScriptsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return scripts.FirstOrDefault(s => s.Id == scriptId);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceScript?> GetByPathAsync(Guid workspaceId, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string normalizedPath = NormalizeVfsPath(path);
        IReadOnlyList<WorkspaceScript> scripts = await this.GetScriptsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return scripts.FirstOrDefault(s => string.Equals(s.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceScript> UpsertAsync(WorkspaceScript script, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        string normalizedPath = NormalizeVfsPath(script.Path);
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        List<ScriptCommandRegistration> normalizedCommands = NormalizeCommandRegistrations(script.RegisteredCommands);
        List<string> previousTriggers = [];

        WorkspaceScript persisted;
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceScript? existing = script.Id != Guid.Empty
                ? await db.WorkspaceScripts
                    .AsTracking()
                    .FirstOrDefaultAsync(x => x.Id == script.Id && x.WorkspaceId == script.WorkspaceId, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            existing ??= await db.WorkspaceScripts
                .AsTracking()
                .FirstOrDefaultAsync(x => x.WorkspaceId == script.WorkspaceId && x.Path == normalizedPath, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                persisted = new WorkspaceScript
                {
                    Id = script.Id == Guid.Empty ? Guid.CreateVersion7() : script.Id,
                    WorkspaceId = script.WorkspaceId,
                    Path = normalizedPath,
                    Code = script.Code ?? string.Empty,
                    IsEnabled = script.IsEnabled,
                    IsPermanentlyLoaded = script.IsPermanentlyLoaded,
                    RegisteredCommands = normalizedCommands,
                    UpdatedAtUtc = now,
                };
                _ = db.WorkspaceScripts.Add(persisted);
            }
            else
            {
                previousTriggers.AddRange(existing.RegisteredCommands.Select(static c => c.Trigger));
                existing.Path = normalizedPath;
                existing.Code = script.Code ?? string.Empty;
                existing.IsEnabled = script.IsEnabled;
                existing.IsPermanentlyLoaded = script.IsPermanentlyLoaded;
                existing.RegisteredCommands = normalizedCommands;
                existing.UpdatedAtUtc = now;
                persisted = existing;
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._cache.InvalidateAsync(script.WorkspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
        await this._commandPermissionTracker
            .SyncScriptCommandPermissionsAsync(script.WorkspaceId, previousTriggers, normalizedCommands, cancellationToken)
            .ConfigureAwait(false);

        return CloneScript(persisted);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default)
    {
        List<string> removedTriggers = [];
        int deleted;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceScript? existing = await db.WorkspaceScripts
                .FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == scriptId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return false;
            }

            removedTriggers.AddRange(existing.RegisteredCommands.Select(static c => c.Trigger));

            deleted = await db.WorkspaceScripts
                .Where(x => x.WorkspaceId == workspaceId && x.Id == scriptId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (deleted <= 0)
        {
            return false;
        }

        await this._cache.InvalidateAsync(workspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
        if (removedTriggers.Count > 0)
        {
            _ = await this._commandPermissionTracker
                .CleanupOrphanedCommandPermissionsAsync(workspaceId, removedTriggers, cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Normalizes and validates a Workspace VFS script path, rejecting empty paths and parent directory traversal (<c>..</c>).
    /// </summary>
    /// <param name="rawPath">The raw VFS path.</param>
    /// <returns>The normalized forward-slash lowercase path.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Workspace VFS paths are normalized to lowercase invariant.")]
    public static string NormalizeVfsPath(string rawPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPath);

        string normalized = rawPath.Trim().Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Script VFS path cannot be empty.", nameof(rawPath));
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException($"Script VFS path '{rawPath}' contains disallowed traversal segments.", nameof(rawPath));
            }
        }

        return string.Join('/', segments).ToLowerInvariant();
    }

    private static List<ScriptCommandRegistration> NormalizeCommandRegistrations(IEnumerable<ScriptCommandRegistration> registrations)
    {
        List<ScriptCommandRegistration> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (ScriptCommandRegistration registration in registrations)
        {
            string trigger = BuiltInCommandRegistry.NormalizeTrigger(registration.Trigger);
            if (string.IsNullOrEmpty(trigger) || !seen.Add(trigger))
            {
                continue;
            }

            normalized.Add(registration with
            {
                Trigger = trigger,
                GlobalCooldownSeconds = Math.Max(0, registration.GlobalCooldownSeconds),
                UserCooldownSeconds = Math.Max(0, registration.UserCooldownSeconds),
            });
        }

        return normalized;
    }

    private static WorkspaceScript CloneScript(WorkspaceScript source) => new()
    {
        Id = source.Id,
        WorkspaceId = source.WorkspaceId,
        Path = source.Path,
        Code = source.Code,
        IsEnabled = source.IsEnabled,
        IsPermanentlyLoaded = source.IsPermanentlyLoaded,
        RegisteredCommands = [.. source.RegisteredCommands],
        UpdatedAtUtc = source.UpdatedAtUtc,
    };
}


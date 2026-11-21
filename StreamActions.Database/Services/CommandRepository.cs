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
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Metadata;
using System.Text.Json;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="ICommandRepository"/> with workspace-scoped caching and event bus invalidation.
/// </summary>
public sealed class CommandRepository : ICommandRepository
{
    /// <summary>
    /// The workspace cache domain for custom commands.
    /// </summary>
    private const string _cacheDomain = "commands";

    /// <summary>
    /// The workspace cache key for the full command list.
    /// </summary>
    private const string _allCommandsCacheKey = "all";

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The workspace cache service.
    /// </summary>
    private readonly WorkspaceCacheService _cache;

    /// <summary>
    /// The event bus used to publish command invalidation events.
    /// </summary>
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandRepository"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="cache">The workspace cache service.</param>
    /// <param name="eventBus">The event bus used to publish invalidation events.</param>
    public CommandRepository(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        WorkspaceCacheService cache,
        IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(eventBus);

        this._dbContextFactory = dbContextFactory;
        this._cache = cache;
        this._eventBus = eventBus;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CustomCommand>> GetCommandsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default) => await this._cache.GetOrCreateAsync(
            workspaceId,
            _cacheDomain,
            _allCommandsCacheKey,
            async ct =>
            {
                StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                await using (db.ConfigureAwait(false))
                {
                    CustomCommand[] commands = await db.CustomCommands
                        .Where(x => x.WorkspaceId == workspaceId)
                        .OrderBy(static x => x.Trigger)
                        .ThenBy(static x => x.Id)
                        .ToArrayAsync(ct)
                        .ConfigureAwait(false);

                    return commands;
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<CustomCommand?> GetByTriggerAsync(
        Guid workspaceId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        string normalizedTrigger = NormalizeTrigger(trigger);
        IReadOnlyList<CustomCommand> commands = await this.GetCommandsAsync(workspaceId, cancellationToken).ConfigureAwait(false);

        foreach (CustomCommand command in commands)
        {
            if (string.Equals(NormalizeTrigger(command.Trigger), normalizedTrigger, StringComparison.Ordinal))
            {
                return command;
            }
        }

        foreach (CustomCommand command in commands)
        {
            foreach (string alias in ParseNormalizedAliases(command.AliasesJson))
            {
                if (string.Equals(alias, normalizedTrigger, StringComparison.Ordinal))
                {
                    return command;
                }
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask UpsertAsync(CustomCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Trigger);

        string normalizedTrigger = NormalizeTrigger(command.Trigger);
        string normalizedAliasesJson = NormalizeAliasesJson(command.AliasesJson);

        command.Trigger = normalizedTrigger;
        command.AliasesJson = normalizedAliasesJson;
        command.Metadata = CloneMetadata(command.Metadata);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            CustomCommand? existing = null;
            if (command.Id != Guid.Empty)
            {
                existing = await db.CustomCommands
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == command.Id && x.WorkspaceId == command.WorkspaceId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            existing ??= await db.CustomCommands
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == command.WorkspaceId && x.Trigger == normalizedTrigger,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                if (command.Id == Guid.Empty)
                {
                    command.Id = Guid.CreateVersion7();
                }

                _ = db.CustomCommands.Add(command);
            }
            else
            {
                command.Id = existing.Id;
                existing.Trigger = normalizedTrigger;
                existing.AliasesJson = normalizedAliasesJson;
                existing.GlobalCooldownSeconds = command.GlobalCooldownSeconds;
                existing.UserCooldownSeconds = command.UserCooldownSeconds;
                existing.CostPoints = command.CostPoints;
                existing.RewardPoints = command.RewardPoints;
                existing.BoundCounterName = command.BoundCounterName;
                existing.CounterUsageBehavior = command.CounterUsageBehavior;
                existing.DeliveryMode = command.DeliveryMode;
                existing.StreamStatusRequirement = command.StreamStatusRequirement;
                existing.IsHiddenFromPublicPortal = command.IsHiddenFromPublicPortal;
                existing.ExposeAsDiscordSlashCommand = command.ExposeAsDiscordSlashCommand;
                existing.RoutineJson = command.RoutineJson;
                existing.IsEnabled = command.IsEnabled;
                existing.Metadata = CloneMetadata(command.Metadata);
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._cache.InvalidateAsync(command.WorkspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
        await this._eventBus.PublishAsync(new CommandsInvalidatedEvent(command.WorkspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(Guid workspaceId, Guid commandId, CancellationToken cancellationToken = default)
    {
        int deleted;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            deleted = await db.CustomCommands
                .Where(x => x.WorkspaceId == workspaceId && x.Id == commandId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (deleted > 0)
        {
            await this._cache.InvalidateAsync(workspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
            await this._eventBus.PublishAsync(new CommandsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Normalizes a command trigger or alias by trimming whitespace and converting to lowercase invariant.
    /// </summary>
    /// <param name="trigger">The raw trigger or alias.</param>
    /// <returns>The normalized trigger string.</returns>
    private static string NormalizeTrigger(string trigger) => trigger.Trim().ToLowerInvariant();

    /// <summary>
    /// Normalizes and deduplicates a JSON array of command aliases.
    /// </summary>
    /// <param name="aliasesJson">The raw serialized aliases JSON string.</param>
    /// <returns>A normalized JSON array string.</returns>
    private static string NormalizeAliasesJson(string? aliasesJson)
    {
        if (string.IsNullOrWhiteSpace(aliasesJson))
        {
            return "[]";
        }

        string?[]? parsed = JsonSerializer.Deserialize<string?[]>(aliasesJson);
        if (parsed is null || parsed.Length == 0)
        {
            return "[]";
        }

        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string? entry in parsed)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            string alias = NormalizeTrigger(entry);
            if (seen.Add(alias))
            {
                normalized.Add(alias);
            }
        }

        return JsonSerializer.Serialize(normalized);
    }

    /// <summary>
    /// Parses and normalizes command aliases from a serialized JSON array, returning an empty sequence if blank or invalid.
    /// </summary>
    /// <param name="aliasesJson">The serialized aliases JSON string.</param>
    /// <returns>A list of normalized non-empty aliases.</returns>
    private static List<string> ParseNormalizedAliases(string? aliasesJson)
    {
        if (string.IsNullOrWhiteSpace(aliasesJson))
        {
            return [];
        }

        try
        {
            string?[]? parsed = JsonSerializer.Deserialize<string?[]>(aliasesJson);
            if (parsed is null || parsed.Length == 0)
            {
                return [];
            }

            List<string> aliases = [];
            foreach (string? entry in parsed)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    aliases.Add(NormalizeTrigger(entry));
                }
            }

            return aliases;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Creates a detached copy of <see cref="CommandMetadata"/> with non-null collection members.
    /// </summary>
    /// <param name="metadata">The source metadata, or <see langword="null"/>.</param>
    /// <returns>A cloned <see cref="CommandMetadata"/> instance.</returns>
    private static CommandMetadata CloneMetadata(CommandMetadata? metadata) => metadata is null
            ? new CommandMetadata()
            : new CommandMetadata
            {
                PlatformChannelOverride = metadata.PlatformChannelOverride,
                AllowCustomOverride = metadata.AllowCustomOverride,
                CounterMutateDesignations = metadata.CounterMutateDesignations,
                CounterMutatePermissionKey = metadata.CounterMutatePermissionKey,
                AllowedPlatforms = metadata.AllowedPlatforms is null ? null : [.. metadata.AllowedPlatforms],
                TargetScope = metadata.TargetScope,
                ExtraSettings = metadata.ExtraSettings is null
                ? []
                : new Dictionary<string, string>(metadata.ExtraSettings, StringComparer.Ordinal),
            };
}

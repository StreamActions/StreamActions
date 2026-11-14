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

using StreamActions.Core.Models.Metadata;

namespace StreamActions.Core.Models;

/// <summary>
/// Represents a portable <c>.sabackup</c> workspace archive for export and import without secret leakage.
/// </summary>
public sealed class WorkspacePortableArchive
{
    /// <summary>
    /// Gets or sets the archive schema version.
    /// </summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the UTC timestamp when the archive was exported.
    /// </summary>
    public DateTimeOffset ExportedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the source workspace identifier.
    /// </summary>
    public Guid SourceWorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the workspace display name.
    /// </summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary platform identifier of the workspace.
    /// </summary>
    public string PrimaryPlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exported custom commands.
    /// </summary>
    public IReadOnlyList<PortableCommandDto> Commands { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported workspace timers.
    /// </summary>
    public IReadOnlyList<PortableTimerDto> Timers { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported permission groups.
    /// </summary>
    public IReadOnlyList<PortablePermissionGroupDto> PermissionGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported permission grants.
    /// </summary>
    public IReadOnlyList<PortablePermissionGrantDto> PermissionGrants { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported workspace counters.
    /// </summary>
    public IReadOnlyList<PortableCounterDto> Counters { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported viewer loyalty profiles.
    /// </summary>
    public IReadOnlyList<PortableViewerProfileDto> ViewerProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported workspace key-value entries.
    /// </summary>
    public IReadOnlyList<PortableKeyValueEntryDto> KeyValueEntries { get; set; } = [];

    /// <summary>
    /// Gets or sets the exported secret metadata (names and host patterns only; never raw secret values).
    /// </summary>
    public IReadOnlyList<PortableSecretMetadataDto> SecretMetadata { get; set; } = [];
}

/// <summary>
/// Portable representation of a custom command in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortableCommandDto
{
    /// <summary>
    /// Gets or sets the command trigger.
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON array of command aliases.
    /// </summary>
    public string AliasesJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the global cooldown in seconds.
    /// </summary>
    public int GlobalCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the per-user cooldown in seconds.
    /// </summary>
    public int UserCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the loyalty points cost to execute the command.
    /// </summary>
    public long CostPoints { get; set; }

    /// <summary>
    /// Gets or sets the loyalty points rewarded upon execution.
    /// </summary>
    public long RewardPoints { get; set; }

    /// <summary>
    /// Gets or sets the optional bound counter name.
    /// </summary>
    public string? BoundCounterName { get; set; }

    /// <summary>
    /// Gets or sets the counter usage behavior.
    /// </summary>
    public CounterUsageBehavior CounterUsageBehavior { get; set; }

    /// <summary>
    /// Gets or sets the delivery mode for command output.
    /// </summary>
    public CommandDeliveryMode DeliveryMode { get; set; }

    /// <summary>
    /// Gets or sets the stream status requirement for execution.
    /// </summary>
    public StreamStatusRequirement StreamStatusRequirement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is hidden from the public portal.
    /// </summary>
    public bool IsHiddenFromPublicPortal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is exposed as a Discord slash command.
    /// </summary>
    public bool ExposeAsDiscordSlashCommand { get; set; }

    /// <summary>
    /// Gets or sets the serialized action routine JSON.
    /// </summary>
    public string RoutineJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the command is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the platform-specific command metadata.
    /// </summary>
    public CommandMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Portable representation of a scheduled timer in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortableTimerDto
{
    /// <summary>
    /// Gets or sets the timer pool name.
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timer selection mode.
    /// </summary>
    public TimerSelectionMode SelectionMode { get; set; }

    /// <summary>
    /// Gets or sets the timer interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the minimum chat lines threshold required before firing.
    /// </summary>
    public int MinChatLinesThreshold { get; set; }

    /// <summary>
    /// Gets or sets the stream status requirement for execution.
    /// </summary>
    public StreamStatusRequirement StreamStatusRequirement { get; set; }

    /// <summary>
    /// Gets or sets the optional game or category restriction.
    /// </summary>
    public string? GameRestriction { get; set; }

    /// <summary>
    /// Gets or sets the serialized action routine JSON.
    /// </summary>
    public string RoutineJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the timer is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Portable representation of a permission group in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortablePermissionGroupDto
{
    /// <summary>
    /// Gets or sets the original group identifier for mapping grants during import.
    /// </summary>
    public Guid OriginalGroupId { get; set; }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional mapped platform role or designation.
    /// </summary>
    public string? MappedPlatformDesignation { get; set; }

    /// <summary>
    /// Gets or sets the optional mapped OIDC group claim value.
    /// </summary>
    public string? MappedOidcGroupClaim { get; set; }
}

/// <summary>
/// Portable representation of a permission grant in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortablePermissionGrantDto
{
    /// <summary>
    /// Gets or sets the original group identifier referenced by <see cref="PortablePermissionGroupDto.OriginalGroupId"/>.
    /// </summary>
    public Guid OriginalGroupId { get; set; }

    /// <summary>
    /// Gets or sets the permission key.
    /// </summary>
    public string PermissionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tri-state permission rule.
    /// </summary>
    public PermissionState State { get; set; }
}

/// <summary>
/// Portable representation of a workspace counter in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortableCounterDto
{
    /// <summary>
    /// Gets or sets the counter name.
    /// </summary>
    public string CounterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the counter scope mode.
    /// </summary>
    public CounterScopeMode ScopeMode { get; set; }

    /// <summary>
    /// Gets or sets the game or category name for per-game counters.
    /// </summary>
    public string GameCategory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current counter value.
    /// </summary>
    public long CurrentValue { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the counter was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Portable representation of a viewer profile in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortableViewerProfileDto
{
    /// <summary>
    /// Gets or sets the platform identifier.
    /// </summary>
    public string PlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the platform-specific user identifier.
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last known login name.
    /// </summary>
    public string LastKnownLogin { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last known display name.
    /// </summary>
    public string LastKnownDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the viewer's loyalty points balance.
    /// </summary>
    public long Points { get; set; }

    /// <summary>
    /// Gets or sets the viewer's accumulated watch time in seconds.
    /// </summary>
    public long WatchTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the viewer's accumulated chat XP.
    /// </summary>
    public long ChatXp { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Portable representation of a key-value entry in a <see cref="WorkspacePortableArchive"/>.
/// </summary>
public sealed class PortableKeyValueEntryDto
{
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the section name.
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variable name.
    /// </summary>
    public string Variable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stored string value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Portable metadata for a workspace secret, intentionally excluding raw or encrypted secret values.
/// </summary>
public sealed class PortableSecretMetadataDto
{
    /// <summary>
    /// Gets or sets the secret name.
    /// </summary>
    public string SecretName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON array of allowed host patterns.
    /// </summary>
    public string AllowedHostPatternsJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional UTC expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

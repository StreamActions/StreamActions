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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace StreamActions.Core.Commands;

/// <summary>
/// Thread-safe registry that discovers <see cref="IBuiltInCommandHandler"/> implementations and <see cref="CommandAttribute"/> metadata and resolves commands by identifier, sub-command trigger, or workspace-configured top-level alias.
/// </summary>
public sealed class BuiltInCommandRegistry : IBuiltInCommandRegistry
{
    /// <summary>
    /// Logical table name in <see cref="IWorkspaceKeyValueStore"/> storing built-in command settings.
    /// </summary>
    public const string KeyValueTable = "commands";

    /// <summary>
    /// Section name in <see cref="IWorkspaceKeyValueStore"/> storing top-level alias exposure toggles.
    /// </summary>
    public const string TopLevelAliasesSection = "builtin_top_level_aliases";

    /// <summary>
    /// Variable key in <see cref="TopLevelAliasesSection"/> controlling the workspace-wide default for top-level <c>!bot</c> sub-command aliases.
    /// </summary>
    public const string AllCommandsAliasKey = "__all__";

    /// <summary>
    /// Optional workspace key-value store for per-workspace top-level alias exposure settings.
    /// </summary>
    private readonly IWorkspaceKeyValueStore? _keyValueStore;

    /// <summary>
    /// Optional localizer used to resolve localized top-level triggers when top-level aliases are enabled.
    /// </summary>
    private readonly IStreamActionsLocalizer? _localizer;

    /// <summary>
    /// Registered built-in commands keyed by normalized canonical command identifier.
    /// </summary>
    private readonly ConcurrentDictionary<string, BuiltInCommandRegistration> _byCommandId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lookup index mapping normalized sub-command triggers to canonical command identifiers.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _bySubTrigger = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lookup index mapping normalized optional top-level triggers to canonical command identifiers.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _byOptionalTopLevelTrigger = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lookup index mapping all candidate top-level triggers (when explicitly enabled) to canonical command identifiers.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _byCandidateTopLevelTrigger = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new empty <see cref="BuiltInCommandRegistry"/>.
    /// </summary>
    public BuiltInCommandRegistry()
        : this([], null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltInCommandRegistry"/> class with discovered handlers and optional stores.
    /// </summary>
    /// <param name="handlers">The discovered <see cref="IBuiltInCommandHandler"/> instances.</param>
    /// <param name="keyValueStore">Optional workspace key-value store for top-level alias exposure toggles.</param>
    /// <param name="localizer">Optional localizer for resolving localized top-level aliases.</param>
    public BuiltInCommandRegistry(
        IEnumerable<IBuiltInCommandHandler> handlers,
        IWorkspaceKeyValueStore? keyValueStore = null,
        IStreamActionsLocalizer? localizer = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        this._keyValueStore = keyValueStore;
        this._localizer = localizer;

        foreach (IBuiltInCommandHandler handler in handlers)
        {
            if (handler is not null)
            {
                this.Register(handler);
            }
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command identifiers and triggers to lowercase invariant.")]
    public void Register(IBuiltInCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.CommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.DefaultSubCommandTrigger);

        CommandAttribute? attribute = handler.GetType().GetCustomAttribute<CommandAttribute>(inherit: false);

        string commandId = handler.CommandId.Trim().ToLowerInvariant();
        string defaultSubTrigger = NormalizeTrigger(handler.DefaultSubCommandTrigger);
        if (string.IsNullOrEmpty(defaultSubTrigger))
        {
            throw new ArgumentException("DefaultSubCommandTrigger cannot be empty after normalization.", nameof(handler));
        }

        List<string> subTriggers = [];
        HashSet<string> subSeen = new(StringComparer.OrdinalIgnoreCase);
        AddUniqueNormalizedTrigger(defaultSubTrigger, subTriggers, subSeen);

        if (attribute is not null)
        {
            AddUniqueNormalizedTrigger(attribute.Trigger, subTriggers, subSeen);
            foreach (string alias in attribute.Aliases)
            {
                AddUniqueNormalizedTrigger(alias, subTriggers, subSeen);
            }
        }

        List<string> optionalTopLevel = [];
        HashSet<string> optionalSeen = new(StringComparer.OrdinalIgnoreCase);
        if (handler.OptionalTopLevelTriggers is not null)
        {
            foreach (string trigger in handler.OptionalTopLevelTriggers)
            {
                AddUniqueNormalizedTrigger(trigger, optionalTopLevel, optionalSeen);
            }
        }

        List<string> allTopLevelCandidates = [];
        HashSet<string> candidateSeen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string trigger in optionalTopLevel)
        {
            AddUniqueNormalizedTrigger(trigger, allTopLevelCandidates, candidateSeen);
        }

        foreach (string trigger in subTriggers)
        {
            AddUniqueNormalizedTrigger(trigger, allTopLevelCandidates, candidateSeen);
        }

        bool allowCustomOverride = handler.AllowCustomOverride || (attribute?.AllowCustomOverride ?? false);
        string? permissionKey = !string.IsNullOrWhiteSpace(handler.RequiredPermissionKey)
            ? handler.RequiredPermissionKey.Trim()
            : !string.IsNullOrWhiteSpace(attribute?.DefaultPermissionKey)
                ? attribute.DefaultPermissionKey.Trim()
                : null;
        int globalCooldown = handler.GlobalCooldownSeconds > 0
            ? handler.GlobalCooldownSeconds
            : Math.Max(0, attribute?.GlobalCooldownSeconds ?? 0);
        int userCooldown = handler.UserCooldownSeconds > 0
            ? handler.UserCooldownSeconds
            : Math.Max(0, attribute?.UserCooldownSeconds ?? 0);

        BuiltInCommandRegistration registration = new(
            handler,
            commandId,
            defaultSubTrigger,
            subTriggers.AsReadOnly(),
            optionalTopLevel.AsReadOnly(),
            allTopLevelCandidates.AsReadOnly(),
            allowCustomOverride,
            permissionKey,
            globalCooldown,
            userCooldown,
            attribute?.Description);

        this._byCommandId[commandId] = registration;

        foreach (string trigger in registration.SubCommandTriggers)
        {
            this._bySubTrigger[trigger] = commandId;
        }

        foreach (string trigger in registration.OptionalTopLevelTriggers)
        {
            this._byOptionalTopLevelTrigger[trigger] = commandId;
        }

        foreach (string trigger in registration.AllTopLevelCandidateTriggers)
        {
            this._byCandidateTopLevelTrigger[trigger] = commandId;
        }
    }

    /// <inheritdoc/>
    public BuiltInCommandRegistration? ResolveByCommandId(string commandId) => string.IsNullOrWhiteSpace(commandId) ? null : this._byCommandId.GetValueOrDefault(commandId.Trim());

    /// <inheritdoc/>
    public BuiltInCommandRegistration? ResolveBySubCommandTrigger(string subCommandTrigger)
    {
        string normalized = NormalizeTrigger(subCommandTrigger);
        return string.IsNullOrEmpty(normalized)
            ? null
            : this._bySubTrigger.TryGetValue(normalized, out string? commandId)
            ? this._byCommandId.GetValueOrDefault(commandId)
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<BuiltInCommandRegistration?> ResolveByTopLevelTriggerAsync(
        Guid workspaceId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeTrigger(trigger);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (this._byOptionalTopLevelTrigger.TryGetValue(normalized, out string? optionalCommandId)
            && this._byCommandId.TryGetValue(optionalCommandId, out BuiltInCommandRegistration? optionalReg))
        {
            bool? explicitToggle = await this.GetExplicitTopLevelToggleAsync(workspaceId, optionalReg.CommandId, cancellationToken).ConfigureAwait(false);
            if (explicitToggle != false)
            {
                return optionalReg;
            }
        }

        if (this._byCandidateTopLevelTrigger.TryGetValue(normalized, out string? candidateCommandId)
            && this._byCommandId.TryGetValue(candidateCommandId, out BuiltInCommandRegistration? candidateReg))
        {
            if (await this.IsTopLevelAliasEnabledAsync(workspaceId, candidateReg.CommandId, cancellationToken).ConfigureAwait(false))
            {
                return candidateReg;
            }
        }

        if (this._localizer is not null)
        {
            string? localizedCommandId = await this._localizer
                .ResolveCommandIdAsync(workspaceId, normalized, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(localizedCommandId)
                && this._byCommandId.TryGetValue(localizedCommandId.Trim(), out BuiltInCommandRegistration? localizedReg)
                && await this.IsTopLevelAliasEnabledAsync(workspaceId, localizedReg.CommandId, cancellationToken).ConfigureAwait(false))
            {
                return localizedReg;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command identifiers to lowercase invariant.")]
    public async ValueTask SetTopLevelAliasEnabledAsync(
        Guid workspaceId,
        string commandId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        if (this._keyValueStore is null)
        {
            throw new InvalidOperationException("IWorkspaceKeyValueStore is required to persist top-level alias exposure settings.");
        }

        string normalizedKey = commandId.Trim().ToLowerInvariant();
        await this._keyValueStore.SetAsync(
            workspaceId,
            KeyValueTable,
            TopLevelAliasesSection,
            normalizedKey,
            enabled,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command identifiers to lowercase invariant.")]
    public async ValueTask<bool> IsTopLevelAliasEnabledAsync(
        Guid workspaceId,
        string commandId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return false;
        }

        string normalizedId = commandId.Trim().ToLowerInvariant();
        bool? explicitToggle = await this.GetExplicitTopLevelToggleAsync(workspaceId, normalizedId, cancellationToken).ConfigureAwait(false);
        return explicitToggle ?? (this._byCommandId.TryGetValue(normalizedId, out BuiltInCommandRegistration? registration)
            && registration.OptionalTopLevelTriggers.Count > 0);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<BuiltInCommandRegistration> GetAll() =>
        [.. this._byCommandId.Values];

    /// <summary>
    /// Normalizes a trigger string by trimming whitespace, stripping leading <c>!</c> prefixes, and converting to lowercase invariant.
    /// </summary>
    /// <param name="rawTrigger">The raw trigger text.</param>
    /// <returns>The normalized trigger, or <see cref="string.Empty"/> when blank.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command triggers to lowercase invariant.")]
    internal static string NormalizeTrigger(string? rawTrigger) =>
        string.IsNullOrWhiteSpace(rawTrigger)
            ? string.Empty
            : rawTrigger.Trim().TrimStart('!').Trim().ToLowerInvariant();

    /// <summary>
    /// Checks workspace key-value settings for an explicit per-command or workspace-wide top-level alias toggle.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="normalizedCommandId">The normalized canonical command identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> or <see langword="false"/> if explicitly configured; otherwise, <see langword="null"/>.</returns>
    private async ValueTask<bool?> GetExplicitTopLevelToggleAsync(
        Guid workspaceId,
        string normalizedCommandId,
        CancellationToken cancellationToken)
    {
        if (this._keyValueStore is null)
        {
            return null;
        }

        string? perCommand = await this._keyValueStore
            .GetStringAsync(workspaceId, KeyValueTable, TopLevelAliasesSection, normalizedCommandId, defaultValue: null, cancellationToken)
            .ConfigureAwait(false);
        if (bool.TryParse(perCommand, out bool perCommandBool))
        {
            return perCommandBool;
        }

        string? workspaceDefault = await this._keyValueStore
            .GetStringAsync(workspaceId, KeyValueTable, TopLevelAliasesSection, AllCommandsAliasKey, defaultValue: null, cancellationToken)
            .ConfigureAwait(false);
        return bool.TryParse(workspaceDefault, out bool workspaceDefaultBool) ? workspaceDefaultBool : null;
    }

    /// <summary>
    /// Adds a normalized trigger to <paramref name="target"/> if non-empty and not already present in <paramref name="seen"/>.
    /// </summary>
    /// <param name="rawTrigger">The candidate raw trigger.</param>
    /// <param name="target">The destination list.</param>
    /// <param name="seen">The deduplication set.</param>
    private static void AddUniqueNormalizedTrigger(string? rawTrigger, List<string> target, HashSet<string> seen)
    {
        string normalized = NormalizeTrigger(rawTrigger);
        if (!string.IsNullOrEmpty(normalized) && seen.Add(normalized))
        {
            target.Add(normalized);
        }
    }
}

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
using StreamActions.Core.Models;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Services;

/// <summary>
/// Aggregates required platform OAuth scopes from plugins and <see cref="IPlatformScopeRequirement"/> registrations
/// and detects scope drift against granted token scopes.
/// </summary>
public sealed class ScopeDriftDetector : IScopeDriftDetector
{
    /// <summary>
    /// The plugin manager supplying registered plugins.
    /// </summary>
    private readonly IPluginManager _pluginManager;

    /// <summary>
    /// Registered platform scope requirement contributors.
    /// </summary>
    private readonly IReadOnlyList<IPlatformScopeRequirement> _scopeRequirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeDriftDetector"/> class.
    /// </summary>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <param name="scopeRequirements">The registered platform scope requirements.</param>
    public ScopeDriftDetector(
        IPluginManager pluginManager,
        IEnumerable<IPlatformScopeRequirement> scopeRequirements)
    {
        ArgumentNullException.ThrowIfNull(pluginManager);
        ArgumentNullException.ThrowIfNull(scopeRequirements);

        this._pluginManager = pluginManager;
        this._scopeRequirements = [.. scopeRequirements];
    }

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    public IReadOnlyList<string> GetRequiredScopes(
        string platformId,
        IEnumerable<string>? additionalRequiredScopes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = platformId.Trim().ToLowerInvariant();
        List<string> requiredScopes = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (IPlugin plugin in this._pluginManager.Plugins)
        {
            if (!string.IsNullOrWhiteSpace(plugin.RequiredPlatformId)
                && !string.Equals(plugin.RequiredPlatformId.Trim(), normalizedPlatformId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (KeyValuePair<string, IReadOnlyCollection<string>> entry in plugin.RequiredPlatformScopes)
            {
                if (string.Equals(entry.Key.Trim(), normalizedPlatformId, StringComparison.OrdinalIgnoreCase))
                {
                    AddUniqueScopes(entry.Value, requiredScopes, seen);
                }
            }
        }

        foreach (IPlatformScopeRequirement requirement in this._scopeRequirements)
        {
            AddUniqueScopes(requirement.GetRequiredScopes(normalizedPlatformId), requiredScopes, seen);
        }

        AddUniqueScopes(additionalRequiredScopes, requiredScopes, seen);
        return requiredScopes;
    }

    /// <inheritdoc />
    public ScopeDriftReport DetectDrift(
        string platformId,
        PlatformTokenPayload? tokenPayload,
        IEnumerable<string>? additionalRequiredScopes = null) =>
        this.DetectDrift(platformId, tokenPayload?.Scopes, additionalRequiredScopes);

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    public ScopeDriftReport DetectDrift(
        string platformId,
        IReadOnlyCollection<string>? grantedScopes,
        IEnumerable<string>? additionalRequiredScopes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = platformId.Trim().ToLowerInvariant();
        IReadOnlyList<string> requiredScopes = this.GetRequiredScopes(normalizedPlatformId, additionalRequiredScopes);

        List<string> normalizedGranted = [];
        HashSet<string> grantedSet = new(StringComparer.OrdinalIgnoreCase);
        AddUniqueScopes(grantedScopes, normalizedGranted, grantedSet);

        List<string> missingScopes = [];
        foreach (string requiredScope in requiredScopes)
        {
            if (!grantedSet.Contains(requiredScope))
            {
                missingScopes.Add(requiredScope);
            }
        }

        List<string> upgradeUnion = [.. normalizedGranted];
        HashSet<string> unionSet = new(grantedSet, StringComparer.OrdinalIgnoreCase);
        AddUniqueScopes(missingScopes, upgradeUnion, unionSet);

        return new ScopeDriftReport(
            normalizedPlatformId,
            normalizedGranted,
            requiredScopes,
            missingScopes,
            upgradeUnion);
    }

    /// <summary>
    /// Appends non-empty trimmed scopes to <paramref name="destination"/> while deduplicating case-insensitively.
    /// </summary>
    /// <param name="source">The candidate scope strings.</param>
    /// <param name="destination">The ordered destination list.</param>
    /// <param name="seen">The case-insensitive uniqueness set.</param>
    private static void AddUniqueScopes(
        IEnumerable<string>? source,
        List<string> destination,
        HashSet<string> seen)
    {
        if (source is null)
        {
            return;
        }

        foreach (string rawScope in source)
        {
            if (string.IsNullOrWhiteSpace(rawScope))
            {
                continue;
            }

            string trimmed = rawScope.Trim();
            if (seen.Add(trimmed))
            {
                destination.Add(trimmed);
            }
        }
    }
}

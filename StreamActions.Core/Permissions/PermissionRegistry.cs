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

using System.Collections.Concurrent;

namespace StreamActions.Core.Permissions;

/// <summary>
/// Thread-safe default implementation of <see cref="IPermissionRegistry"/> pre-seeded with built-in permissions.
/// </summary>
public sealed class PermissionRegistry : IPermissionRegistry
{
    /// <summary>
    /// Registered permission descriptors keyed by permission key (case-insensitive).
    /// </summary>
    private readonly ConcurrentDictionary<string, PermissionDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRegistry"/> class and seeds built-in permissions.
    /// </summary>
    public PermissionRegistry() => this.SeedBuiltInPermissions();

    /// <inheritdoc />
    public void Register(PermissionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Category);

        this._descriptors[descriptor.Key] = descriptor;
    }

    /// <inheritdoc />
    public bool Unregister(string key) => !string.IsNullOrWhiteSpace(key) && this._descriptors.TryRemove(key, out _);

    /// <inheritdoc />
    public PermissionDescriptor? GetByKey(string key) => string.IsNullOrWhiteSpace(key)
            ? null
            : this._descriptors.TryGetValue(key, out PermissionDescriptor? descriptor) ? descriptor : null;

    /// <inheritdoc />
    public IReadOnlyList<PermissionDescriptor> GetByCategory(string category) => string.IsNullOrWhiteSpace(category)
            ? []
            : [.. this._descriptors.Values
            .Where(d => string.Equals(d.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc />
    public IReadOnlyList<PermissionDescriptor> GetAll() => [.. this._descriptors.Values
            .OrderBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Key, StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc />
    public IReadOnlyList<string> GetCategories() => [.. this._descriptors.Values
            .Select(d => d.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Populates the registry with all built-in feature, UI, moderation, and action permissions.
    /// </summary>
    private void SeedBuiltInPermissions()
    {
        PermissionDescriptor[] builtIns =
        [
            new(BuiltInPermissionKeys.FeatureCustomCommands, PermissionCategories.Features, "Custom Commands", "Enables custom chat commands within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureTimers, PermissionCategories.Features, "Timers", "Enables scheduled chat timers within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureQuotes, PermissionCategories.Features, "Quotes", "Enables channel quotes within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureCounters, PermissionCategories.Features, "Counters", "Enables channel counters within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureModeration, PermissionCategories.Features, "Moderation", "Enables automated chat moderation within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureLoyalty, PermissionCategories.Features, "Loyalty", "Enables viewer loyalty points within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureOverlays, PermissionCategories.Features, "Overlays", "Enables browser source overlays within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureSoundboard, PermissionCategories.Features, "Soundboard", "Enables soundboard playback within a workspace.", true),
            new(BuiltInPermissionKeys.FeatureAlerts, PermissionCategories.Features, "Alerts", "Enables stream alert triggers within a workspace.", true),
            new(BuiltInPermissionKeys.FeaturePlugins, PermissionCategories.Features, "Plugins", "Enables third-party plugin execution within a workspace.", true),

            new(BuiltInPermissionKeys.UiDashboardView, PermissionCategories.Ui, "View Dashboard", "Allows viewing the workspace dashboard in the web panel."),
            new(BuiltInPermissionKeys.UiCommandsView, PermissionCategories.Ui, "View Commands", "Allows viewing custom commands in the web panel."),
            new(BuiltInPermissionKeys.UiCommandsEdit, PermissionCategories.Ui, "Edit Commands", "Allows creating, editing, and deleting custom commands in the web panel."),
            new(BuiltInPermissionKeys.UiTimersManage, PermissionCategories.Ui, "Manage Timers", "Allows managing timers in the web panel."),
            new(BuiltInPermissionKeys.UiQuotesManage, PermissionCategories.Ui, "Manage Quotes", "Allows managing quotes in the web panel."),
            new(BuiltInPermissionKeys.UiCountersManage, PermissionCategories.Ui, "Manage Counters", "Allows managing counters in the web panel."),
            new(BuiltInPermissionKeys.UiModerationManage, PermissionCategories.Ui, "Manage Moderation", "Allows managing moderation settings and filters in the web panel."),
            new(BuiltInPermissionKeys.UiPermissionsManage, PermissionCategories.Ui, "Manage Permissions", "Allows managing workspace permission groups and overrides in the web panel."),
            new(BuiltInPermissionKeys.UiSettingsManage, PermissionCategories.Ui, "Manage Settings", "Allows managing general workspace settings in the web panel."),
            new(BuiltInPermissionKeys.UiAuditLogView, PermissionCategories.Ui, "View Audit Log", "Allows viewing the workspace audit log in the web panel."),

            new(BuiltInPermissionKeys.ModerationTimeout, PermissionCategories.Moderation, "Timeout Users", "Allows timing out users in chat."),
            new(BuiltInPermissionKeys.ModerationBan, PermissionCategories.Moderation, "Ban Users", "Allows banning users in chat."),
            new(BuiltInPermissionKeys.ModerationUnban, PermissionCategories.Moderation, "Unban Users", "Allows unbanning users in chat."),
            new(BuiltInPermissionKeys.ModerationDeleteMessage, PermissionCategories.Moderation, "Delete Messages", "Allows deleting individual chat messages."),
            new(BuiltInPermissionKeys.ModerationBypassLinks, PermissionCategories.Moderation, "Bypass Link Filter", "Allows bypassing automated link filters in chat."),
            new(BuiltInPermissionKeys.ModerationBypassCaps, PermissionCategories.Moderation, "Bypass Caps Filter", "Allows bypassing automated caps filters in chat."),
            new(BuiltInPermissionKeys.ModerationBypassSpam, PermissionCategories.Moderation, "Bypass Spam Filter", "Allows bypassing automated spam filters in chat."),
            new(BuiltInPermissionKeys.ModerationManageFilters, PermissionCategories.Moderation, "Manage Filters", "Allows modifying moderation filters via chat commands."),

            new(BuiltInPermissionKeys.ActionRunCommand, PermissionCategories.Actions, "Run Command", "Allows executing custom chat commands."),
            new(BuiltInPermissionKeys.ActionManageQuotes, PermissionCategories.Actions, "Manage Quotes", "Allows adding, editing, or removing quotes via chat actions."),
            new(BuiltInPermissionKeys.ActionManageCounters, PermissionCategories.Actions, "Manage Counters", "Allows incrementing or modifying counters via chat actions."),
            new(BuiltInPermissionKeys.ActionTriggerSoundboard, PermissionCategories.Actions, "Trigger Soundboard", "Allows triggering soundboard clips."),
            new(BuiltInPermissionKeys.ActionTriggerAlert, PermissionCategories.Actions, "Trigger Alert", "Allows triggering stream alerts."),
        ];

        foreach (PermissionDescriptor descriptor in builtIns)
        {
            this._descriptors[descriptor.Key] = descriptor;
        }
    }
}

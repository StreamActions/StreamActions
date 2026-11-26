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

using StreamActions.Core.Models;
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

        if (descriptor.DefaultState == PermissionState.Deny)
        {
            throw new ArgumentException("Permission descriptor default state can only be Inherit or Allow and must never be Deny.", nameof(descriptor));
        }

        if (descriptor.IsDangerousGlobalFeature && (!descriptor.IsGloballyGated || descriptor.DefaultState == PermissionState.Allow))
        {
            throw new ArgumentException("Dangerous global features must be globally gated and must not default to Allow.", nameof(descriptor));
        }

        this._descriptors[descriptor.Key] = descriptor;
    }

    /// <inheritdoc />
    public bool Unregister(string key) => !string.IsNullOrWhiteSpace(key) && this._descriptors.TryRemove(key, out _);

    /// <inheritdoc />
    public PermissionDescriptor? GetByKey(string key) => string.IsNullOrWhiteSpace(key)
        ? null
        : this._descriptors.TryGetValue(key.Trim(), out PermissionDescriptor? descriptor)
            ? descriptor
            : null;

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
        const PlatformUserDesignations broadcasterAndModerator = PlatformUserDesignations.Broadcaster | PlatformUserDesignations.Moderator;

        PermissionDescriptor[] builtIns =
        [
            new(BuiltInPermissionKeys.FeatureCustomCommands, PermissionCategories.Features, "Custom Commands", "Enables custom chat commands within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureTimers, PermissionCategories.Features, "Timers", "Enables scheduled chat timers within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureQuotes, PermissionCategories.Features, "Quotes", "Enables channel quotes within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureCounters, PermissionCategories.Features, "Counters", "Enables channel counters within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureModeration, PermissionCategories.Features, "Moderation", "Enables automated chat moderation within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureLoyalty, PermissionCategories.Features, "Loyalty", "Enables viewer loyalty points within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureOverlays, PermissionCategories.Features, "Overlays", "Enables browser source overlays within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureSoundboard, PermissionCategories.Features, "Soundboard", "Enables soundboard playback within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureAlerts, PermissionCategories.Features, "Alerts", "Enables stream alert triggers within a workspace.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeaturePublicAnonymousAccess, PermissionCategories.Features, "Public Portal Anonymous Access", "Enables anonymous viewer access to the public workspace portal.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeaturePublicViewerLogin, PermissionCategories.Features, "Public Portal Viewer Login", "Enables viewer login on the public workspace portal.", IsGloballyGated: true),
            new(BuiltInPermissionKeys.FeatureAddonDiscord, PermissionCategories.Features, "Discord Add-On", "Enables connecting the Discord add-on platform adapter within a workspace.", IsGloballyGated: true),

            new(BuiltInPermissionKeys.FeatureScriptingClearScript, PermissionCategories.Features, "ClearScript V8 Scripting", "Enables user-authored JavaScript execution in the ClearScript V8 sandbox within a workspace.", IsGloballyGated: true, IsDangerousGlobalFeature: true),
            new(BuiltInPermissionKeys.FeatureScriptingClearScriptPersistent, PermissionCategories.Features, "Persistent ClearScript Resident Scripts", "Allows workspace ClearScript scripts to remain permanently loaded in memory across stream sessions.", IsGloballyGated: true, IsDangerousGlobalFeature: true),
            new(BuiltInPermissionKeys.FeatureActionBlocksOfflineSchedule, PermissionCategories.Features, "Action Blocks Off-Stream Schedules", "Allows Timer and Cron Action Block profiles to execute after the 30-minute post-stream grace period expires.", IsGloballyGated: true, IsDangerousGlobalFeature: true),
            new(BuiltInPermissionKeys.FeaturePlugins, PermissionCategories.Features, "Plugins", "Enables third-party plugin execution within a workspace.", IsGloballyGated: true, IsDangerousGlobalFeature: true),
            new(BuiltInPermissionKeys.FeatureCustomOAuthApp, PermissionCategories.Features, "Custom OAuth Application", "Enables configuring and using custom per-workspace platform OAuth application credentials.", IsGloballyGated: true, IsDangerousGlobalFeature: true),
            new(BuiltInPermissionKeys.FeatureCustomBotAccount, PermissionCategories.Features, "Custom Bot Account", "Enables connecting and using a custom per-workspace bot chat account.", IsGloballyGated: true, IsDangerousGlobalFeature: true),
            new(BuiltInPermissionKeys.FeatureCustomOidcProvider, PermissionCategories.Features, "Custom OIDC Provider", "Enables configuring and using custom per-workspace external OIDC identity providers.", IsGloballyGated: true, IsDangerousGlobalFeature: true),

            new(BuiltInPermissionKeys.UiPanelAccess, PermissionCategories.Ui, "Panel Access", "Allows accessing the workspace web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiDashboardView, PermissionCategories.Ui, "View Dashboard", "Allows viewing the workspace dashboard in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiCommandsView, PermissionCategories.Ui, "View Commands", "Allows viewing custom commands in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiCommandsEdit, PermissionCategories.Ui, "Edit Commands", "Allows creating, editing, and deleting custom commands in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiCommandsScriptEdit, PermissionCategories.Ui, "Edit Command Scripts", "Allows editing custom command script routines in the web panel.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),
            new(BuiltInPermissionKeys.UiSecretsView, PermissionCategories.Ui, "View Secrets", "Allows viewing workspace secrets metadata in the web panel.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),
            new(BuiltInPermissionKeys.UiSecretsManage, PermissionCategories.Ui, "Manage Secrets", "Allows creating, updating, and deleting workspace secrets in the web panel.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),
            new(BuiltInPermissionKeys.UiTimersManage, PermissionCategories.Ui, "Manage Timers", "Allows managing timers in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiQuotesManage, PermissionCategories.Ui, "Manage Quotes", "Allows managing quotes in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiCountersManage, PermissionCategories.Ui, "Manage Counters", "Allows managing counters in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiModerationManage, PermissionCategories.Ui, "Manage Moderation", "Allows managing moderation settings and filters in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiPermissionsManage, PermissionCategories.Ui, "Manage Permissions", "Allows managing workspace permission groups and overrides in the web panel.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),
            new(BuiltInPermissionKeys.UiSettingsManage, PermissionCategories.Ui, "Manage Settings", "Allows managing general workspace settings in the web panel.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),
            new(BuiltInPermissionKeys.UiAuditLogView, PermissionCategories.Ui, "View Audit Log", "Allows viewing the workspace audit log in the web panel.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiLoyaltyManage, PermissionCategories.Ui, "Manage Loyalty", "Allows managing viewer loyalty points and settings in the web panel and chat templates.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiCommandsActionsDiscordRoles, PermissionCategories.Ui, "Manage Discord Role Actions", "Allows creating or editing command actions that assign or remove Discord roles.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),
            new(BuiltInPermissionKeys.UiTaskManagerView, PermissionCategories.Ui, "View Task Manager", "Allows viewing running Action Block flows and loaded ClearScript V8 instances in the workspace Task Manager.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.UiTaskManagerTerminate, PermissionCategories.Ui, "Terminate Tasks & Scripts", "Allows terminating running Action Block flows and unloading ClearScript V8 script instances in the workspace Task Manager.", DefaultAllowedDesignations: PlatformUserDesignations.Broadcaster),

            new(BuiltInPermissionKeys.ModerationTimeout, PermissionCategories.Moderation, "Timeout Users", "Allows timing out users in chat.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationBan, PermissionCategories.Moderation, "Ban Users", "Allows banning users in chat.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationUnban, PermissionCategories.Moderation, "Unban Users", "Allows unbanning users in chat.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationDeleteMessage, PermissionCategories.Moderation, "Delete Messages", "Allows deleting individual chat messages.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationBypassLinks, PermissionCategories.Moderation, "Bypass Link Filter", "Allows bypassing automated link filters in chat.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationBypassCaps, PermissionCategories.Moderation, "Bypass Caps Filter", "Allows bypassing automated caps filters in chat.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationBypassSpam, PermissionCategories.Moderation, "Bypass Spam Filter", "Allows bypassing automated spam filters in chat.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ModerationManageFilters, PermissionCategories.Moderation, "Manage Filters", "Allows modifying moderation filters via chat commands.", DefaultAllowedDesignations: broadcasterAndModerator),

            new(BuiltInPermissionKeys.ActionRunCommand, PermissionCategories.Actions, "Run Command", "Allows executing custom chat commands.", DefaultAllowedDesignations: PlatformUserDesignations.Everyone, DefaultState: PermissionState.Allow),
            new(BuiltInPermissionKeys.ActionManageQuotes, PermissionCategories.Actions, "Manage Quotes", "Allows adding, editing, or removing quotes via chat actions.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ActionManageCounters, PermissionCategories.Actions, "Manage Counters", "Allows incrementing or modifying counters via chat actions.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ActionTriggerSoundboard, PermissionCategories.Actions, "Trigger Soundboard", "Allows triggering soundboard clips.", DefaultAllowedDesignations: broadcasterAndModerator),
            new(BuiltInPermissionKeys.ActionTriggerAlert, PermissionCategories.Actions, "Trigger Alert", "Allows triggering stream alerts.", DefaultAllowedDesignations: broadcasterAndModerator),
        ];

        foreach (PermissionDescriptor descriptor in builtIns)
        {
            this.Register(descriptor);
        }
    }
}

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

namespace StreamActions.Core.Permissions;

/// <summary>
/// Defines the built-in permission category names.
/// </summary>
public static class PermissionCategories
{
    /// <summary>
    /// Category for globally gated workspace feature toggles (<c>feature.*</c>).
    /// </summary>
    public const string Features = "Features";

    /// <summary>
    /// Category for web panel user interface permissions (<c>ui.*</c>).
    /// </summary>
    public const string Ui = "UI";

    /// <summary>
    /// Category for chat and channel moderation permissions (<c>moderation.*</c>).
    /// </summary>
    public const string Moderation = "Moderation";

    /// <summary>
    /// Category for executable bot and chat actions (<c>action.*</c>).
    /// </summary>
    public const string Actions = "Actions";
}

/// <summary>
/// Defines all built-in permission keys seeded into <see cref="IPermissionRegistry"/>.
/// </summary>
public static class BuiltInPermissionKeys
{
    /// <summary>
    /// Enables custom chat commands within a workspace.
    /// </summary>
    public const string FeatureCustomCommands = "feature.custom_commands";

    /// <summary>
    /// Enables scheduled chat timers within a workspace.
    /// </summary>
    public const string FeatureTimers = "feature.timers";

    /// <summary>
    /// Enables channel quotes within a workspace.
    /// </summary>
    public const string FeatureQuotes = "feature.quotes";

    /// <summary>
    /// Enables channel counters within a workspace.
    /// </summary>
    public const string FeatureCounters = "feature.counters";

    /// <summary>
    /// Enables automated chat moderation within a workspace.
    /// </summary>
    public const string FeatureModeration = "feature.moderation";

    /// <summary>
    /// Enables viewer loyalty points within a workspace.
    /// </summary>
    public const string FeatureLoyalty = "feature.loyalty";

    /// <summary>
    /// Enables browser source overlays within a workspace.
    /// </summary>
    public const string FeatureOverlays = "feature.overlays";

    /// <summary>
    /// Enables soundboard playback within a workspace.
    /// </summary>
    public const string FeatureSoundboard = "feature.soundboard";

    /// <summary>
    /// Enables stream alert triggers within a workspace.
    /// </summary>
    public const string FeatureAlerts = "feature.alerts";

    /// <summary>
    /// Enables third-party plugin execution within a workspace.
    /// </summary>
    public const string FeaturePlugins = "feature.plugins";

    /// <summary>
    /// Allows viewing the workspace dashboard in the web panel.
    /// </summary>
    public const string UiDashboardView = "ui.dashboard.view";

    /// <summary>
    /// Allows viewing custom commands in the web panel.
    /// </summary>
    public const string UiCommandsView = "ui.commands.view";

    /// <summary>
    /// Allows creating, editing, and deleting custom commands in the web panel.
    /// </summary>
    public const string UiCommandsEdit = "ui.commands.edit";

    /// <summary>
    /// Allows managing timers in the web panel.
    /// </summary>
    public const string UiTimersManage = "ui.timers.manage";

    /// <summary>
    /// Allows managing quotes in the web panel.
    /// </summary>
    public const string UiQuotesManage = "ui.quotes.manage";

    /// <summary>
    /// Allows managing counters in the web panel.
    /// </summary>
    public const string UiCountersManage = "ui.counters.manage";

    /// <summary>
    /// Allows managing moderation settings and filters in the web panel.
    /// </summary>
    public const string UiModerationManage = "ui.moderation.manage";

    /// <summary>
    /// Allows managing workspace permission groups and overrides in the web panel.
    /// </summary>
    public const string UiPermissionsManage = "ui.permissions.manage";

    /// <summary>
    /// Allows managing general workspace settings in the web panel.
    /// </summary>
    public const string UiSettingsManage = "ui.settings.manage";

    /// <summary>
    /// Allows viewing the workspace audit log in the web panel.
    /// </summary>
    public const string UiAuditLogView = "ui.audit_log.view";

    /// <summary>
    /// Allows timing out users in chat.
    /// </summary>
    public const string ModerationTimeout = "moderation.timeout";

    /// <summary>
    /// Allows banning users in chat.
    /// </summary>
    public const string ModerationBan = "moderation.ban";

    /// <summary>
    /// Allows unbanning users in chat.
    /// </summary>
    public const string ModerationUnban = "moderation.unban";

    /// <summary>
    /// Allows deleting individual chat messages.
    /// </summary>
    public const string ModerationDeleteMessage = "moderation.delete_message";

    /// <summary>
    /// Allows bypassing automated link filters in chat.
    /// </summary>
    public const string ModerationBypassLinks = "moderation.bypass_links";

    /// <summary>
    /// Allows bypassing automated caps filters in chat.
    /// </summary>
    public const string ModerationBypassCaps = "moderation.bypass_caps";

    /// <summary>
    /// Allows bypassing automated spam filters in chat.
    /// </summary>
    public const string ModerationBypassSpam = "moderation.bypass_spam";

    /// <summary>
    /// Allows modifying moderation filters via chat commands.
    /// </summary>
    public const string ModerationManageFilters = "moderation.manage_filters";

    /// <summary>
    /// Allows executing custom chat commands.
    /// </summary>
    public const string ActionRunCommand = "action.run_command";

    /// <summary>
    /// Allows adding, editing, or removing quotes via chat actions.
    /// </summary>
    public const string ActionManageQuotes = "action.manage_quotes";

    /// <summary>
    /// Allows incrementing or modifying counters via chat actions.
    /// </summary>
    public const string ActionManageCounters = "action.manage_counters";

    /// <summary>
    /// Allows triggering soundboard clips.
    /// </summary>
    public const string ActionTriggerSoundboard = "action.trigger_soundboard";

    /// <summary>
    /// Allows triggering stream alerts.
    /// </summary>
    public const string ActionTriggerAlert = "action.trigger_alert";
}

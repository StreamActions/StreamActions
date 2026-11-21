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

using StreamActions.Core.Models.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace StreamActions.Core.Models;

/// <summary>
/// Defines the role of a platform attached to a workspace.
/// </summary>
public enum PlatformRole
{
    /// <summary>
    /// The primary platform that anchors the workspace.
    /// </summary>
    Primary = 0,

    /// <summary>
    /// A peer platform used for simulcasting alongside the primary platform.
    /// </summary>
    SimulcastPeer = 1,

    /// <summary>
    /// An add-on platform such as Discord that augments the workspace.
    /// </summary>
    Addon = 2,
}

/// <summary>
/// Defines the role of an OAuth or API token stored for a platform.
/// </summary>
public enum TokenRole
{
    /// <summary>
    /// The broadcaster or channel owner token.
    /// </summary>
    Broadcaster = 0,

    /// <summary>
    /// The bot chat account token.
    /// </summary>
    BotChat = 1,

    /// <summary>
    /// An application-level client credentials token.
    /// </summary>
    App = 2,
}

/// <summary>
/// Represents the lifecycle status of a stored credential or secret.
/// </summary>
public enum CredentialStatus
{
    /// <summary>
    /// The credential is active and valid for use.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The credential has expired and needs renewal.
    /// </summary>
    Expired = 1,

    /// <summary>
    /// The credential could not be decrypted with the current Data Protection key ring and requires re-authentication.
    /// </summary>
    KeyRingMismatchRequiresReauth = 2,

    /// <summary>
    /// The credential has been explicitly revoked.
    /// </summary>
    Revoked = 3,
}

/// <summary>
/// Represents a tri-state permission grant rule.
/// </summary>
public enum PermissionState
{
    /// <summary>
    /// Inherits the permission evaluation from parent or default rules.
    /// </summary>
    Inherit = 0,

    /// <summary>
    /// Explicitly grants the permission.
    /// </summary>
    Allow = 1,

    /// <summary>
    /// Explicitly denies the permission, overriding any <see cref="Allow"/> rules.
    /// </summary>
    Deny = 2,
}

/// <summary>
/// Represents the active view mode of the permissions management pages (<c>/permissions</c> and <c>/admin/global-permissions</c>).
/// </summary>
public enum PermissionsPageMode
{
    /// <summary>
    /// Displays the list of permission groups and the permission audit launcher.
    /// </summary>
    List = 0,

    /// <summary>
    /// Displays a read-only view of a single permission group's grants and members/workspaces, or an effective permission audit breakdown.
    /// </summary>
    View = 1,

    /// <summary>
    /// Displays the single-group permission editor with staged edits, bulk actions, and sticky Save/Revert controls.
    /// </summary>
    Edit = 2,
}

/// <summary>
/// Defines how a workspace counter is scoped.
/// </summary>
public enum CounterScopeMode
{
    /// <summary>
    /// A single global counter value across all games/categories.
    /// </summary>
    Global = 0,

    /// <summary>
    /// Separate counter values tracked per game or category.
    /// </summary>
    PerGame = 1,
}

/// <summary>
/// Defines how a bound counter behaves when a custom command is executed.
/// </summary>
public enum CounterUsageBehavior
{
    /// <summary>
    /// Does not modify or read the bound counter.
    /// </summary>
    None = 0,

    /// <summary>
    /// Increments the bound counter by one on command use.
    /// </summary>
    IncrementOnUse = 1,

    /// <summary>
    /// Decrements the bound counter by one on command use.
    /// </summary>
    DecrementOnUse = 2,

    /// <summary>
    /// Reads the current value of the bound counter without modifying it.
    /// </summary>
    ReadOnly = 3,

    /// <summary>
    /// Sets the bound counter value from the first command argument.
    /// </summary>
    SetFromArg1 = 4,

    /// <summary>
    /// Resets the bound counter value to zero.
    /// </summary>
    ResetToZero = 5,

    /// <summary>
    /// Mutates or reads the bound counter using parameterized arguments (for example, <c>+1</c>, <c>-2</c>, or <c>=10</c>).
    /// </summary>
    Parameterized = 6,
}

/// <summary>
/// Defines the priority lanes used by the outbound chat dispatcher, ordered from highest to lowest priority.
/// </summary>
public enum OutboundChatPriority
{
    /// <summary>
    /// Highest-priority lane for immediate moderation actions and notices; applies backpressure without dropping messages.
    /// </summary>
    ModerationImmediate = 0,

    /// <summary>
    /// High-priority lane for interactive command replies; applies backpressure without dropping messages.
    /// </summary>
    CommandReply = 1,

    /// <summary>
    /// Normal-priority lane for stream event announcements; drops the oldest queued message when saturated.
    /// </summary>
    EventAnnouncement = 2,

    /// <summary>
    /// Lowest-priority lane for scheduled timer messages; drops the oldest queued message when saturated.
    /// </summary>
    Timer = 3,
}

/// <summary>
/// Defines which attached workspace platforms receive an outbound message.
/// </summary>
public enum OutboundTargetScope
{
    /// <summary>
    /// Routes the message only to the platform and channel where the invocation originated.
    /// </summary>
    OriginPlatform = 0,

    /// <summary>
    /// Routes the message only to the workspace's primary platform channel.
    /// </summary>
    PrimaryPlatformOnly = 1,

    /// <summary>
    /// Routes the message to all currently live streaming platforms (primary and simulcast peers) attached to the workspace.
    /// </summary>
    AllActiveStreamPlatforms = 2,
}

/// <summary>
/// Defines how identical outbound messages sent within the duplicate-guard window are handled.
/// </summary>
public enum DuplicateMessagePolicy
{
    /// <summary>
    /// Suppresses identical messages within the duplicate window unless the platform permits duplicates for non-moderators or the bot is a moderator or VIP.
    /// </summary>
    PlatformDefault = 0,

    /// <summary>
    /// Always suppresses identical messages sent to the same channel within the duplicate window, regardless of moderator or VIP status.
    /// </summary>
    Suppress = 1,

    /// <summary>
    /// Bypasses the duplicate-message guard and always sends the message.
    /// </summary>
    Allow = 2,
}

/// <summary>
/// Defines how a command response is delivered to chat.
/// </summary>
public enum CommandDeliveryMode
{
    /// <summary>
    /// Sends the response as a standard chat message.
    /// </summary>
    Chat = 0,

    /// <summary>
    /// Sends the response as a threaded reply to the invoking message.
    /// </summary>
    ThreadedReply = 1,

    /// <summary>
    /// Sends the response as a private whisper or direct message.
    /// </summary>
    Whisper = 2,
}

/// <summary>
/// Defines when a command or timer is eligible to execute based on stream status.
/// </summary>
public enum StreamStatusRequirement
{
    /// <summary>
    /// Eligible whether the stream is online or offline.
    /// </summary>
    Both = 0,

    /// <summary>
    /// Eligible only when the stream is live/online.
    /// </summary>
    OnlineOnly = 1,

    /// <summary>
    /// Eligible only when the stream is offline.
    /// </summary>
    OfflineOnly = 2,
}

/// <summary>
/// Defines the selection strategy for a timer pool.
/// </summary>
public enum TimerSelectionMode
{
    /// <summary>
    /// Selects timers in sequential order.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// Selects timers in a randomized shuffle order without immediate repeats.
    /// </summary>
    RandomShuffle = 1,
}

/// <summary>
/// Identifies the origin of an audited workspace action.
/// </summary>
public enum AuditSource
{
    /// <summary>
    /// Action initiated through the web user interface.
    /// </summary>
    WebUI = 0,

    /// <summary>
    /// Action initiated through a chat command.
    /// </summary>
    ChatCommand = 1,

    /// <summary>
    /// Action initiated automatically by the system.
    /// </summary>
    System = 2,
}

/// <summary>
/// Identifies the multi-factor authentication method for a panel user.
/// </summary>
public enum MfaMethod
{
    /// <summary>
    /// WebAuthn passkey / hardware security key.
    /// </summary>
    Passkey = 0,

    /// <summary>
    /// Time-based one-time password (TOTP) authenticator.
    /// </summary>
    Totp = 1,
}

/// <summary>
/// Defines the operational role of a StreamActions node in a deployment.
/// </summary>
public enum NodeMode
{
    /// <summary>
    /// Single-node mode running all services in one process.
    /// </summary>
    AllInOne = 0,

    /// <summary>
    /// Source node owning platform connections and dispatching events.
    /// </summary>
    Source = 1,

    /// <summary>
    /// Sink node handling web UI or auxiliary workloads connected to a source node.
    /// </summary>
    Sink = 2,
}

/// <summary>
/// Identifies the database provider configured for persistence.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// Embedded SQLite database.
    /// </summary>
    Sqlite = 0,

    /// <summary>
    /// PostgreSQL database server.
    /// </summary>
    Postgres = 1,

    /// <summary>
    /// MySQL database server.
    /// </summary>
    MySql = 2,

    /// <summary>
    /// MariaDB database server.
    /// </summary>
    MariaDb = 3,

    /// <summary>
    /// Microsoft SQL Server database.
    /// </summary>
    SqlServer = 4,
}

/// <summary>
/// Flags describing the capabilities supported by a chat or streaming platform integration.
/// </summary>
[Flags]
public enum PlatformCapabilities
{
    /// <summary>
    /// No capabilities declared.
    /// </summary>
    None = 0,

    /// <summary>
    /// Platform can serve as a primary workspace anchor.
    /// </summary>
    PrimaryWorkspace = 1 << 0,

    /// <summary>
    /// Platform can be attached as a workspace add-on.
    /// </summary>
    WorkspaceAddon = 1 << 1,

    /// <summary>
    /// Platform supports real-time chat messaging.
    /// </summary>
    Chat = 1 << 2,

    /// <summary>
    /// Platform supports loyalty points or channel redemptions.
    /// </summary>
    Loyalty = 1 << 3,

    /// <summary>
    /// Platform supports webhook or EventSub-style event subscriptions.
    /// </summary>
    EventSub = 1 << 4,

    /// <summary>
    /// Platform supports native slash commands.
    /// </summary>
    SlashCommands = 1 << 5,

    /// <summary>
    /// Platform supports channel moderation actions.
    /// </summary>
    Moderation = 1 << 6,
}

/// <summary>
/// Defines the instance-wide policy governing platform OAuth application credentials.
/// </summary>
public enum InstanceOAuthAppPolicy
{
    /// <summary>
    /// All workspaces must use the instance-wide OAuth application credentials.
    /// </summary>
    InstanceOnly = 0,

    /// <summary>
    /// Workspaces use the instance-wide OAuth application by default, with an optional per-workspace override when permitted.
    /// </summary>
    InstanceWithOptionalOverride = 1,

    /// <summary>
    /// Panel sign-in, linking, and step-up flows use the instance-wide OAuth application, while workspace broadcaster and bot API OAuth flows require streamer-supplied OAuth application credentials.
    /// </summary>
    InstanceLoginWithStreamerApiOAuth = 2,

    /// <summary>
    /// Workspaces must supply their own OAuth application credentials and never fall back to instance credentials.
    /// </summary>
    RequireStreamerOAuthApp = 3,
}

/// <summary>
/// Defines the instance-wide policy governing bot chat account credentials.
/// </summary>
public enum InstanceBotAccountPolicy
{
    /// <summary>
    /// All workspaces use the instance default bot account.
    /// </summary>
    DefaultOnly = 0,

    /// <summary>
    /// Workspaces use the instance default bot account unless permitted to connect a custom bot account.
    /// </summary>
    DefaultWithOptionalOverride = 1,

    /// <summary>
    /// Workspaces must connect their own bot account and cannot use an instance default bot account.
    /// </summary>
    RequireStreamerBotAccount = 2,
}

/// <summary>
/// Defines whether external OpenID Connect providers may be used to sign into the web panel.
/// </summary>
public enum ExternalOidcLoginMode
{
    /// <summary>
    /// External OIDC login is allowed alongside platform OAuth login.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// External OIDC login is disabled.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// External OIDC login is required for panel authentication.
    /// </summary>
    Required = 2,
}

/// <summary>
/// Defines the availability policy for a multi-factor authentication method.
/// </summary>
public enum MfaMethodAvailability
{
    /// <summary>
    /// The MFA method is allowed for enrollment and verification.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// The MFA method is disabled and may not be enrolled or used for verification.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// The MFA method is required by policy.
    /// </summary>
    Required = 2,
}

/// <summary>
/// Defines which panel user roles are required to have MFA enrolled before accessing the web panel.
/// </summary>
public enum PanelLoginMfaRequirement
{
    /// <summary>
    /// MFA enrollment is optional at login unless the user already has MFA enrolled.
    /// </summary>
    Optional = 0,

    /// <summary>
    /// MFA is required for the instance root operator, global administrators, and workspace owners (streamers).
    /// </summary>
    RequiredForRootAndStreamers = 1,

    /// <summary>
    /// MFA is required for root, global administrators, workspace owners, and moderators.
    /// </summary>
    RequiredForModerators = 2,

    /// <summary>
    /// MFA is required for all panel users.
    /// </summary>
    RequiredForAll = 3,
}

/// <summary>
/// Defines whether sensitive actions require recent MFA verification ("Sudo Mode" step-up elevation).
/// </summary>
public enum StepUpSudoPolicy
{
    /// <summary>
    /// Step-up MFA elevation is optional for sensitive actions.
    /// </summary>
    Optional = 0,

    /// <summary>
    /// Step-up MFA verification within the elevation window is required before performing sensitive actions.
    /// </summary>
    Required = 1,
}

/// <summary>
/// Defines the authentication readiness status of an accessible workspace for the current user session.
/// </summary>
public enum WorkspaceSessionAuthStatus
{
    /// <summary>
    /// The workspace meets all authentication requirements and is ready for active use.
    /// </summary>
    Ready = 0,

    /// <summary>
    /// The workspace requires signing in via an external OpenID Connect provider before activation.
    /// </summary>
    RequiresOidcReauth = 1,

    /// <summary>
    /// The workspace requires multi-factor authentication verification before activation.
    /// </summary>
    RequiresMfaChallenge = 2,
}

/// <summary>
/// Parses persisted authentication policy strings into strongly typed policy enums.
/// </summary>
public static class AuthPolicyParser
{
    /// <summary>
    /// Seeded legacy string value representing the default instance-only policy.
    /// </summary>
    public const string InstanceDefaultValue = "instance_default";

    /// <summary>
    /// Default display name and TOTP issuer used when <see cref="InstanceSettings"/> values are blank.
    /// </summary>
    public const string DefaultInstanceName = "StreamActions";

    /// <summary>
    /// Resolves the effective display name for the instance, falling back to <see cref="DefaultInstanceName"/> when blank.
    /// </summary>
    /// <param name="settings">The instance settings, or <see langword="null"/>.</param>
    /// <returns>The trimmed instance name or <see cref="DefaultInstanceName"/>.</returns>
    public static string ResolveInstanceName(InstanceSettings? settings) =>
        string.IsNullOrWhiteSpace(settings?.InstanceName)
            ? DefaultInstanceName
            : settings.InstanceName.Trim();

    /// <summary>
    /// Resolves the effective TOTP issuer label for <c>otpauth://</c> provisioning URIs, falling back to <see cref="DefaultInstanceName"/> when blank.
    /// </summary>
    /// <param name="settings">The instance settings, or <see langword="null"/>.</param>
    /// <returns>The trimmed TOTP issuer name or <see cref="DefaultInstanceName"/>.</returns>
    public static string ResolveTotpIssuerName(InstanceSettings? settings) =>
        string.IsNullOrWhiteSpace(settings?.TotpIssuerName)
            ? DefaultInstanceName
            : settings.TotpIssuerName.Trim();

    /// <summary>
    /// Formats a browser page title using the optional <paramref name="pageName"/> and <paramref name="instanceName"/>, always ending with <see cref="DefaultInstanceName"/> without duplicating it.
    /// </summary>
    /// <param name="pageName">The page-specific title segment, or <see langword="null"/>/whitespace for the home page.</param>
    /// <param name="instanceName">The configured instance display name, or <see langword="null"/>/whitespace when unset.</param>
    /// <returns>The formatted browser page title.</returns>
    public static string FormatPageTitle(string? pageName, string? instanceName)
    {
        string? trimmedPageName = string.IsNullOrWhiteSpace(pageName) ? null : pageName.Trim();
        string? trimmedInstanceName = string.IsNullOrWhiteSpace(instanceName) ? null : instanceName.Trim();
        bool hasCustomInstanceName = trimmedInstanceName is not null &&
            !string.Equals(trimmedInstanceName, DefaultInstanceName, StringComparison.OrdinalIgnoreCase);

        return trimmedPageName is not null
            ? hasCustomInstanceName
                ? $"{trimmedPageName} - {trimmedInstanceName} - {DefaultInstanceName}"
                : $"{trimmedPageName} - {DefaultInstanceName}"
            : hasCustomInstanceName
            ? $"{trimmedInstanceName} - {DefaultInstanceName}"
            : DefaultInstanceName;
    }

    /// <summary>
    /// Formats a browser page title using the optional <paramref name="pageName"/> and the configured instance name in <paramref name="settings"/>.
    /// </summary>
    /// <param name="pageName">The page-specific title segment, or <see langword="null"/>/whitespace for the home page.</param>
    /// <param name="settings">The instance settings, or <see langword="null"/>.</param>
    /// <returns>The formatted browser page title.</returns>
    public static string FormatPageTitle(string? pageName, InstanceSettings? settings) =>
        FormatPageTitle(pageName, settings?.InstanceName);

    /// <summary>
    /// Parses a persisted OAuth application policy string into an <see cref="InstanceOAuthAppPolicy"/> value.
    /// </summary>
    /// <param name="value">The persisted policy string.</param>
    /// <returns>The parsed <see cref="InstanceOAuthAppPolicy"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is non-empty and does not map to a valid <see cref="InstanceOAuthAppPolicy"/>.</exception>
    public static InstanceOAuthAppPolicy ParseOAuthAppPolicy(string? value) =>
        TryParseOAuthAppPolicy(value, out InstanceOAuthAppPolicy policy)
            ? policy
            : throw new ArgumentException($"Unknown OAuth application policy '{value}'.", nameof(value));

    /// <summary>
    /// Attempts to parse a persisted OAuth application policy string into an <see cref="InstanceOAuthAppPolicy"/> value.
    /// </summary>
    /// <param name="value">The persisted policy string.</param>
    /// <param name="policy">When this method returns <see langword="true"/>, contains the parsed <see cref="InstanceOAuthAppPolicy"/>.</param>
    /// <returns><see langword="true"/> if the string was null, whitespace, or mapped to a valid policy; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseOAuthAppPolicy(string? value, out InstanceOAuthAppPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            policy = InstanceOAuthAppPolicy.InstanceOnly;
            return true;
        }

        string trimmed = value.Trim();
        if (string.Equals(trimmed, InstanceDefaultValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "instance_only", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceOAuthAppPolicy.InstanceOnly;
            return true;
        }

        if (string.Equals(trimmed, "instance_with_optional_override", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceOAuthAppPolicy.InstanceWithOptionalOverride;
            return true;
        }

        if (string.Equals(trimmed, "instance_login_with_streamer_api_oauth", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth;
            return true;
        }

        if (string.Equals(trimmed, "require_streamer_oauth_app", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceOAuthAppPolicy.RequireStreamerOAuthApp;
            return true;
        }

        if (Enum.TryParse(trimmed, ignoreCase: true, out InstanceOAuthAppPolicy parsed) && Enum.IsDefined(parsed))
        {
            policy = parsed;
            return true;
        }

        policy = InstanceOAuthAppPolicy.InstanceOnly;
        return false;
    }

    /// <summary>
    /// Reads all valid per-platform <see cref="InstanceOAuthAppPolicy"/> overrides from <see cref="InstanceSettings.PlatformOAuthAppPoliciesJson"/>.
    /// </summary>
    /// <param name="settings">The instance settings.</param>
    /// <returns>A case-insensitive dictionary mapping normalized platform identifiers to their overridden policies.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    public static IReadOnlyDictionary<string, InstanceOAuthAppPolicy> GetPlatformOAuthAppPolicies(InstanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Dictionary<string, InstanceOAuthAppPolicy> result = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(settings.PlatformOAuthAppPoliciesJson))
        {
            return result;
        }

        Dictionary<string, string>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, string>>(settings.PlatformOAuthAppPoliciesJson);
        }
        catch (JsonException)
        {
            return result;
        }

        if (raw is null)
        {
            return result;
        }

        foreach (KeyValuePair<string, string> entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            if (TryParseOAuthAppPolicy(entry.Value, out InstanceOAuthAppPolicy parsed))
            {
                result[entry.Key.Trim().ToLowerInvariant()] = parsed;
            }
        }

        return result;
    }

    /// <summary>
    /// Attempts to retrieve an explicit per-platform <see cref="InstanceOAuthAppPolicy"/> override from <paramref name="settings"/>.
    /// </summary>
    /// <param name="settings">The instance settings.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="policy">When this method returns <see langword="true"/>, contains the explicit platform policy override.</param>
    /// <returns><see langword="true"/> if an explicit override exists for <paramref name="platformId"/>; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPlatformOAuthAppPolicyOverride(
        InstanceSettings settings,
        string platformId,
        out InstanceOAuthAppPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        IReadOnlyDictionary<string, InstanceOAuthAppPolicy> overrides = GetPlatformOAuthAppPolicies(settings);
        return overrides.TryGetValue(platformId.Trim(), out policy);
    }

    /// <summary>
    /// Resolves the effective <see cref="InstanceOAuthAppPolicy"/> for a specific platform, falling back to <see cref="InstanceSettings.InstanceOAuthAppPolicy"/> when no override exists.
    /// </summary>
    /// <param name="settings">The instance settings.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <returns>The effective <see cref="InstanceOAuthAppPolicy"/> for <paramref name="platformId"/>.</returns>
    public static InstanceOAuthAppPolicy ResolveOAuthAppPolicy(InstanceSettings settings, string platformId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        return TryGetPlatformOAuthAppPolicyOverride(settings, platformId, out InstanceOAuthAppPolicy overridePolicy)
            ? overridePolicy
            : ParseOAuthAppPolicy(settings.InstanceOAuthAppPolicy);
    }

    /// <summary>
    /// Sets or clears a per-platform <see cref="InstanceOAuthAppPolicy"/> override in <see cref="InstanceSettings.PlatformOAuthAppPoliciesJson"/>.
    /// </summary>
    /// <param name="settings">The instance settings to update.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="policy">The override policy to store, or <see langword="null"/> to remove the override and fall back to the instance default.</param>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    public static void SetPlatformOAuthAppPolicy(
        InstanceSettings settings,
        string platformId,
        InstanceOAuthAppPolicy? policy)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = platformId.Trim().ToLowerInvariant();
        IReadOnlyDictionary<string, InstanceOAuthAppPolicy> existing = GetPlatformOAuthAppPolicies(settings);
        Dictionary<string, string> updated = new(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, InstanceOAuthAppPolicy> entry in existing)
        {
            updated[entry.Key] = entry.Value.ToString();
        }

        if (policy.HasValue)
        {
            updated[normalizedPlatformId] = policy.Value.ToString();
        }
        else
        {
            _ = updated.Remove(normalizedPlatformId);
        }

        settings.PlatformOAuthAppPoliciesJson = updated.Count == 0
            ? null
            : JsonSerializer.Serialize(updated);
    }

    /// <summary>
    /// Parses a persisted bot account policy string into an <see cref="InstanceBotAccountPolicy"/> value.
    /// </summary>
    /// <param name="value">The persisted policy string.</param>
    /// <returns>The parsed <see cref="InstanceBotAccountPolicy"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is non-empty and does not map to a valid <see cref="InstanceBotAccountPolicy"/>.</exception>
    public static InstanceBotAccountPolicy ParseBotAccountPolicy(string? value) =>
        TryParseBotAccountPolicy(value, out InstanceBotAccountPolicy policy)
            ? policy
            : throw new ArgumentException($"Unknown bot account policy '{value}'.", nameof(value));

    /// <summary>
    /// Attempts to parse a persisted bot account policy string into an <see cref="InstanceBotAccountPolicy"/> value.
    /// </summary>
    /// <param name="value">The persisted policy string.</param>
    /// <param name="policy">When this method returns <see langword="true"/>, contains the parsed <see cref="InstanceBotAccountPolicy"/>.</param>
    /// <returns><see langword="true"/> if the string was null, whitespace, or mapped to a valid policy; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseBotAccountPolicy(string? value, out InstanceBotAccountPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            policy = InstanceBotAccountPolicy.DefaultOnly;
            return true;
        }

        string trimmed = value.Trim();
        if (string.Equals(trimmed, InstanceDefaultValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "default_only", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceBotAccountPolicy.DefaultOnly;
            return true;
        }

        if (string.Equals(trimmed, "default_with_optional_override", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceBotAccountPolicy.DefaultWithOptionalOverride;
            return true;
        }

        if (string.Equals(trimmed, "require_streamer_bot_account", StringComparison.OrdinalIgnoreCase))
        {
            policy = InstanceBotAccountPolicy.RequireStreamerBotAccount;
            return true;
        }

        if (Enum.TryParse(trimmed, ignoreCase: true, out InstanceBotAccountPolicy parsed) && Enum.IsDefined(parsed))
        {
            policy = parsed;
            return true;
        }

        policy = InstanceBotAccountPolicy.DefaultOnly;
        return false;
    }
}

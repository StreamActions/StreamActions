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

namespace StreamActions.Core.Models;

/// <summary>
/// Represents the outcome of resolving chat bot credentials for a workspace platform.
/// </summary>
public enum BotCredentialResolutionStatus
{
    /// <summary>
    /// An active bot token payload was resolved and is ready for use.
    /// </summary>
    Resolved = 0,

    /// <summary>
    /// No usable bot token is currently available and bot authorization is required.
    /// </summary>
    AwaitingBotAccountAuth = 1,

    /// <summary>
    /// A stored credential could not be decrypted with the active Data Protection key ring and requires re-authentication.
    /// </summary>
    KeyRingMismatchRequiresReauth = 2,
}

/// <summary>
/// Identifies the source credential selected for chat bot authentication.
/// </summary>
public enum BotCredentialSource
{
    /// <summary>
    /// No bot credential was resolved.
    /// </summary>
    None = 0,

    /// <summary>
    /// The instance-wide root default bot account credential.
    /// </summary>
    InstanceDefaultBot = 1,

    /// <summary>
    /// A custom per-workspace bot account credential.
    /// </summary>
    WorkspaceCustomBot = 2,

    /// <summary>
    /// The workspace's broadcaster credential used as the chat bot identity fallback.
    /// </summary>
    WorkspaceBroadcasterFallback = 3,
}

/// <summary>
/// Represents the resolved bot chat credential and policy metadata for a workspace platform.
/// </summary>
/// <param name="Status">The resolution status.</param>
/// <param name="Source">The selected credential source.</param>
/// <param name="Payload">The decrypted token payload when <paramref name="Status"/> is <see cref="BotCredentialResolutionStatus.Resolved"/>.</param>
/// <param name="EffectivePolicy">The evaluated instance bot account policy.</param>
/// <param name="IsCustomOverrideAllowed">A value indicating whether the workspace is permitted to use a custom bot account or broadcaster fallback.</param>
/// <param name="CredentialWorkspaceId">The workspace identifier owning the resolved credential, or <see langword="null"/> for the instance default bot.</param>
/// <param name="CredentialTokenRole">The token role of the resolved or attempted credential.</param>
/// <param name="Reason">An optional human-readable explanation of the resolution outcome.</param>
public sealed record BotCredentialResolutionResult(
    BotCredentialResolutionStatus Status,
    BotCredentialSource Source,
    PlatformTokenPayload? Payload = null,
    InstanceBotAccountPolicy EffectivePolicy = InstanceBotAccountPolicy.DefaultOnly,
    bool IsCustomOverrideAllowed = false,
    Guid? CredentialWorkspaceId = null,
    TokenRole? CredentialTokenRole = null,
    string? Reason = null);

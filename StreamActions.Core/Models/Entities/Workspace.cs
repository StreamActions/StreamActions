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

namespace StreamActions.Core.Models.Entities;

/// <summary>
/// Represents an isolated bot workspace owned by a <see cref="PanelUser"/>.
/// </summary>
public sealed class Workspace
{
    /// <summary>
    /// Gets or sets the unique workspace identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the <see cref="PanelUser"/> who owns this workspace.
    /// </summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>
    /// Gets or sets the workspace display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique normalized lowercase URL-friendly workspace slug.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the primary platform anchoring this workspace.
    /// </summary>
    public string PrimaryPlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether non-admin users must authenticate via an external OIDC provider to access this workspace.
    /// </summary>
    public bool RequireExternalOidcLogin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this workspace may override the instance default bot account.
    /// </summary>
    public bool AllowCustomBotAccountOverride { get; set; }

    /// <summary>
    /// Gets or sets the workspace-level availability policy for WebAuthn passkeys.
    /// </summary>
    public MfaMethodAvailability MfaPasskeyAvailability { get; set; } = MfaMethodAvailability.Allowed;

    /// <summary>
    /// Gets or sets the workspace-level availability policy for TOTP authenticator apps.
    /// </summary>
    public MfaMethodAvailability MfaTotpAvailability { get; set; } = MfaMethodAvailability.Allowed;

    /// <summary>
    /// Gets or sets the workspace-level panel login MFA requirement.
    /// </summary>
    public PanelLoginMfaRequirement LoginMfaRequirement { get; set; } = PanelLoginMfaRequirement.Optional;

    /// <summary>
    /// Gets or sets the workspace-level step-up ("Sudo Mode") MFA policy for sensitive actions.
    /// </summary>
    public StepUpSudoPolicy StepUpSudoPolicy { get; set; } = StepUpSudoPolicy.Required;

    /// <summary>
    /// Gets or sets the UTC timestamp when the workspace was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

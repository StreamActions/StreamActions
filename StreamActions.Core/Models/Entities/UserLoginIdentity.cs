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
/// Represents an external OAuth or OIDC login identity linked to a <see cref="PanelUser"/>.
/// </summary>
public sealed class UserLoginIdentity
{
    /// <summary>
    /// Gets or sets the unique login identity identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the associated <see cref="PanelUser"/> identifier.
    /// </summary>
    public Guid PanelUserId { get; set; }

    /// <summary>
    /// Gets or sets the identity provider identifier (for example, <c>twitch</c> or <c>oidc:authentik</c>).
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject or user identifier issued by the provider.
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username or login reported by the provider.
    /// </summary>
    public string ProviderUsername { get; set; } = string.Empty;
}

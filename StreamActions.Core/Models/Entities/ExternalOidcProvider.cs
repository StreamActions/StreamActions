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

using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Models.Entities;

/// <summary>
/// Represents a configured OpenID Connect identity provider for instance-wide or workspace authentication.
/// </summary>
public sealed class ExternalOidcProvider
{
    /// <summary>
    /// Gets or sets the unique provider configuration identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the optional workspace identifier; <see langword="null"/> indicates an instance-wide provider.
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the unique URL-friendly slug for this provider.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OIDC authority URL.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Persisted as a string column in EF Core.")]
    public string AuthorityUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OIDC client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Data Protection encrypted OIDC client secret.
    /// </summary>
    public string EncryptedClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON array of requested OIDC scopes.
    /// </summary>
    public string ScopesJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional claim type used to map OIDC groups.
    /// </summary>
    public string? GroupClaimType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

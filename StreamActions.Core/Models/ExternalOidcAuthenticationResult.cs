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
/// Represents the validated identity claims returned after completing an external OIDC authentication flow.
/// </summary>
/// <param name="Subject">The validated OIDC subject (<c>sub</c>) identifier.</param>
/// <param name="Username">The resolved username claim (<c>preferred_username</c>, <c>nickname</c>, or fallback).</param>
/// <param name="DisplayName">The resolved human-readable display name (<c>name</c> or <c>preferred_username</c>).</param>
/// <param name="Email">The optional validated email address claim.</param>
/// <param name="Groups">The group claim values extracted using the provider's configured group claim type.</param>
public sealed record ExternalOidcAuthenticationResult(
    string Subject,
    string? Username = null,
    string? DisplayName = null,
    string? Email = null,
    IReadOnlyList<string>? Groups = null);

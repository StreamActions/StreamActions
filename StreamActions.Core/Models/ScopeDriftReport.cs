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
/// Represents the comparison between currently granted OAuth scopes and required platform/plugin scopes.
/// </summary>
/// <param name="PlatformId">The normalized lowercase platform identifier.</param>
/// <param name="GrantedScopes">The scopes currently granted on the stored token.</param>
/// <param name="RequiredScopes">The aggregated scopes required by enabled plugins and registered requirements.</param>
/// <param name="MissingScopes">The required scopes absent from <paramref name="GrantedScopes"/>.</param>
/// <param name="UpgradeScopesUnion">The deduplicated union of <paramref name="GrantedScopes"/> and <paramref name="MissingScopes"/> for incremental re-authorization.</param>
public sealed record ScopeDriftReport(
    string PlatformId,
    IReadOnlyList<string> GrantedScopes,
    IReadOnlyList<string> RequiredScopes,
    IReadOnlyList<string> MissingScopes,
    IReadOnlyList<string> UpgradeScopesUnion)
{
    /// <summary>
    /// Gets a value indicating whether any required scopes are missing from the granted token.
    /// </summary>
    public bool HasDrift => this.MissingScopes.Count > 0;
}

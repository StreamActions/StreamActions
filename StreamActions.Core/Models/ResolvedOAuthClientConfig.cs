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
using System.Text;

namespace StreamActions.Core.Models;

/// <summary>
/// Represents the effective OAuth application credentials resolved for a platform and optional workspace.
/// </summary>
/// <param name="PlatformId">The normalized lowercase platform identifier.</param>
/// <param name="ClientId">The resolved OAuth client identifier.</param>
/// <param name="ClientSecret">The resolved OAuth client secret.</param>
/// <param name="RedirectUri">The optional explicit redirect URI override.</param>
/// <param name="IsWorkspaceOverride">A value indicating whether the credentials came from a workspace-level custom OAuth application.</param>
/// <param name="WorkspaceId">The optional workspace identifier for which the configuration was resolved.</param>
public sealed record ResolvedOAuthClientConfig(
    string PlatformId,
    string ClientId,
    string ClientSecret,
    Uri? RedirectUri = null,
    bool IsWorkspaceOverride = false,
    Guid? WorkspaceId = null)
{
    /// <summary>
    /// Formats record members while redacting the client secret.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.PlatformId)).Append(" = ").Append(this.PlatformId);
        _ = builder.Append(", ").Append(nameof(this.ClientId)).Append(" = ").Append(this.ClientId);
        _ = builder.Append(", ").Append(nameof(this.ClientSecret)).Append(" = ").Append(string.IsNullOrEmpty(this.ClientSecret) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.RedirectUri)).Append(" = ").Append(this.RedirectUri);
        _ = builder.Append(", ").Append(nameof(this.IsWorkspaceOverride)).Append(" = ").Append(this.IsWorkspaceOverride);
        _ = builder.Append(", ").Append(nameof(this.WorkspaceId)).Append(" = ").Append(this.WorkspaceId);
        return true;
    }
}

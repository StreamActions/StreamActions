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
/// Represents an active or revoked web control panel session for a <see cref="PanelUser"/>.
/// </summary>
public sealed class PanelUserSession
{
    /// <summary>
    /// Gets or sets the unique session identifier (stored as the <c>sid</c> claim in the authentication cookie).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning <see cref="PanelUser"/> identifier.
    /// </summary>
    public Guid PanelUserId { get; set; }

    /// <summary>
    /// Gets or sets the authentication provider identifier used to create the session.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the session was last observed.
    /// </summary>
    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional remote client IP address recorded when the session was created.
    /// </summary>
    public string? RemoteIpAddress { get; set; }

    /// <summary>
    /// Gets or sets the optional HTTP <c>User-Agent</c> header recorded when the session was created.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC timestamp when the session was revoked, or <see langword="null"/> if still active.
    /// </summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }
}


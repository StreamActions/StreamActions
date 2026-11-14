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

namespace StreamActions.Core.Models.Metadata;

/// <summary>
/// Tier A JSON metadata stored on <see cref="Entities.WorkspacePlatform"/> for platform-specific settings.
/// </summary>
public sealed class PlatformMetadata
{
    /// <summary>
    /// Gets or sets a value indicating whether the bot should automatically join chat for this platform.
    /// </summary>
    public bool AutoJoinChat { get; set; }

    /// <summary>
    /// Gets or sets the optional bot username override for this platform.
    /// </summary>
    public string? BotUsername { get; set; }

    /// <summary>
    /// Gets or sets additional platform-specific key-value settings.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Mutable dictionary required for EF Core complex JSON property and System.Text.Json binding.")]
    public Dictionary<string, string> ExtraSettings { get; set; } = [];
}

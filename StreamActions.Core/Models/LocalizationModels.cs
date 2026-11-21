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
/// Represents a localized bot command definition including its resolved trigger phrases and optional metadata.
/// </summary>
/// <param name="CommandId">The canonical command identifier (for example, <c>bot.permit</c>).</param>
/// <param name="Triggers">The deduplicated list of localized, fallback, and workspace-override trigger phrases.</param>
/// <param name="Description">The optional localized human-readable description of the command.</param>
/// <param name="Module">The optional catalog module name that defines the command.</param>
public sealed record LocalizedCommandDefinition(
    string CommandId,
    IReadOnlyList<string> Triggers,
    string? Description = null,
    string? Module = null);

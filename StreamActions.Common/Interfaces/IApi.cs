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

namespace StreamActions.Common.Interfaces;

/// <summary>
/// An interface for a component which interacts with an API.
/// </summary>
public interface IApi : IComponent
{
    #region Public Properties

    /// <summary>
    /// Defines the timestamp of the latest API update that is included in the type.
    /// </summary>
    public static DateTime? ApiDate { get; }

    /// <summary>
    /// The API version.
    /// </summary>
    public static string? ApiVersion { get; }

    #endregion Public Properties
}

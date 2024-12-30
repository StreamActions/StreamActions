/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Provides the JSON string value for the attached enum value, for serialization by <see cref="JsonCustomEnumConverter{T}"/>.
/// </summary>
/// <remarks>
/// Attribute Constructor.
/// </remarks>
/// <param name="value">The serialized value of the enum value.</param>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class JsonCustomEnumAttribute(string value) : Attribute
{

    #region Public Constructors

    #endregion Public Constructors

    #region Public Properties

    /// <summary>
    /// The serialized value of the enum value.
    /// </summary>
    public string Value { get; init; } = value;

    #endregion Public Properties
}

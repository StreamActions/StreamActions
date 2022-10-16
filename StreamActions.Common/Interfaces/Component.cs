/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using System.Collections.Immutable;

namespace StreamActions.Common.Interfaces;

/// <summary>
/// A base interface containing common properties for sub-interfaces.
/// </summary>
public abstract class Component
{
    #region Public Properties

    /// <summary>
    /// The author of the component.
    /// </summary>
    public static string? Author { get; }

    /// <summary>
    /// A collection of <see cref="Guid"/> of other components that this component is dependent on.
    /// </summary>
    public static IReadOnlyCollection<Guid>? Dependencies => ImmutableArray<Guid>.Empty;

    /// <summary>
    /// A description of the component.
    /// </summary>
    public static string? Description { get; }

    /// <summary>
    /// A <see cref="Guid"/> used to identify the component.
    /// </summary>
    public static Guid? Id { get; }

    /// <summary>
    /// The name of the component.
    /// </summary>
    public static string? Name { get; }

    /// <summary>
    /// The Uri of the component.
    /// </summary>
    public static Uri? Uri { get; }

    /// <summary>
    /// The component version. Usage of semver as defined in CONTRIBUTING.md is preferred.
    /// </summary>
    public static Version? Version { get; }

    #endregion Public Properties
}

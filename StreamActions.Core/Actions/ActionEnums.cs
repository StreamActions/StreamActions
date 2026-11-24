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

namespace StreamActions.Core.Actions;

/// <summary>
/// Defines the UI input control and validation kind for an Action Block parameter or action schema parameter.
/// </summary>
public enum ActionInputKind
{
    /// <summary>
    /// A text or <c>{tag}</c> template string.
    /// </summary>
    TemplateString = 0,

    /// <summary>
    /// A whole integer value or template resolving to an integer.
    /// </summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Domain input kind names match the Action Block schema specification.")]
    Integer = 1,

    /// <summary>
    /// A decimal or floating-point number or template resolving to a number.
    /// </summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Domain input kind names match the Action Block schema specification.")]
    Decimal = 2,

    /// <summary>
    /// A boolean value (<see langword="true"/> or <see langword="false"/>).
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// A selection from a fixed list of allowed values.
    /// </summary>
    SelectList = 4,

    /// <summary>
    /// A reference to another Action Block task selected by name and persisted by <see cref="Guid"/>.
    /// </summary>
    TaskSelector = 5,

    /// <summary>
    /// A reference to an Action Block profile selected by name and persisted by <see cref="Guid"/>.
    /// </summary>
    ProfileSelector = 6,

    /// <summary>
    /// A reference to a ClearScript V8 VFS script selected by path and persisted by <see cref="Guid"/>.
    /// </summary>
    ScriptSelector = 7,

    /// <summary>
    /// A UTC date and time value (<see cref="DateTimeOffset"/>).
    /// </summary>
    DateTime = 8,
}

/// <summary>
/// Defines the boolean combinator linking an Action Block condition clause to the following clause.
/// </summary>
public enum ActionBlockLogicalCombinator
{
    /// <summary>
    /// Logical conjunction (<c>AND</c>).
    /// </summary>
    And = 0,

    /// <summary>
    /// Logical disjunction (<c>OR</c>).
    /// </summary>
    Or = 1,
}

/// <summary>
/// Defines the 12 comparison operators supported by Action Block profile predicates, inline step conditionals, and <see cref="IfActionBlock"/>.
/// </summary>
public enum ActionBlockComparisonOperator
{
    /// <summary>
    /// Equality (<c>==</c>).
    /// </summary>
    Equals = 0,

    /// <summary>
    /// Inequality (<c>!=</c>).
    /// </summary>
    NotEquals = 1,

    /// <summary>
    /// Numeric or lexicographical greater-than (<c>&gt;</c>).
    /// </summary>
    GreaterThan = 2,

    /// <summary>
    /// Numeric or lexicographical greater-than-or-equal (<c>&gt;=</c>).
    /// </summary>
    GreaterThanOrEqual = 3,

    /// <summary>
    /// Numeric or lexicographical less-than (<c>&lt;</c>).
    /// </summary>
    LessThan = 4,

    /// <summary>
    /// Numeric or lexicographical less-than-or-equal (<c>&lt;=</c>).
    /// </summary>
    LessThanOrEqual = 5,

    /// <summary>
    /// Case-insensitive substring containment (<c>Contains</c>).
    /// </summary>
    Contains = 6,

    /// <summary>
    /// Case-insensitive substring exclusion (<c>NotContains</c>).
    /// </summary>
    NotContains = 7,

    /// <summary>
    /// Set membership check (<c>In</c>).
    /// </summary>
    In = 8,

    /// <summary>
    /// Negated set membership check (<c>NotIn</c>).
    /// </summary>
    NotIn = 9,

    /// <summary>
    /// Regular expression match evaluated with <see cref="System.Text.RegularExpressions.RegexOptions.NonBacktracking"/> and a 50 ms timeout.
    /// </summary>
    MatchesRegex = 10,

    /// <summary>
    /// Negated regular expression match evaluated with <see cref="System.Text.RegularExpressions.RegexOptions.NonBacktracking"/> and a 50 ms timeout.
    /// </summary>
    NotMatchesRegex = 11,
}

/// <summary>
/// Defines the schedule mode for a Timer, Cron, or One-Shot Action Block profile trigger.
/// </summary>
public enum ProfileTriggerScheduleMode
{
    /// <summary>
    /// Not a scheduled trigger.
    /// </summary>
    None = 0,

    /// <summary>
    /// Fires on a fixed interval in seconds.
    /// </summary>
    Interval = 1,

    /// <summary>
    /// Fires according to a 5-field UTC cron expression.
    /// </summary>
    Cron = 2,

    /// <summary>
    /// Fires once at or after a configured UTC <see cref="DateTimeOffset"/> timestamp.
    /// </summary>
    OneShot = 3,
}

/// <summary>
/// Describes the execution outcome of an individual <see cref="ActionBlock"/> step during an Action Block flow run.
/// </summary>
public enum ActionStepExecutionStatus
{
    /// <summary>
    /// The action block executed successfully.
    /// </summary>
    Executed = 0,

    /// <summary>
    /// The action block was skipped because <see cref="ActionBlock.IsEnabled"/> is <see langword="false"/>.
    /// </summary>
    SkippedDisabled = 1,

    /// <summary>
    /// The action block was skipped because <see cref="ActionBlock.InlineCondition"/> evaluated to <see langword="false"/>.
    /// </summary>
    SkippedCondition = 2,

    /// <summary>
    /// The action block failed with an error.
    /// </summary>
    Failed = 3,
}

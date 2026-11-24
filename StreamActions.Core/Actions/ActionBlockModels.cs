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
/// Defines a typed input parameter exposed by a <see cref="WorkspaceActionBlockTask"/> or output parameter emitted by an <see cref="ITriggerDescriptor"/>.
/// </summary>
/// <param name="Name">The variable name used in <c>{var Name}</c> or parameter mapping.</param>
/// <param name="Label">The human-readable UI label.</param>
/// <param name="Kind">The input kind.</param>
/// <param name="IsRequired">Whether a value is required when executing or binding the task.</param>
/// <param name="DefaultValue">Optional default template value.</param>
/// <param name="PopulatedFromTriggerKey">The optional trigger output parameter key that populates this input parameter.</param>
public sealed record TaskParameterDefinition(
    string Name,
    string Label,
    ActionInputKind Kind = ActionInputKind.TemplateString,
    bool IsRequired = false,
    string? DefaultValue = null,
    string? PopulatedFromTriggerKey = null);

/// <summary>
/// Represents a single structured clause inside an <see cref="ActionBlockConditionGroup"/>.
/// </summary>
/// <param name="LeftExpression">The left-hand template expression or state key.</param>
/// <param name="Operator">The comparison operator to apply.</param>
/// <param name="RightExpression">The right-hand literal, list, regex, or template expression.</param>
/// <param name="CombinatorWithNext">The boolean combinator joining this clause with the subsequent clause.</param>
/// <param name="IndentLevel">Visual indentation / parenthesized grouping level (0 or greater).</param>
public sealed record ActionBlockConditionClause(
    string LeftExpression,
    ActionBlockComparisonOperator Operator,
    string RightExpression,
    ActionBlockLogicalCombinator CombinatorWithNext = ActionBlockLogicalCombinator.And,
    int IndentLevel = 0);

/// <summary>
/// Unified conditional model shared across profile predicates, inline step conditionals, and <see cref="IfActionBlock"/>.
/// </summary>
/// <param name="Clauses">Ordered structured clauses evaluated in Action Blocks visual builder mode.</param>
/// <param name="FreeformExpression">Optional freeform boolean expression evaluated when <paramref name="UseFreeform"/> is <see langword="true"/>.</param>
/// <param name="UseFreeform">Whether <paramref name="FreeformExpression"/> should be evaluated instead of <paramref name="Clauses"/>.</param>
public sealed record ActionBlockConditionGroup(
    IReadOnlyList<ActionBlockConditionClause> Clauses,
    string? FreeformExpression = null,
    bool UseFreeform = false);

/// <summary>
/// Defines the primary trigger configuration for a <see cref="WorkspaceActionBlockProfile"/>.
/// </summary>
public sealed class ProfileTriggerDefinition
{
    /// <summary>
    /// Gets or sets the descriptor identifier (for example, <c>core.chat.command</c>, <c>core.schedule.timer</c>, or <c>twitch.subscription</c>).
    /// </summary>
    public string TriggerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional platform filter (<see langword="null"/> matches all platforms in the workspace).
    /// </summary>
    public string? PlatformId { get; set; }

    /// <summary>
    /// Gets or sets the command trigger matched when <see cref="TriggerId"/> is a command trigger.
    /// </summary>
    public string? CommandTrigger { get; set; }

    /// <summary>
    /// Gets or sets the keyword or pattern matched when <see cref="TriggerId"/> is a keyword trigger.
    /// </summary>
    public string? KeywordPattern { get; set; }

    /// <summary>
    /// Gets or sets the schedule mode when <see cref="TriggerId"/> is a scheduled trigger.
    /// </summary>
    public ProfileTriggerScheduleMode ScheduleMode { get; set; } = ProfileTriggerScheduleMode.None;

    /// <summary>
    /// Gets or sets the interval in seconds when <see cref="ScheduleMode"/> is <see cref="ProfileTriggerScheduleMode.Interval"/>.
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the 5-field UTC cron expression when <see cref="ScheduleMode"/> is <see cref="ProfileTriggerScheduleMode.Cron"/>.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the target UTC date and time when <see cref="ScheduleMode"/> is <see cref="ProfileTriggerScheduleMode.OneShot"/>.
    /// </summary>
    public DateTimeOffset? ScheduledAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when a <see cref="ProfileTriggerScheduleMode.OneShot"/> trigger fired, preventing duplicate fires across restarts.
    /// </summary>
    public DateTimeOffset? OneShotFiredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets additional trigger-specific settings.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Mutable EF Core entity property replaced during store updates.")]
    public Dictionary<string, string> Settings { get; set; } = [];
}

/// <summary>
/// Represents an operator-named, workspace-scoped Action Block flow.
/// </summary>
public sealed class WorkspaceActionBlockTask
{
    /// <summary>
    /// Gets or sets the immutable internal task identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the owning workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the operator-assigned workspace-unique display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the task is enabled for execution.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the ordered typed input parameter definitions for this task.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Mutable EF Core entity list matching the Action Block model specification.")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Mutable EF Core entity property replaced during store updates.")]
    public List<TaskParameterDefinition> InputParameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the ordered sequence of polymorphic <see cref="ActionBlock"/> steps in this task.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Mutable EF Core entity list matching the Action Block model specification.")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Mutable EF Core entity property replaced during store updates.")]
    public List<ActionBlock> Blocks { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Represents an operator-named, workspace-scoped Action Block Profile binding a primary trigger and state predicates to a <see cref="WorkspaceActionBlockTask"/>.
/// </summary>
public sealed class WorkspaceActionBlockProfile
{
    /// <summary>
    /// Gets or sets the immutable internal profile identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the owning workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the operator-assigned workspace-unique display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this profile is active.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the primary trigger definition for this profile.
    /// </summary>
    public ProfileTriggerDefinition Trigger { get; set; } = new();

    /// <summary>
    /// Gets or sets optional multi-predicate conditions that must all evaluate to <see langword="true"/> before firing the attached task.
    /// </summary>
    public ActionBlockConditionGroup? Predicates { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the <see cref="WorkspaceActionBlockTask"/> executed when this profile triggers.
    /// </summary>
    public Guid AttachedTaskId { get; set; }

    /// <summary>
    /// Gets or sets explicit mappings from task input parameter names to trigger template expressions.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Mutable EF Core entity property replaced during store updates.")]
    public Dictionary<string, string> ParameterMappings { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

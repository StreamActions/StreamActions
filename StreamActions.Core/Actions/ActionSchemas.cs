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

namespace StreamActions.Core.Actions;

/// <summary>
/// Declares a typed parameter accepted by an <see cref="IPlatformActionHandler"/>.
/// </summary>
/// <param name="Key">The unique parameter key.</param>
/// <param name="Label">The UI display label.</param>
/// <param name="Kind">The input kind.</param>
/// <param name="IsRequired">Whether the parameter is required.</param>
/// <param name="DefaultValue">Optional default parameter value or template.</param>
/// <param name="AllowedValues">Optional fixed list of allowed values when <paramref name="Kind"/> is <see cref="ActionInputKind.SelectList"/>.</param>
public sealed record ActionParameterSchema(
    string Key,
    string Label,
    ActionInputKind Kind,
    bool IsRequired = true,
    string? DefaultValue = null,
    IReadOnlyList<string>? AllowedValues = null);

/// <summary>
/// Contract implemented by core flow modules and platform UI plugins to contribute schema-driven Action Blocks.
/// </summary>
public interface IPlatformActionHandler
{
    /// <summary>
    /// Gets the unique action identifier (for example, <c>twitch.api.clips.create</c>).
    /// </summary>
    string ActionId { get; }

    /// <summary>
    /// Gets the owning platform identifier (for example, <c>core</c>, <c>twitch</c>, <c>youtube</c>, or <c>discord</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the multi-level hierarchical category path used to render nested action menus (for example, <c>["Twitch", "Api", "Clips"]</c>).
    /// </summary>
    IReadOnlyList<string> CategoryPath { get; }

    /// <summary>
    /// Gets the human-readable action display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the optional permission key required to configure or execute this action.
    /// </summary>
    string RequiredPermissionKey { get; }

    /// <summary>
    /// Gets the ordered parameter schemas displayed in the Action Block editor.
    /// </summary>
    IReadOnlyList<ActionParameterSchema> Parameters { get; }

    /// <summary>
    /// Gets the optional default flow variable name populated with this action's string return value.
    /// </summary>
    string? DefaultOutputVariableName => null;

    /// <summary>
    /// Executes the action with template-resolved parameters.
    /// </summary>
    /// <param name="context">The active execution context.</param>
    /// <param name="resolvedParameters">The resolved parameter dictionary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An optional string output stored into the configured output variable.</returns>
    ValueTask<string?> ExecuteAsync(
        IActionExecutionContext context,
        IReadOnlyDictionary<string, string> resolvedParameters,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes an event or schedule trigger that can be bound to a <see cref="WorkspaceActionBlockProfile"/>.
/// </summary>
public interface ITriggerDescriptor
{
    /// <summary>
    /// Gets the unique trigger identifier (for example, <c>core.chat.command</c>, <c>core.schedule.timer</c>, or <c>twitch.subscription</c>).
    /// </summary>
    string TriggerId { get; }

    /// <summary>
    /// Gets the owning platform identifier (for example, <c>core</c> or <c>twitch</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the multi-level hierarchical category path for trigger pickers.
    /// </summary>
    IReadOnlyList<string> CategoryPath { get; }

    /// <summary>
    /// Gets the human-readable trigger display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets a concise description of when this trigger fires.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets a value indicating whether this trigger configures an interval or cron schedule.
    /// </summary>
    bool SupportsSchedule => false;

    /// <summary>
    /// Gets the ordered list of output parameters emitted by this trigger and auto-populated into bound tasks.
    /// </summary>
    IReadOnlyList<TaskParameterDefinition> OutputParameters { get; }
}

/// <summary>
/// Represents a single node in the multi-level nested action menu hierarchy built from <see cref="IPlatformActionHandler.CategoryPath"/>.
/// </summary>
/// <param name="Name">The category display name at this level.</param>
/// <param name="Children">Child sub-category nodes ordered alphabetically.</param>
/// <param name="Actions">Action handlers registered directly under this category node.</param>
public sealed record ActionCategoryNode(
    string Name,
    IReadOnlyList<ActionCategoryNode> Children,
    IReadOnlyList<IPlatformActionHandler> Actions);

/// <summary>
/// Provides lookup, multi-level category tree construction, and live search across registered <see cref="IPlatformActionHandler"/> and <see cref="ITriggerDescriptor"/> instances.
/// </summary>
public interface IActionBlockCatalog
{
    /// <summary>
    /// Retrieves all registered action handlers.
    /// </summary>
    /// <returns>All registered <see cref="IPlatformActionHandler"/> instances.</returns>
    IReadOnlyList<IPlatformActionHandler> GetAllActions();

    /// <summary>
    /// Retrieves an action handler by its unique <see cref="IPlatformActionHandler.ActionId"/>.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <returns>The matching handler, or <see langword="null"/> if not found.</returns>
    IPlatformActionHandler? GetAction(string actionId);

    /// <summary>
    /// Builds the multi-level nested category tree for action pickers.
    /// </summary>
    /// <returns>Top-level category nodes.</returns>
    IReadOnlyList<ActionCategoryNode> BuildActionCategoryTree();

    /// <summary>
    /// Searches registered actions across <see cref="IPlatformActionHandler.DisplayName"/>, <see cref="IPlatformActionHandler.CategoryPath"/>, and <see cref="IPlatformActionHandler.ActionId"/>.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <returns>Matching action handlers ordered by relevance and display name.</returns>
    IReadOnlyList<IPlatformActionHandler> SearchActions(string? query);

    /// <summary>
    /// Retrieves all registered trigger descriptors.
    /// </summary>
    /// <returns>All registered <see cref="ITriggerDescriptor"/> instances.</returns>
    IReadOnlyList<ITriggerDescriptor> GetAllTriggers();

    /// <summary>
    /// Retrieves a trigger descriptor by its unique <see cref="ITriggerDescriptor.TriggerId"/>.
    /// </summary>
    /// <param name="triggerId">The trigger identifier.</param>
    /// <returns>The matching descriptor, or <see langword="null"/> if not found.</returns>
    ITriggerDescriptor? GetTrigger(string triggerId);
}


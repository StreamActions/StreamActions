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

using Microsoft.Extensions.Localization;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides structured JSON localization for UI strings, bot response templates, and localized command triggers.
/// </summary>
/// <remarks>
/// <para>
/// Inherits <see cref="IStringLocalizer"/> for synchronous Blazor/UI localization backed by <see cref="CultureInfo.CurrentUICulture"/>,
/// while providing workspace-aware asynchronous resolution using <see cref="Workspace.BotCulture"/> and workspace database overrides.
/// </para>
/// </remarks>
public interface IStreamActionsLocalizer : IStringLocalizer
{
    /// <summary>
    /// Resolves a localized string using the 4-layer fallback chain (Workspace DB Override, Exact Culture, Parent Neutral Culture, Default Culture).
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="module">The catalog module name (for example, <c>core</c> or <c>moderation</c>).</param>
    /// <param name="key">The dotted localization key (for example, <c>ui.title</c>).</param>
    /// <param name="cultureOverride">An optional BCP-47 culture override that takes precedence over the workspace bot culture.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The localized string if found; otherwise, <see langword="null"/>.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Parameter name 'module' is part of the public localization API contract and represents a catalog module identifier.")]
    ValueTask<string?> GetStringAsync(
        Guid workspaceId,
        string module,
        string key,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a raw bot response template containing unformatted placeholders (such as <c>{sender}</c> or <c>{1}</c>).
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="module">The catalog module name.</param>
    /// <param name="key">The response key (with or without the <c>responses.</c> prefix).</param>
    /// <param name="cultureOverride">An optional BCP-47 culture override that takes precedence over the workspace bot culture.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The raw localized response template if found; otherwise, <see langword="null"/>.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Parameter name 'module' is part of the public localization API contract and represents a catalog module identifier.")]
    ValueTask<string?> GetResponseTemplateAsync(
        Guid workspaceId,
        string module,
        string key,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all command definitions for a workspace with combined override, localized, parent, and canonical <c>en-US</c> triggers.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cultureOverride">An optional BCP-47 culture override that takes precedence over the workspace bot culture.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of resolved <see cref="LocalizedCommandDefinition"/> entries.</returns>
    ValueTask<IReadOnlyList<LocalizedCommandDefinition>> GetCommandDefinitionsAsync(
        Guid workspaceId,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a localized or fallback chat subcommand trigger to its canonical command identifier.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="trigger">The trigger phrase entered in chat.</param>
    /// <param name="cultureOverride">An optional BCP-47 culture override that takes precedence over the workspace bot culture.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching canonical command identifier, or <see langword="null"/> if no command matches.</returns>
    ValueTask<string?> ResolveCommandIdAsync(
        Guid workspaceId,
        string trigger,
        string? cultureOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a workspace-specific localization string override and evicts the workspace localization cache.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="module">The catalog module name.</param>
    /// <param name="key">The dotted localization key.</param>
    /// <param name="value">The override string value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Parameter name 'module' is part of the public localization API contract and represents a catalog module identifier.")]
    ValueTask SetWorkspaceOverrideAsync(
        Guid workspaceId,
        string module,
        string key,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a workspace-specific localization string override and evicts the workspace localization cache.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="module">The catalog module name.</param>
    /// <param name="key">The dotted localization key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if an override existed and was deleted; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Parameter name 'module' is part of the public localization API contract and represents a catalog module identifier.")]
    ValueTask<bool> RemoveWorkspaceOverrideAsync(
        Guid workspaceId,
        string module,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists workspace-specific custom command trigger overrides for a command and evicts the workspace localization cache.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="commandId">The canonical command identifier.</param>
    /// <param name="triggers">The workspace-custom trigger phrases to associate with the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SetWorkspaceCommandTriggersAsync(
        Guid workspaceId,
        string commandId,
        IReadOnlyCollection<string> triggers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached localization entries for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InvalidateWorkspaceCacheAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all BCP-47 culture codes currently available in the loaded localization catalogs.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A sorted read-only list of available culture codes.</returns>
    ValueTask<IReadOnlyList<string>> GetAvailableCulturesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads all JSON localization catalogs from disk into memory.
    /// </summary>
    void ReloadCatalogs();
}

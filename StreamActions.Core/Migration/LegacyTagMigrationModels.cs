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

using StreamActions.Core.Models;
using StreamActions.Core.Templates;

namespace StreamActions.Core.Migration;

/// <summary>
/// Identifies the legacy chatbot platform syntax from which a command template is being migrated.
/// </summary>
public enum LegacyBotSource
{
    /// <summary>
    /// PhantomBot parenthesized <c>(tag)</c> command syntax.
    /// </summary>
    PhantomBot = 0,

    /// <summary>
    /// Nightbot <c>$(tag)</c> command syntax.
    /// </summary>
    Nightbot = 1,

    /// <summary>
    /// StreamElements <c>${tag}</c> command syntax.
    /// </summary>
    StreamElements = 2,

    /// <summary>
    /// Streamlabs Chatbot <c>$tag</c> and <c>$tag(...)</c> command syntax.
    /// </summary>
    Streamlabs = 3,

    /// <summary>
    /// Firebot <c>$tag</c> and <c>$tag[...]</c> variable syntax.
    /// </summary>
    Firebot = 4,

    /// <summary>
    /// Streamer.bot <c>%var%</c> variable syntax.
    /// </summary>
    StreamerBot = 5,

    /// <summary>
    /// Mix It Up <c>$var</c> and <c>%var%</c> variable syntax.
    /// </summary>
    MixItUp = 6,
}

/// <summary>
/// Defines the severity level of a <see cref="MigrationDiagnostic"/> emitted by <see cref="LegacyTagMigrationConverter"/>.
/// </summary>
public enum MigrationDiagnosticSeverity
{
    /// <summary>
    /// Informational diagnostic describing a successful extraction, unwrap, or automatic rewrite.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Non-fatal warning highlighting stripped security risks, ClearScript promotions, Tasker upgrades, or AST parse warnings.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error indicating a syntax or validation failure in the converted template.
    /// </summary>
    Error = 2,
}

/// <summary>
/// Identifies the specific condition recorded in a <see cref="MigrationDiagnostic"/>.
/// </summary>
public enum MigrationDiagnosticCode
{
    /// <summary>
    /// A Stage 1 meta guard tag such as <c>(adminonlyedit)</c> or <c>(onlineonly)</c> was extracted into structured command metadata.
    /// </summary>
    ExtractedMetaGuard = 0,

    /// <summary>
    /// A prohibited <c>(unescape ...)</c> tag was unwrapped or stripped so external or user data stays strictly literal.
    /// </summary>
    StrippedUnescapeTag = 1,

    /// <summary>
    /// A legacy escape wrapper or strip-escape modifier such as <c>(escape ...)</c>, <c>(1!)</c>, <c>(echo!)</c>, or <c>(cpinput!)</c> was removed.
    /// </summary>
    StrippedEscapeModifier = 2,

    /// <summary>
    /// A host filesystem tag such as <c>(readfile)</c>, <c>(readfilerand)</c>, <c>(writefile)</c>, <c>$readfile</c>, or <c>$overwritefile</c> was rewritten to scoped KV tags.
    /// </summary>
    ReplacedFileTag = 3,

    /// <summary>
    /// A <c>(token)</c> tag inside an external HTTP tag was rewritten to a <c>$secret.*_TOKEN</c> placeholder with host allowlist metadata.
    /// </summary>
    ReplacedTokenTag = 4,

    /// <summary>
    /// A standalone <c>(token)</c> tag outside an HTTP request tag was stripped so secrets cannot leak in chat.
    /// </summary>
    StrippedStandaloneSecretTag = 5,

    /// <summary>
    /// A safe Nightbot <c>$(eval ...)</c> idiom was automatically transpiled into Tier 1 StreamActions AST tags.
    /// </summary>
    TranspiledEvalExpression = 6,

    /// <summary>
    /// A complex Nightbot <c>$(eval ...)</c> script was promoted to a ClearScript V8 action block requiring broadcaster review.
    /// </summary>
    PromotedEvalToClearScript = 7,

    /// <summary>
    /// A legacy side-effect tag was upgraded to one or more Tasker <see cref="MigratedActionBlockDescriptor"/> steps.
    /// </summary>
    UpgradedSideEffectToTasker = 8,

    /// <summary>
    /// A warning emitted by <see cref="TemplateParser"/> when validating the converted template.
    /// </summary>
    TemplateParseWarning = 9,

    /// <summary>
    /// An error emitted by <see cref="TemplateParser"/> when validating the converted template.
    /// </summary>
    TemplateParseError = 10,
}

/// <summary>
/// Represents a structured diagnostic produced during legacy command tag migration.
/// </summary>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Code">The categorized diagnostic code.</param>
/// <param name="Message">The human-readable diagnostic description.</param>
/// <param name="SourceFragment">The optional original legacy tag fragment associated with this diagnostic.</param>
public sealed record MigrationDiagnostic(
    MigrationDiagnosticSeverity Severity,
    MigrationDiagnosticCode Code,
    string Message,
    string? SourceFragment = null);

/// <summary>
/// Represents a workspace secret placeholder discovered during migration along with its authorized target hostnames.
/// </summary>
/// <param name="SecretName">The normalized secret name (for example, <c>WEATHER_TOKEN</c>).</param>
/// <param name="AllowedHostPatterns">The deduplicated target hostnames discovered in the originating HTTP tags.</param>
public sealed record DiscoveredSecretReference(
    string SecretName,
    IReadOnlyList<string> AllowedHostPatterns);

/// <summary>
/// Describes a Tasker action block synthesized during Stage 2 or Stage 4 migration upgrades.
/// </summary>
/// <param name="ActionType">The Tasker action type identifier (for example, <c>platform.invoke</c> or <c>script.clearscript</c>).</param>
/// <param name="Parameters">The immutable key-value parameter map for the action block.</param>
/// <param name="RequiredPermissionKey">The optional SMF permission key required to edit or execute this action block.</param>
/// <param name="RequiresBroadcasterReview">Whether the upgraded block requires explicit broadcaster review before enabling.</param>
public sealed record MigratedActionBlockDescriptor(
    string ActionType,
    IReadOnlyDictionary<string, string> Parameters,
    string? RequiredPermissionKey = null,
    bool RequiresBroadcasterReview = false);

/// <summary>
/// Represents an input request to <see cref="LegacyTagMigrationConverter.Convert"/>.
/// </summary>
/// <param name="CommandTrigger">The legacy command trigger name used for normalizing generated secret names.</param>
/// <param name="RawTemplate">The raw legacy command response template text.</param>
/// <param name="Source">The originating legacy chatbot syntax family.</param>
/// <param name="PlatformId">Optional target platform identifier passed to Stage 4 <see cref="TemplateParser"/> validation.</param>
public sealed record LegacyCommandMigrationRequest(
    string CommandTrigger,
    string RawTemplate,
    LegacyBotSource Source = LegacyBotSource.PhantomBot,
    string? PlatformId = null);

/// <summary>
/// Represents the deterministic output of converting a legacy chatbot command template through the 4-stage <see cref="LegacyTagMigrationConverter"/> pipeline.
/// </summary>
/// <param name="ConvertedTemplate">The transpiled StreamActions <c>{tag}</c> template string.</param>
/// <param name="ParseResult">The Stage 4 <see cref="TemplateParseResult"/> produced by <see cref="TemplateParser"/>.</param>
/// <param name="LockModeratorEdits">Whether <c>(adminonlyedit)</c> was extracted from the template.</param>
/// <param name="AllowedUsers">Users extracted from <c>(useronly ...)</c> guard tags.</param>
/// <param name="StreamStatusRequirement">Online/offline requirement extracted from <c>(onlineonly)</c> or <c>(offlineonly)</c>.</param>
/// <param name="AllowedGames">Game/category restrictions extracted from <c>(gameonly ...)</c>.</param>
/// <param name="MinRequiredArgs">Minimum required argument count extracted from <c>(requireargs ...)</c>.</param>
/// <param name="DiscoveredSecrets">Workspace secrets discovered from migrated <c>(token)</c> placeholders.</param>
/// <param name="ReferencedVirtualFiles">Virtual file names referenced by migrated file tags for datastore import.</param>
/// <param name="RequiresTaskerUpgrade">Whether side-effect or complex script tags require upgrading the command to a Tasker routine.</param>
/// <param name="UpgradedActions">Ordered list of synthesized Tasker action block descriptors.</param>
/// <param name="Diagnostics">Ordered list of migration diagnostics across all four stages.</param>
public sealed record LegacyCommandMigrationResult(
    string ConvertedTemplate,
    TemplateParseResult ParseResult,
    bool LockModeratorEdits,
    IReadOnlyList<string> AllowedUsers,
    StreamStatusRequirement StreamStatusRequirement,
    IReadOnlyList<string> AllowedGames,
    int? MinRequiredArgs,
    IReadOnlyList<DiscoveredSecretReference> DiscoveredSecrets,
    IReadOnlyList<string> ReferencedVirtualFiles,
    bool RequiresTaskerUpgrade,
    IReadOnlyList<MigratedActionBlockDescriptor> UpgradedActions,
    IReadOnlyList<MigrationDiagnostic> Diagnostics);

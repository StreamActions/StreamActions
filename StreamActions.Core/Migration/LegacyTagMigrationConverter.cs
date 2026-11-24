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
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Migration;

/// <summary>
/// Deterministic 4-stage AST transpiler that converts legacy chatbot command templates
/// (PhantomBot, Nightbot, StreamElements, Streamlabs, Firebot, Streamer.bot, and Mix It Up)
/// into safe StreamActions <c>{tag}</c> templates, extracted command guards, secret references,
/// virtual file references, and Action Block upgrade descriptors.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="LegacyTagMigrationConverter"/>.
/// </remarks>
/// <param name="tagRegistry">Optional tag registry for Stage 4 validation; defaults to <see cref="CoreTagRegistryFactory.CreateDefaultRegistry()"/>.</param>
public sealed partial class LegacyTagMigrationConverter(ITagRegistry? tagRegistry = null)
{
    /// <summary>
    /// Tag registry passed to Stage 4 <see cref="TemplateParser.Parse"/> validation.
    /// </summary>
    private readonly ITagRegistry _tagRegistry = tagRegistry ?? CoreTagRegistryFactory.CreateDefaultRegistry();

    /// <summary>
    /// Matches a Nightbot inline array random-pick expression such as <c>['a','b'][Math.floor(Math.random()*2)]</c>.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"^\s*\[(?<items>.*)\]\s*\[\s*Math\.(?:floor|trunc)\s*\(\s*Math\.random\s*\(\s*\)\s*\*\s*(?<bound>[^\]\)]+)\)\s*\]\s*;?\s*$", RegexOptions.Singleline)]
    private static partial Regex InlineArrayRandomPickRegex();

    /// <summary>
    /// Matches a Nightbot two-statement array variable random-pick expression such as <c>var a = ['x', 'y']; a[Math.floor(Math.random() * a.length)]</c>.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"^\s*(?:const|let|var)\s+(?<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\[(?<items>.*)\]\s*;\s*(?:return\s+)?\k<var>\s*\[\s*Math\.(?:floor|trunc)\s*\(\s*Math\.random\s*\(\s*\)\s*\*\s*(?:\k<var>\.length|\d+)\s*\)\s*\]\s*;?\s*$", RegexOptions.Singleline)]
    private static partial Regex VariableArrayRandomPickRegex();

    /// <summary>
    /// Matches a Nightbot simple <c>.replace('a', 'b')</c> script expression.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"^\s*(?<target>.+?)\.replace\s*\(\s*(?<find>(?:'(?:\\.|[^'])*'|""(?:\\.|[^""])*""))\s*,\s*(?<repl>(?:'(?:\\.|[^'])*'|""(?:\\.|[^""])*""))\s*\)\s*;?\s*$", RegexOptions.Singleline)]
    private static partial Regex SimpleStringReplaceEvalRegex();

    /// <summary>
    /// Matches a Nightbot <c>{{metric}}</c> placeholder inside <c>$(twitch ...)</c> format strings.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"\{\{\s*(?<metric>[A-Za-z0-9_]+)\s*\}\}")]
    private static partial Regex NightbotTwitchMetricRegex();

    /// <summary>
    /// Matches <c>$secret.NAME</c> placeholders so extracted URL hosts remain clean.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"\$secret\.[A-Z0-9_]+")]
    private static partial Regex SecretPlaceholderRegex();

    /// <summary>
    /// Matches runs of two or more horizontal spaces when cleaning removed inline tags.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex MultiHorizontalSpaceRegex();

    /// <summary>
    /// Converts <paramref name="request"/> through the 4-stage migration pipeline and returns the validated result.
    /// </summary>
    /// <param name="request">The migration request containing command trigger, raw legacy template, and bot source.</param>
    /// <returns>A <see cref="LegacyCommandMigrationResult"/> containing the transpiled template, AST parse result, extracted metadata, secrets, virtual files, Action Block upgrades, and diagnostics.</returns>
    public LegacyCommandMigrationResult Convert(LegacyCommandMigrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        MigrationContext context = new(request.CommandTrigger, request.Source);
        string workingTemplate = request.RawTemplate ?? string.Empty;

        // Stage 1: Extract Meta Guard Tags
        if (request.Source == LegacyBotSource.PhantomBot)
        {
            workingTemplate = ExtractPhantomBotMetaGuards(workingTemplate, context);
        }

        // Stages 2, 3 & inline Stage 4 side-effect extraction
        workingTemplate = request.Source switch
        {
            LegacyBotSource.PhantomBot => ConvertPhantomBot(workingTemplate, context, insideHttpTag: false),
            LegacyBotSource.Nightbot => ConvertNightbot(workingTemplate, context, insideUrl: false),
            LegacyBotSource.StreamElements => ConvertStreamElements(workingTemplate, context),
            LegacyBotSource.Streamlabs => ConvertStreamlabs(workingTemplate, context),
            LegacyBotSource.Firebot => ConvertFirebot(workingTemplate, context),
            LegacyBotSource.StreamerBot => ConvertStreamerBotAndMixItUp(workingTemplate, context, allowMixItUpDollarVars: false),
            LegacyBotSource.MixItUp => ConvertStreamerBotAndMixItUp(workingTemplate, context, allowMixItUpDollarVars: true),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Source, "Unsupported legacy bot source."),
        };

        string convertedTemplate = NormalizeStrippedSpacing(workingTemplate);

        // Stage 4: Parse final AST and forward parse diagnostics
        TemplateParseResult parseResult = TemplateParser.Parse(convertedTemplate, this._tagRegistry, request.PlatformId);
        foreach (TemplateDiagnostic parseDiag in parseResult.Diagnostics)
        {
            MigrationDiagnosticSeverity severity = parseDiag.Severity == TemplateDiagnosticSeverity.Error
                ? MigrationDiagnosticSeverity.Error
                : MigrationDiagnosticSeverity.Warning;
            MigrationDiagnosticCode code = parseDiag.Severity == TemplateDiagnosticSeverity.Error
                ? MigrationDiagnosticCode.TemplateParseError
                : MigrationDiagnosticCode.TemplateParseWarning;
            context.Diagnostics.Add(new MigrationDiagnostic(severity, code, parseDiag.Message));
        }

        return new LegacyCommandMigrationResult(
            ConvertedTemplate: convertedTemplate,
            ParseResult: parseResult,
            LockModeratorEdits: context.LockModeratorEdits,
            AllowedUsers: context.AllowedUsers.AsReadOnly(),
            StreamStatusRequirement: context.StreamStatusRequirement,
            AllowedGames: context.AllowedGames.AsReadOnly(),
            MinRequiredArgs: context.MinRequiredArgs,
            DiscoveredSecrets: context.BuildSecretReferences(),
            ReferencedVirtualFiles: context.ReferencedVirtualFiles.AsReadOnly(),
            RequiresActionBlockUpgrade: context.RequiresActionBlockUpgrade,
            UpgradedActions: context.UpgradedActions.AsReadOnly(),
            Diagnostics: context.Diagnostics.AsReadOnly());
    }

    #region Stage 1 - Meta Guard Extraction

    /// <summary>
    /// Stage 1: Extracts and strips PhantomBot meta guard tags (<c>(adminonlyedit)</c>, <c>(useronly ...)</c>, <c>(onlineonly)</c>, <c>(offlineonly)</c>, <c>(gameonly ...)</c>, <c>(requireargs ...)</c>).
    /// </summary>
    /// <param name="input">The raw template text.</param>
    /// <param name="context">The migration context collecting extracted guards and diagnostics.</param>
    /// <returns>The template string with meta guard tags removed.</returns>
    private static string ExtractPhantomBotMetaGuards(string input, MigrationContext context)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('(', StringComparison.Ordinal))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (input[index] == '(' && TryFindBalancedClosingParen(input, index, respectJsStrings: false, out int closeIndex))
            {
                string rawTag = input[index..(closeIndex + 1)];
                string inner = input[(index + 1)..closeIndex];
                if (TryApplyMetaGuard(inner, rawTag, context))
                {
                    index = closeIndex + 1;
                    continue;
                }
            }

            _ = output.Append(input[index]);
            index++;
        }

        return NormalizeStrippedSpacing(output.ToString());
    }

    /// <summary>
    /// Attempts to match and extract a Stage 1 PhantomBot meta guard expression from <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The text inside parentheses.</param>
    /// <param name="rawTag">The full original <c>(...)</c> fragment for diagnostics.</param>
    /// <param name="context">The migration context.</param>
    /// <returns><see langword="true"/> if a meta guard was matched and extracted; otherwise, <see langword="false"/>.</returns>
    private static bool TryApplyMetaGuard(string inner, string rawTag, MigrationContext context)
    {
        string trimmed = inner.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        SplitFirstToken(trimmed, out string name, out string args);

        if (string.Equals(name, "adminonlyedit", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(args))
        {
            context.LockModeratorEdits = true;
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.ExtractedMetaGuard,
                "Extracted (adminonlyedit) guard to LockModeratorEdits.",
                rawTag));
            return true;
        }

        if (string.Equals(name, "onlineonly", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(args))
        {
            context.StreamStatusRequirement = StreamStatusRequirement.OnlineOnly;
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.ExtractedMetaGuard,
                "Extracted (onlineonly) guard to StreamStatusRequirement.OnlineOnly.",
                rawTag));
            return true;
        }

        if (string.Equals(name, "offlineonly", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(args))
        {
            context.StreamStatusRequirement = StreamStatusRequirement.OfflineOnly;
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.ExtractedMetaGuard,
                "Extracted (offlineonly) guard to StreamStatusRequirement.OfflineOnly.",
                rawTag));
            return true;
        }

        if (string.Equals(name, "useronly", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args))
        {
            string[] users = args.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string user in users)
            {
                string cleaned = user.TrimStart('@');
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    context.AddAllowedUser(cleaned);
                }
            }

            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.ExtractedMetaGuard,
                "Extracted (useronly) guard to AllowedUsers.",
                rawTag));
            return true;
        }

        if (string.Equals(name, "gameonly", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args))
        {
            string[] games = args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string game in games)
            {
                if (!string.IsNullOrWhiteSpace(game))
                {
                    context.AddAllowedGame(game);
                }
            }

            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.ExtractedMetaGuard,
                "Extracted (gameonly) guard to AllowedGames.",
                rawTag));
            return true;
        }

        if (string.Equals(name, "requireargs", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(args.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minArgs)
            && minArgs >= 0)
        {
            context.MinRequiredArgs = context.MinRequiredArgs is int existing
                ? Math.Max(existing, minArgs)
                : minArgs;
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.ExtractedMetaGuard,
                $"Extracted (requireargs {minArgs}) guard to MinRequiredArgs.",
                rawTag));
            return true;
        }

        return false;
    }

    #endregion

    #region PhantomBot Transpiler (Stages 2, 3, and 4)

    /// <summary>
    /// Recursively transpiles PhantomBot <c>(tag)</c> expressions into StreamActions <c>{tag}</c> syntax.
    /// </summary>
    /// <param name="input">The PhantomBot template fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether the current fragment resides inside an HTTP request tag where <c>(token)</c> is permitted.</param>
    /// <returns>The converted template fragment.</returns>
    private static string ConvertPhantomBot(string input, MigrationContext context, bool insideHttpTag)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('(', StringComparison.Ordinal))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (input[index] == '(' && TryFindBalancedClosingParen(input, index, respectJsStrings: false, out int closeIndex))
            {
                string rawTag = input[index..(closeIndex + 1)];
                string inner = input[(index + 1)..closeIndex];
                if (TryConvertPhantomBotTag(inner, rawTag, context, insideHttpTag, out string replacement))
                {
                    _ = output.Append(replacement);
                    index = closeIndex + 1;
                    continue;
                }

                // Unrecognized parenthesized text (e.g. English prose): keep outer parens while converting inner tags.
                _ = output.Append('(').Append(ConvertPhantomBot(inner, context, insideHttpTag)).Append(')');
                index = closeIndex + 1;
                continue;
            }

            _ = output.Append(input[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Attempts to convert a single PhantomBot <c>(inner)</c> tag expression across Stages 2, 3, and 4.
    /// </summary>
    /// <param name="inner">The text inside parentheses.</param>
    /// <param name="rawTag">The full original <c>(...)</c> fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether the tag sits inside an HTTP request tag.</param>
    /// <param name="replacement">When this method returns <see langword="true"/>, contains the converted output.</param>
    /// <returns><see langword="true"/> if <paramref name="inner"/> matched a known PhantomBot tag; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions tag names are lowercase ASCII identifiers.")]
    private static bool TryConvertPhantomBotTag(
        string inner,
        string rawTag,
        MigrationContext context,
        bool insideHttpTag,
        out string replacement)
    {
        string trimmed = inner.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            replacement = string.Empty;
            return false;
        }

        // Positional fallback patterns: (1>=rhs), (1=rhs), (1|default)
        if (TryConvertPhantomBotFallbackTag(trimmed, context, insideHttpTag, out replacement))
        {
            return true;
        }

        // Positional arguments: (1)..(9), (1!)..(9!), (1>)..(9>)
        if (TryConvertPhantomBotPositionalTag(trimmed, rawTag, context, out replacement))
        {
            return true;
        }

        // Random integer tag: (#) or (# min, max)
        if (trimmed == "#" || (trimmed.Length > 1 && trimmed[0] == '#' && char.IsWhiteSpace(trimmed[1])))
        {
            string rangeArgs = trimmed.Length > 1 ? trimmed[1..].Trim() : string.Empty;
            if (string.IsNullOrEmpty(rangeArgs))
            {
                replacement = "{random_int 1 100}";
                return true;
            }

            List<string> parts = SplitCommaOrSpaceArgs(rangeArgs, context, insideHttpTag, isPhantomBot: true);
            replacement = parts.Count >= 2
                ? $"{{random_int {parts[0]} {parts[1]}}}"
                : $"{{random_int {parts[0]}}}";
            return true;
        }

        SplitFirstToken(trimmed, out string rawName, out string rawArgs);
        string name = rawName.ToLowerInvariant();

        // Stage 2: Prohibited / Dangerous Tags
        if (TryConvertPhantomBotStage2Tag(name, rawArgs, rawTag, context, insideHttpTag, out replacement))
        {
            return true;
        }

        // Stage 4: Side-Effect Tags upgraded to Action Blocks
        if (TryConvertPhantomBotSideEffectTag(name, rawArgs, rawTag, context, out replacement))
        {
            return true;
        }

        // Stage 3: Standard PhantomBot Tags (Modules 3.1 - 3.8)
        return TryConvertPhantomBotStage3Tag(name, rawArgs, context, insideHttpTag, out replacement);
    }

    /// <summary>
    /// Handles PhantomBot fallback forms <c>(1=tag)</c>, <c>(1|default)</c>, and <c>(1&gt;=tag)</c>.
    /// </summary>
    /// <param name="trimmed">The trimmed tag content.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether the tag sits inside an HTTP request tag.</param>
    /// <param name="replacement">The converted <c>{or ...}</c> expression when matched.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertPhantomBotFallbackTag(
        string trimmed,
        MigrationContext context,
        bool insideHttpTag,
        out string replacement)
    {
        if (trimmed.Length >= 3 && trimmed[0] is >= '1' and <= '9')
        {
            int digit = trimmed[0] - '0';
            if (trimmed.StartsWith($"{digit}>=", StringComparison.Ordinal))
            {
                string rhs = trimmed[3..].Trim();
                string convertedRhs = ConvertPhantomBotFallbackRhs(rhs, context, insideHttpTag);
                replacement = $"{{or {{args {digit}}} {convertedRhs}}}";
                return true;
            }

            if (trimmed[1] == '=')
            {
                string rhs = trimmed[2..].Trim();
                string convertedRhs = ConvertPhantomBotFallbackRhs(rhs, context, insideHttpTag);
                replacement = $"{{or {{{digit}}} {convertedRhs}}}";
                return true;
            }

            if (trimmed[1] == '|')
            {
                string fallbackLiteral = trimmed[2..];
                string convertedFallback = ConvertPhantomBot(fallbackLiteral, context, insideHttpTag);
                replacement = $"{{or {{{digit}}} {FormatQuotedString(convertedFallback)}}}";
                return true;
            }
        }

        replacement = string.Empty;
        return false;
    }

    /// <summary>
    /// Converts the right-hand side of a PhantomBot <c>(1=rhs)</c> or <c>(1&gt;=rhs)</c> fallback expression.
    /// </summary>
    /// <param name="rhs">The raw right-hand side text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP request tag.</param>
    /// <returns>The converted AST tag or quoted string fallback.</returns>
    private static string ConvertPhantomBotFallbackRhs(string rhs, MigrationContext context, bool insideHttpTag)
    {
        if (string.IsNullOrEmpty(rhs))
        {
            return "\"\"";
        }

        if (rhs.StartsWith('(') && rhs.EndsWith(')')
            && TryFindBalancedClosingParen(rhs, 0, respectJsStrings: false, out int closeIdx)
            && closeIdx == rhs.Length - 1)
        {
            return ConvertPhantomBot(rhs, context, insideHttpTag);
        }

        if (TryConvertPhantomBotTag(rhs, $"({rhs})", context, insideHttpTag, out string bareTagConverted))
        {
            return bareTagConverted;
        }

        if (!rhs.Contains(' ', StringComparison.Ordinal) && !rhs.Contains('{', StringComparison.Ordinal))
        {
            return $"{{{rhs}}}";
        }

        string transpiled = ConvertPhantomBot(rhs, context, insideHttpTag);
        return FormatAstArgument(transpiled);
    }

    /// <summary>
    /// Handles PhantomBot positional argument tags <c>(1)</c>..<c>(9)</c>, <c>(1!)</c>..<c>(9!)</c>, and <c>(1&gt;)</c>..<c>(9&gt;)</c>.
    /// </summary>
    /// <param name="trimmed">The trimmed tag content.</param>
    /// <param name="rawTag">The original <c>(...)</c> fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="replacement">The converted AST tag when matched.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertPhantomBotPositionalTag(
        string trimmed,
        string rawTag,
        MigrationContext context,
        out string replacement)
    {
        if (trimmed.Length == 1 && trimmed[0] is >= '1' and <= '9')
        {
            replacement = $"{{{trimmed}}}";
            return true;
        }

        if (trimmed.Length == 2 && trimmed[0] is >= '1' and <= '9')
        {
            if (trimmed[1] == '!')
            {
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.StrippedEscapeModifier,
                    $"Stripped legacy strip-escape modifier from {rawTag}; AST arguments are literal by default.",
                    rawTag));
                replacement = $"{{{trimmed[0]}}}";
                return true;
            }

            if (trimmed[1] == '>')
            {
                replacement = $"{{args {trimmed[0]}}}";
                return true;
            }
        }

        replacement = string.Empty;
        return false;
    }

    /// <summary>
    /// Stage 2: Handles prohibited or dangerous PhantomBot tags (<c>unescape</c>, <c>escape</c>, <c>echo!</c>, <c>cpinput*</c>, <c>readfile*</c>, <c>writefile</c>, and <c>token</c>).
    /// </summary>
    /// <param name="name">The lowercase tag name.</param>
    /// <param name="rawArgs">The raw argument text after the tag name.</param>
    /// <param name="rawTag">The full original tag fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP request tag.</param>
    /// <param name="replacement">The safe converted output.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertPhantomBotStage2Tag(
        string name,
        string rawArgs,
        string rawTag,
        MigrationContext context,
        bool insideHttpTag,
        out string replacement)
    {
        switch (name)
        {
            case "unescape":
                bool wrapsExternalOrFile = ContainsDangerousUnescapeTarget(rawArgs);
                context.Diagnostics.Add(new MigrationDiagnostic(
                    wrapsExternalOrFile ? MigrationDiagnosticSeverity.Warning : MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.StrippedUnescapeTag,
                    wrapsExternalOrFile
                        ? "Stripped prohibited (unescape) wrapper around external/file data so returned text remains strictly literal."
                        : "Unwrapped prohibited (unescape) wrapper into native StreamActions AST expression.",
                    rawTag));
                replacement = ConvertPhantomBot(rawArgs, context, insideHttpTag);
                return true;

            case "escape":
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.StrippedEscapeModifier,
                    "Unwrapped legacy (escape) tag; StreamActions AST evaluation prevents tag injection without escaping.",
                    rawTag));
                replacement = ConvertPhantomBot(rawArgs, context, insideHttpTag);
                return true;

            case "echo!":
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.StrippedEscapeModifier,
                    "Stripped legacy strip-escape modifier from (echo!).",
                    rawTag));
                replacement = "{args}";
                return true;

            case "cpinput!":
            case "cpinputraw":
            case "cpinputraw!":
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.StrippedEscapeModifier,
                    $"Stripped legacy escape modifier from ({name}); {{cp_input}} is always literal.",
                    rawTag));
                replacement = "{cp_input}";
                return true;

            case "readfile":
            case "readfileall":
                if (string.IsNullOrWhiteSpace(rawArgs))
                {
                    replacement = string.Empty;
                    return false;
                }

                string readFileName = StripOuterQuotes(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag));
                context.AddVirtualFile(readFileName);
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.ReplacedFileTag,
                    $"Replaced ({name}) with scoped {{kv_get}} against virtual file '{readFileName}'.",
                    rawTag));
                replacement = $"{{kv_get \"files\" \"default\" {FormatQuotedString(readFileName)}}}";
                return true;

            case "readfilerand":
                if (string.IsNullOrWhiteSpace(rawArgs))
                {
                    replacement = string.Empty;
                    return false;
                }

                string randFileName = StripOuterQuotes(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag));
                context.AddVirtualFile(randFileName);
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.ReplacedFileTag,
                    $"Replaced (readfilerand) with scoped {{kv_rand}} against virtual file '{randFileName}'.",
                    rawTag));
                replacement = $"{{kv_rand \"files\" {FormatQuotedString(randFileName)}}}";
                return true;

            case "writefile":
                List<string> writeParts = SplitTopLevelCommaArgs(rawArgs);
                if (writeParts.Count < 3)
                {
                    replacement = string.Empty;
                    return false;
                }

                string writeFileName = StripOuterQuotes(ConvertPhantomBot(writeParts[0].Trim(), context, insideHttpTag));
                string appendRaw = ConvertPhantomBot(writeParts[1].Trim(), context, insideHttpTag);
                string writeTextRaw = string.Join(", ", writeParts.Skip(2)).Trim();
                string writeTextConverted = ConvertPhantomBot(writeTextRaw, context, insideHttpTag);
                bool isAppend = string.Equals(appendRaw, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(appendRaw, "1", StringComparison.Ordinal);

                context.AddVirtualFile(writeFileName);
                string kvTag = isAppend ? "kv_append" : "kv_set";
                context.Diagnostics.Add(new MigrationDiagnostic(
                    MigrationDiagnosticSeverity.Info,
                    MigrationDiagnosticCode.ReplacedFileTag,
                    $"Replaced (writefile) with scoped {{{kvTag}}} against virtual file '{writeFileName}'.",
                    rawTag));
                replacement = $"{{{kvTag} \"files\" \"default\" {FormatQuotedString(writeFileName)} {FormatAstArgument(writeTextConverted)}}}";
                return true;

            case "token":
                if (insideHttpTag)
                {
                    string secretName = context.SecretName;
                    context.EnsureSecretRegistered(secretName);
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Info,
                        MigrationDiagnosticCode.ReplacedTokenTag,
                        $"Replaced (token) inside HTTP tag with $secret.{secretName}.",
                        rawTag));
                    replacement = $"$secret.{secretName}";
                }
                else
                {
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.StrippedStandaloneSecretTag,
                        "Stripped standalone (token) tag outside an HTTP request tag so secrets can never leak in chat.",
                        rawTag));
                    replacement = string.Empty;
                }

                return true;

            default:
                replacement = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Stage 4: Upgrades PhantomBot side-effect tags into <see cref="MigratedActionBlockDescriptor"/> entries and strips them from the inline template.
    /// </summary>
    /// <param name="name">The lowercase tag name.</param>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="rawTag">The full original tag fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="replacement">Empty string when upgraded to an Action Block.</param>
    /// <returns><see langword="true"/> if matched as a side-effect tag; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertPhantomBotSideEffectTag(
        string name,
        string rawArgs,
        string rawTag,
        MigrationContext context,
        out string replacement)
    {
        switch (name)
        {
            case "setrole":
            case "removerole":
                {
                    List<string> args = SplitCommaOrSpaceArgs(rawArgs, context, insideHttpTag: false, isPhantomBot: true);
                    string userArg = args.Count >= 1 ? StripOuterQuotes(args[0]) : "{target}";
                    string roleArg = args.Count >= 2 ? StripOuterQuotes(string.Join(' ', args.Skip(1))) : string.Empty;
                    string op = name == "setrole" ? "discord.set_role" : "discord.remove_role";

                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "platform.invoke",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["operation"] = op,
                            ["user"] = userArg,
                            ["role"] = roleArg,
                        }.AsReadOnly(),
                        RequiredPermissionKey: BuiltInPermissionKeys.UiCommandsActionsDiscordRoles,
                        RequiresBroadcasterReview: false));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        $"Upgraded ({name}) to Action Blocks 'platform.invoke' ({op}) gated by {BuiltInPermissionKeys.UiCommandsActionsDiscordRoles}.",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "setdiscordplaying":
                {
                    string statusText = StripOuterQuotes(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag: false));
                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "platform.invoke",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["operation"] = "discord.set_presence",
                            ["activity"] = statusText,
                        }.AsReadOnly()));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        "Upgraded (setdiscordplaying) to Action Blocks 'platform.invoke' (discord.set_presence).",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "cpsetenabled":
            case "cpsetpaused":
                {
                    List<string> args = SplitCommaOrSpaceArgs(rawArgs, context, insideHttpTag: false, isPhantomBot: true);
                    string rewardId = args.Count >= 1 ? StripOuterQuotes(args[0]) : string.Empty;
                    string boolArg = args.Count >= 2 ? StripOuterQuotes(args[1]) : "true";
                    bool isEnabledOp = name == "cpsetenabled";
                    string op = isEnabledOp ? "twitch.channel_points.set_enabled" : "twitch.channel_points.set_paused";
                    string flagKey = isEnabledOp ? "enabled" : "paused";

                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "platform.invoke",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["operation"] = op,
                            ["rewardId"] = rewardId,
                            [flagKey] = boolArg,
                        }.AsReadOnly()));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        $"Upgraded ({name}) to Action Blocks 'platform.invoke' ({op}).",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "addpointstoall":
            case "takepointsfromall":
                {
                    string amount = StripOuterQuotes(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag: false));
                    string op = name == "addpointstoall" ? "add" : "take";
                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "loyalty.adjust_all",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["operation"] = op,
                            ["amount"] = amount,
                        }.AsReadOnly(),
                        RequiredPermissionKey: BuiltInPermissionKeys.UiLoyaltyManage,
                        RequiresBroadcasterReview: true));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        $"Upgraded ({name}) to Action Blocks 'loyalty.adjust_all' (requires broadcaster review).",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "delaycommand":
                {
                    SplitFirstToken(rawArgs.Trim(), out string secRaw, out string rest);
                    SplitFirstToken(rest, out string cmdNameRaw, out string cmdArgsRaw);
                    string sec = ConvertPhantomBot(secRaw, context, insideHttpTag: false);
                    string cmdName = ConvertPhantomBot(cmdNameRaw, context, insideHttpTag: false);
                    string cmdArgs = ConvertPhantomBot(cmdArgsRaw, context, insideHttpTag: false);

                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "flow.delay",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["seconds"] = sec,
                        }.AsReadOnly()));
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "command.run",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["command"] = cmdName,
                            ["arguments"] = cmdArgs,
                        }.AsReadOnly()));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        "Upgraded (delaycommand) to Action Blocks 'flow.delay' + 'command.run' blocks.",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "delaysay":
                {
                    SplitFirstToken(rawArgs.Trim(), out string secRaw, out string msgRaw);
                    string sec = ConvertPhantomBot(secRaw, context, insideHttpTag: false);
                    string msg = ConvertPhantomBot(msgRaw, context, insideHttpTag: false);

                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "flow.delay",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["seconds"] = sec,
                        }.AsReadOnly()));
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "chat.send",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["message"] = msg,
                        }.AsReadOnly()));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        "Upgraded (delaysay) to Action Blocks 'flow.delay' + 'chat.send' blocks.",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "twitchsay":
                {
                    string msg = ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag: false);
                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "chat.send",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["targetPlatformId"] = "twitch",
                            ["message"] = msg,
                        }.AsReadOnly()));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        "Upgraded (twitchsay) to Action Blocks 'chat.send' block.",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            case "discordsay":
                {
                    SplitFirstToken(rawArgs.Trim(), out string chanRaw, out string msgRaw);
                    string chan = ConvertPhantomBot(chanRaw, context, insideHttpTag: false);
                    string msg = ConvertPhantomBot(msgRaw, context, insideHttpTag: false);
                    context.RequiresActionBlockUpgrade = true;
                    context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
                        ActionType: "chat.send",
                        Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["targetPlatformId"] = "discord",
                            ["targetChannel"] = chan,
                            ["message"] = msg,
                        }.AsReadOnly()));
                    context.Diagnostics.Add(new MigrationDiagnostic(
                        MigrationDiagnosticSeverity.Warning,
                        MigrationDiagnosticCode.UpgradedSideEffectToActionBlocks,
                        "Upgraded (discordsay) to Action Blocks 'chat.send' block.",
                        rawTag));
                    replacement = string.Empty;
                    return true;
                }

            default:
                replacement = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Stage 3: Converts standard PhantomBot module tags into StreamActions <c>{tag}</c> syntax.
    /// </summary>
    /// <param name="name">The lowercase PhantomBot tag name.</param>
    /// <param name="rawArgs">The raw argument text after the tag name.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP request tag.</param>
    /// <param name="replacement">The converted StreamActions tag expression.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> matched a known PhantomBot tag; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertPhantomBotStage3Tag(
        string name,
        string rawArgs,
        MigrationContext context,
        bool insideHttpTag,
        out string replacement)
    {
        switch (name)
        {
            // 3.1 Arguments, Control Flow & String Formatting
            case "echo":
                replacement = "{args}";
                return true;
            case "help":
                string helpMsg = ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag);
                replacement = $"{{if {{1}} \"\" {FormatAstArgument(helpMsg)}}}";
                return true;
            case "cancel":
                replacement = string.IsNullOrWhiteSpace(rawArgs)
                    ? "{cancel}"
                    : $"{{cancel {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))}}}";
                return true;
            case "encodeurlparam":
            case "encodeurl":
                replacement = $"{{url_encode {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))}}}";
                return true;
            case "nl2br":
                replacement = $"{{replace {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))} \"\\n\" \"<br>\"}}";
                return true;
            case "nl2x":
                {
                    SplitCommaOrSpacePair(rawArgs.Trim(), out string replRaw, out string strRaw);
                    string repl = ConvertPhantomBot(replRaw, context, insideHttpTag);
                    string str = ConvertPhantomBot(strRaw, context, insideHttpTag);
                    replacement = $"{{replace {FormatAstArgument(str)} \"\\n\" {FormatAstArgument(repl)}}}";
                    return true;
                }
            case "url0a2nl":
                replacement = $"{{replace {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))} \"%0A\" \"\\n\"}}";
                return true;
            case "code":
                replacement = $"{{random_code {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))}}}";
                return true;
            case "repeat":
                {
                    List<string> args = SplitCommaOrSpaceArgs(rawArgs, context, insideHttpTag, isPhantomBot: true);
                    if (args.Count >= 2)
                    {
                        string countArg = args[0];
                        string textArg = string.Join(' ', args.Skip(1));
                        replacement = $"{{repeat {FormatAstArgument(countArg)} {FormatAstArgument(textArg)}}}";
                    }
                    else
                    {
                        replacement = $"{{repeat {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))}}}";
                    }

                    return true;
                }
            case "nl":
                replacement = "{nl}";
                return true;

            // 3.2 User & Target Identity
            case "sender":
                replacement = "{sender}";
                return true;
            case "@sender":
                replacement = "{@sender}";
                return true;
            case "baresender":
                replacement = "{bare_sender}";
                return true;
            case "touser":
                replacement = "{target}";
                return true;
            case "sanitizeuser":
                replacement = $"{{clean_user {FormatAstArgument(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag))}}}";
                return true;
            case "pointtouser":
                replacement = "{point_to_user}";
                return true;
            case "senderrank":
                replacement = "{sender_rank}";
                return true;
            case "senderrankonly":
                replacement = "{rank}";
                return true;
            case "age":
                replacement = FormatOptionalSingleArgTag("account_age", rawArgs, context, insideHttpTag);
                return true;
            case "random":
            case "randomrank":
                replacement = "{random_chatter}";
                return true;

            // 3.3 Channel, Stream, Follow, Sub & Team Metrics
            case "channelname":
                replacement = FormatOptionalSingleArgTag("channel", rawArgs, context, insideHttpTag);
                return true;
            case "game":
                replacement = FormatOptionalSingleArgTag("game", rawArgs, context, insideHttpTag);
                return true;
            case "status":
                replacement = FormatOptionalSingleArgTag("title", rawArgs, context, insideHttpTag);
                return true;
            case "uptime":
            case "titleinfo":
                replacement = FormatOptionalSingleArgTag("uptime", rawArgs, context, insideHttpTag);
                return true;
            case "downtime":
                replacement = FormatOptionalSingleArgTag("downtime", rawArgs, context, insideHttpTag);
                return true;
            case "playtime":
            case "gameinfo":
                replacement = "{playtime}";
                return true;
            case "gamesplayed":
                replacement = "{games_played}";
                return true;
            case "viewers":
                replacement = FormatOptionalSingleArgTag("viewers", rawArgs, context, insideHttpTag);
                return true;
            case "follows":
                replacement = FormatOptionalSingleArgTag("followers", rawArgs, context, insideHttpTag);
                return true;
            case "subscribers":
                replacement = "{subscribers}";
                return true;
            case "followage":
                replacement = FormatSpaceSeparatedTag("followage", rawArgs, context, insideHttpTag);
                return true;
            case "followdate":
                replacement = FormatSpaceSeparatedTag("followdate", rawArgs, context, insideHttpTag);
                return true;
            case "hours":
            case "hoursround":
                replacement = FormatOptionalSingleArgTag("hours", rawArgs, context, insideHttpTag);
                return true;
            case "lasttip":
                replacement = "{last_tip}";
                return true;
            case "team_name":
            case "team_url":
            case "team_members":
            case "team_random_member":
            case "team_member_game":
            case "team_member_followers":
            case "team_member_url":
                replacement = FormatCommaOrSpaceTag(name, rawArgs, context, insideHttpTag);
                return true;

            // 3.4 Loyalty Economy, Counters & Command Metadata
            case "points":
                replacement = FormatOptionalSingleArgTag("points", rawArgs, context, insideHttpTag);
                return true;
            case "pointname":
                replacement = FormatOptionalSingleArgTag("pointname", rawArgs, context, insideHttpTag);
                return true;
            case "price":
                replacement = FormatOptionalSingleArgTag("price", rawArgs, context, insideHttpTag);
                return true;
            case "pay":
                replacement = FormatOptionalSingleArgTag("pay", rawArgs, context, insideHttpTag);
                return true;
            case "addpoints":
                replacement = FormatCommaOrSpaceTag("points_add", rawArgs, context, insideHttpTag);
                return true;
            case "takepoints":
                replacement = FormatCommaOrSpaceTag("points_take", rawArgs, context, insideHttpTag);
                return true;
            case "takepointsorcancel":
                replacement = FormatCommaOrSpaceTag("points_take_or_cancel", rawArgs, context, insideHttpTag);
                return true;
            case "transferpoints":
                replacement = FormatCommaOrSpaceTag("points_transfer", rawArgs, context, insideHttpTag);
                return true;
            case "count":
                {
                    string trimmedArgs = rawArgs.Trim();
                    if (string.IsNullOrEmpty(trimmedArgs))
                    {
                        replacement = "{count}";
                        return true;
                    }

                    SplitFirstToken(trimmedArgs, out string first, out string rest);
                    if (string.Equals(first, "reset", StringComparison.OrdinalIgnoreCase))
                    {
                        replacement = string.IsNullOrWhiteSpace(rest)
                            ? "{count_reset}"
                            : $"{{count_reset {FormatAstArgument(ConvertPhantomBot(rest, context, insideHttpTag))}}}";
                        return true;
                    }

                    replacement = FormatSpaceSeparatedTag("count", trimmedArgs, context, insideHttpTag);
                    return true;
                }
            case "keywordcount":
                replacement = FormatOptionalSingleArgTag("count", rawArgs, context, insideHttpTag);
                return true;
            case "commandslist":
                replacement = FormatOptionalSingleArgTag("commands_list", rawArgs, context, insideHttpTag);
                return true;

            // 3.5 Twitch Channel Points & Power-Up Context
            case "cptitle":
                replacement = "{cp_title}";
                return true;
            case "cpprompt":
                replacement = "{cp_prompt}";
                return true;
            case "cpcost":
                replacement = "{cp_cost}";
                return true;
            case "cpinput":
                replacement = "{cp_input}";
                return true;
            case "cpdisplayname":
                replacement = "{cp_user}";
                return true;
            case "cpusername":
                replacement = "{cp_username}";
                return true;
            case "cpuserid":
                replacement = "{cp_user_id}";
                return true;
            case "cpredemptionid":
                replacement = "{cp_redemption_id}";
                return true;
            case "cpredeemableid":
                replacement = "{cp_reward_id}";
                return true;
            case "cpfulfillstatus":
                replacement = "{cp_status}";
                return true;
            case "cpfulfill":
                replacement = "{cp_fulfill}";
                return true;
            case "cpcancel":
                replacement = "{cp_cancel}";
                return true;

            // 3.6 External HTTP APIs Consolidated into {api}
            case "customapi":
                {
                    string convertedUrl = ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag: true);
                    RecordSecretHostIfReferenced(convertedUrl, context);
                    replacement = $"{{api {FormatUrlArgument(convertedUrl)}}}";
                    return true;
                }
            case "customapijson":
                {
                    SplitFirstToken(rawArgs.Trim(), out string urlRaw, out string specsRaw);
                    string convertedUrl = ConvertPhantomBot(urlRaw, context, insideHttpTag: true);
                    RecordSecretHostIfReferenced(convertedUrl, context);
                    List<string> args = [FormatUrlArgument(convertedUrl)];
                    if (!string.IsNullOrWhiteSpace(specsRaw))
                    {
                        foreach (string specToken in SplitTopLevelWhitespaceTokens(specsRaw))
                        {
                            string convertedSpec = ConvertPhantomBot(specToken, context, insideHttpTag: true);
                            args.Add(convertedSpec.StartsWith("json=", StringComparison.OrdinalIgnoreCase)
                                ? convertedSpec
                                : $"json={FormatQuotedString(StripOuterQuotes(convertedSpec))}");
                        }
                    }

                    replacement = $"{{api {string.Join(' ', args)}}}";
                    return true;
                }
            case "customapipost":
                {
                    SplitFirstToken(rawArgs.Trim(), out string urlRaw, out string bodyRaw);
                    string convertedUrl = ConvertPhantomBot(urlRaw, context, insideHttpTag: true);
                    RecordSecretHostIfReferenced(convertedUrl, context);
                    if (string.IsNullOrWhiteSpace(bodyRaw))
                    {
                        replacement = $"{{api POST {FormatUrlArgument(convertedUrl)}}}";
                    }
                    else
                    {
                        string escapedBody = StripOuterQuotes(bodyRaw)
                            .Replace("{", "\\{", StringComparison.Ordinal)
                            .Replace("}", "\\}", StringComparison.Ordinal);
                        string convertedBody = ConvertPhantomBot(escapedBody, context, insideHttpTag: true);
                        replacement = $"{{api POST {FormatUrlArgument(convertedUrl)} body={FormatQuotedString(convertedBody)}}}";
                    }

                    return true;
                }
            case "api":
                {
                    List<string> tokens = SplitTopLevelWhitespaceTokens(rawArgs.Trim());
                    List<string> convertedTokens = new(tokens.Count);
                    foreach (string token in tokens)
                    {
                        string convertedToken = ConvertPhantomBot(token, context, insideHttpTag: true);
                        if (LooksLikeHttpUrl(convertedToken))
                        {
                            RecordSecretHostIfReferenced(convertedToken, context);
                            convertedTokens.Add(FormatUrlArgument(convertedToken));
                        }
                        else
                        {
                            convertedTokens.Add(FormatAstArgument(convertedToken));
                        }
                    }

                    replacement = convertedTokens.Count == 0
                        ? "{api}"
                        : $"{{api {string.Join(' ', convertedTokens)}}}";
                    return true;
                }

            // 3.7 Math & Date/Time
            case "add":
                replacement = FormatMathTag("add", rawArgs, context, insideHttpTag);
                return true;
            case "subtract":
                replacement = FormatMathTag("sub", rawArgs, context, insideHttpTag);
                return true;
            case "multiply":
                replacement = FormatMathTag("mul", rawArgs, context, insideHttpTag);
                return true;
            case "divide":
                replacement = FormatMathTag("div", rawArgs, context, insideHttpTag);
                return true;
            case "remainder":
                replacement = FormatMathTag("mod", rawArgs, context, insideHttpTag);
                return true;
            case "exponentiation":
                replacement = FormatMathTag("pow", rawArgs, context, insideHttpTag);
                return true;
            case "abs":
            case "ceil":
            case "floor":
            case "round":
            case "min":
            case "max":
                replacement = FormatMathTag(name, rawArgs, context, insideHttpTag);
                return true;
            case "currenttime":
                {
                    List<string> timeArgs = SplitTopLevelCommaArgs(rawArgs);
                    List<string> formatted = [];
                    foreach (string arg in timeArgs)
                    {
                        string converted = StripOuterQuotes(ConvertPhantomBot(arg.Trim(), context, insideHttpTag));
                        if (!string.IsNullOrEmpty(converted))
                        {
                            formatted.Add(FormatAstArgument(converted));
                        }
                    }

                    replacement = formatted.Count == 0
                        ? "{time}"
                        : $"{{time {string.Join(' ', formatted)}}}";
                    return true;
                }
            case "countdown":
                replacement = $"{{countdown {FormatAstArgument(StripOuterQuotes(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag)))}}}";
                return true;
            case "countup":
            case "gettimevar":
                replacement = string.IsNullOrWhiteSpace(rawArgs)
                    ? "{countup}"
                    : $"{{countup {FormatAstArgument(StripOuterQuotes(ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag)))}}}";
                return true;

            // 3.8 Command Chaining & Overlays
            case "command":
                replacement = FormatSpaceSeparatedTag("botcmd", rawArgs, context, insideHttpTag);
                return true;
            case "alert":
                replacement = FormatCommaOrSpaceTag("alert", rawArgs, context, insideHttpTag);
                return true;
            case "alerttext":
                replacement = FormatSpaceSeparatedTag("alerttext", rawArgs, context, insideHttpTag);
                return true;
            case "playsound":
                replacement = FormatSpaceSeparatedTag("playsound", rawArgs, context, insideHttpTag);
                return true;

            default:
                replacement = string.Empty;
                return false;
        }
    }

    #endregion

    #region Nightbot $(...) Transpiler

    /// <summary>
    /// Transpiles Nightbot <c>$(tag)</c> expressions into StreamActions <c>{tag}</c> syntax, including Stage 2 <c>$(eval)</c> handling.
    /// </summary>
    /// <param name="input">The Nightbot template fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideUrl">Whether inside a <c>$(urlfetch ...)</c> URL where bare <c>$(querystring)</c> maps to <c>{url_encode {args}}</c>.</param>
    /// <returns>The converted template fragment.</returns>
    private static string ConvertNightbot(string input, MigrationContext context, bool insideUrl)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains("$(", StringComparison.Ordinal))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (index + 1 < input.Length
                && input[index] == '$'
                && input[index + 1] == '('
                && TryFindBalancedClosingParen(input, index + 1, respectJsStrings: true, out int closeIndex))
            {
                string rawTag = input[index..(closeIndex + 1)];
                string inner = input[(index + 2)..closeIndex];
                if (TryConvertNightbotTag(inner, rawTag, context, insideUrl, out string replacement))
                {
                    _ = output.Append(replacement);
                    index = closeIndex + 1;
                    continue;
                }
            }

            _ = output.Append(input[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Attempts to convert a single Nightbot <c>$(inner)</c> expression.
    /// </summary>
    /// <param name="inner">The text inside <c>$(...)</c>.</param>
    /// <param name="rawTag">The full original <c>$(...)</c> fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideUrl">Whether inside a URL context.</param>
    /// <param name="replacement">The converted output.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions tag names are lowercase ASCII identifiers.")]
    private static bool TryConvertNightbotTag(
        string inner,
        string rawTag,
        MigrationContext context,
        bool insideUrl,
        out string replacement)
    {
        string trimmed = inner.Trim();
        if (trimmed.Length == 1 && trimmed[0] is >= '1' and <= '9')
        {
            replacement = $"{{{trimmed}}}";
            return true;
        }

        SplitFirstToken(trimmed, out string rawName, out string rawArgs);
        string name = rawName.ToLowerInvariant();

        switch (name)
        {
            case "user":
                replacement = "{sender}";
                return true;
            case "userid":
                replacement = "{sender_id}";
                return true;
            case "touser":
                replacement = "{target}";
                return true;
            case "channel":
                replacement = "{channel}";
                return true;
            case "querystring":
                if (string.IsNullOrWhiteSpace(rawArgs))
                {
                    replacement = insideUrl ? "{url_encode {args}}" : "{args}";
                }
                else
                {
                    string convertedArg = ConvertNightbot(rawArgs.Trim(), context, insideUrl: false);
                    replacement = $"{{url_encode {FormatAstArgument(convertedArg)}}}";
                }

                return true;
            case "urlfetch":
                {
                    string urlPart = rawArgs.Trim();
                    if (urlPart.StartsWith("json ", StringComparison.OrdinalIgnoreCase))
                    {
                        urlPart = urlPart[5..].Trim();
                    }

                    string convertedUrl = ConvertNightbot(urlPart, context, insideUrl: true);
                    replacement = $"{{api {FormatUrlArgument(convertedUrl)}}}";
                    return true;
                }
            case "twitch":
                replacement = ConvertNightbotTwitchTag(rawArgs.Trim(), context);
                return true;
            case "time":
                {
                    List<string> args = SplitShellStyleArgs(rawArgs.Trim());
                    List<string> formatted = [];
                    foreach (string arg in args)
                    {
                        string converted = ConvertNightbot(StripOuterQuotes(arg), context, insideUrl: false);
                        formatted.Add(FormatAstArgument(converted));
                    }

                    replacement = formatted.Count == 0
                        ? "{time}"
                        : $"{{time {string.Join(' ', formatted)}}}";
                    return true;
                }
            case "countdown":
                replacement = $"{{countdown {FormatAstArgument(StripOuterQuotes(ConvertNightbot(rawArgs.Trim(), context, insideUrl: false)))}}}";
                return true;
            case "countup":
                replacement = $"{{countup {FormatAstArgument(StripOuterQuotes(ConvertNightbot(rawArgs.Trim(), context, insideUrl: false)))}}}";
                return true;
            case "count":
                replacement = "{count}";
                return true;
            case "commands":
                replacement = "{commands_list}";
                return true;
            case "eval":
                replacement = HandleNightbotEval(rawArgs, rawTag, context);
                return true;
            default:
                replacement = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Converts Nightbot <c>$(twitch &lt;ch&gt; "{{metric}}")</c> syntax into StreamActions tags.
    /// </summary>
    /// <param name="rawArgs">The raw arguments inside <c>$(twitch ...)</c>.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The converted StreamActions tag or interpolated text.</returns>
    private static string ConvertNightbotTwitchTag(string rawArgs, MigrationContext context)
    {
        List<string> args = SplitShellStyleArgs(rawArgs);
        if (args.Count == 0)
        {
            return string.Empty;
        }

        string channelArg = args.Count >= 2 ? ConvertNightbot(StripOuterQuotes(args[0]), context, insideUrl: false) : "{channel}";
        string formatStr = args.Count >= 2 ? StripOuterQuotes(args[1]) : StripOuterQuotes(args[0]);
        bool isDefaultChannel = string.IsNullOrWhiteSpace(channelArg)
            || string.Equals(channelArg, "{channel}", StringComparison.OrdinalIgnoreCase);

        string channelSuffix = isDefaultChannel ? string.Empty : $" {FormatAstArgument(channelArg)}";

        string result = NightbotTwitchMetricRegex().Replace(formatStr, match =>
        {
            string metric = match.Groups["metric"].Value;
            return metric.ToUpperInvariant() switch
            {
                "GAME" => $"{{game{channelSuffix}}}",
                "TITLE" => $"{{title{channelSuffix}}}",
                "UPTIMELENGTH" => $"{{uptime{channelSuffix}}}",
                "VIEWERS" => $"{{viewers{channelSuffix}}}",
                "FOLLOWERS" => $"{{followers{channelSuffix}}}",
                "SUBSCRIBERCOUNT" => "{subscribers}",
                "FOLLOWAGE" => isDefaultChannel ? "{followage}" : $"{{followage {FormatAstArgument(channelArg)}}}",
                "FOLLOWEDAT" => isDefaultChannel ? "{followdate}" : $"{{followdate {FormatAstArgument(channelArg)}}}",
                "ACCOUNTAGE" => isDefaultChannel ? "{account_age}" : $"{{account_age {FormatAstArgument(channelArg)}}}",
                _ => match.Value,
            };
        });

        return ConvertNightbot(result, context, insideUrl: false);
    }

    /// <summary>
    /// Stage 2: Transpiles safe Nightbot <c>$(eval ...)</c> idioms into Tier 1 AST tags, or promotes complex scripts to ClearScript V8 action blocks.
    /// </summary>
    /// <param name="jsBody">The raw JavaScript body inside <c>$(eval ...)</c>.</param>
    /// <param name="rawTag">The full <c>$(eval ...)</c> fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The transpiled Tier 1 AST tag, or empty string when promoted to <c>script.clearscript</c>.</returns>
    private static string HandleNightbotEval(string jsBody, string rawTag, MigrationContext context)
    {
        string trimmedJs = jsBody.Trim();

        // 1. Random array pick idioms
        if (TryTranspileEvalRandomArrayPick(trimmedJs, context, out string randomChoiceTag))
        {
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.TranspiledEvalExpression,
                "Transpiled safe Nightbot $(eval) random array pick into {random_choice}.",
                rawTag));
            return randomChoiceTag;
        }

        // 2. Simple string replace idiom
        if (TryTranspileEvalStringReplace(trimmedJs, context, out string replaceTag))
        {
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.TranspiledEvalExpression,
                "Transpiled safe Nightbot $(eval) string replace into {replace}.",
                rawTag));
            return replaceTag;
        }

        // 3. Simple fallback idiom (a || 'default')
        if (TryTranspileEvalFallback(trimmedJs, context, out string orTag))
        {
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.TranspiledEvalExpression,
                "Transpiled safe Nightbot $(eval) fallback expression into {or}.",
                rawTag));
            return orTag;
        }

        // 4. Simple arithmetic idiom
        if (TryTranspileArithmeticExpression(trimmedJs, LegacyBotSource.Nightbot, context, out string mathTag))
        {
            context.Diagnostics.Add(new MigrationDiagnostic(
                MigrationDiagnosticSeverity.Info,
                MigrationDiagnosticCode.TranspiledEvalExpression,
                "Transpiled safe Nightbot $(eval) arithmetic expression into {math}.",
                rawTag));
            return mathTag;
        }

        // Complex $(eval) scripts: promote to Tier 3 ClearScript ActionBlock
        context.RequiresActionBlockUpgrade = true;
        context.UpgradedActions.Add(new MigratedActionBlockDescriptor(
            ActionType: "script.clearscript",
            Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["script"] = trimmedJs,
                ["source"] = "nightbot.eval",
            }.AsReadOnly(),
            RequiredPermissionKey: BuiltInPermissionKeys.UiCommandsScriptEdit,
            RequiresBroadcasterReview: true));
        context.Diagnostics.Add(new MigrationDiagnostic(
            MigrationDiagnosticSeverity.Warning,
            MigrationDiagnosticCode.PromotedEvalToClearScript,
            "Promoted complex Nightbot $(eval) script to a ClearScript V8 action block requiring broadcaster review.",
            rawTag));

        return string.Empty;
    }

    /// <summary>
    /// Attempts to transpile a Nightbot <c>$(eval)</c> random array pick into <c>{random_choice ...}</c>.
    /// </summary>
    /// <param name="js">The trimmed JavaScript expression.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the <c>{random_choice ...}</c> tag.</param>
    /// <returns><see langword="true"/> if matched and safely parsed; otherwise, <see langword="false"/>.</returns>
    private static bool TryTranspileEvalRandomArrayPick(string js, MigrationContext context, out string result)
    {
        Match match = InlineArrayRandomPickRegex().Match(js);
        if (!match.Success)
        {
            match = VariableArrayRandomPickRegex().Match(js);
        }

        if (!match.Success)
        {
            result = string.Empty;
            return false;
        }

        string itemsBody = match.Groups["items"].Value;
        List<string>? parsedItems = TryParseJsStringOrNumericArrayItems(itemsBody);
        if (parsedItems is null || parsedItems.Count == 0)
        {
            result = string.Empty;
            return false;
        }

        List<string> formattedItems = new(parsedItems.Count);
        foreach (string item in parsedItems)
        {
            string converted = ConvertNightbot(item, context, insideUrl: false);
            formattedItems.Add(FormatQuotedString(converted));
        }

        result = $"{{random_choice {string.Join(' ', formattedItems)}}}";
        return true;
    }

    /// <summary>
    /// Attempts to transpile a Nightbot <c>$(eval)</c> <c>.replace('a','b')</c> expression into <c>{replace ...}</c>.
    /// </summary>
    /// <param name="js">The trimmed JavaScript expression.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the <c>{replace ...}</c> tag.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryTranspileEvalStringReplace(string js, MigrationContext context, out string result)
    {
        Match match = SimpleStringReplaceEvalRegex().Match(js);
        if (!match.Success)
        {
            result = string.Empty;
            return false;
        }

        string targetRaw = match.Groups["target"].Value.Trim();
        if (!TryConvertSimpleEvalOperand(targetRaw, context, out string targetConverted))
        {
            result = string.Empty;
            return false;
        }

        string find = UnescapeJsStringLiteral(match.Groups["find"].Value);
        string repl = UnescapeJsStringLiteral(match.Groups["repl"].Value);
        find = ConvertNightbot(find, context, insideUrl: false);
        repl = ConvertNightbot(repl, context, insideUrl: false);

        result = $"{{replace {FormatAstArgument(targetConverted)} {FormatQuotedString(find)} {FormatQuotedString(repl)}}}";
        return true;
    }

    /// <summary>
    /// Attempts to transpile a Nightbot <c>$(eval)</c> logical-OR fallback (<c>a || 'default'</c>) into <c>{or ...}</c>.
    /// </summary>
    /// <param name="js">The trimmed JavaScript expression.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the <c>{or ...}</c> tag.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryTranspileEvalFallback(string js, MigrationContext context, out string result)
    {
        string trimmed = js.TrimEnd(';').Trim();
        List<string>? parts = SplitTopLevelLogicalOr(trimmed);
        if (parts is null || parts.Count < 2)
        {
            result = string.Empty;
            return false;
        }

        List<string> convertedParts = new(parts.Count);
        foreach (string part in parts)
        {
            if (!TryConvertSimpleEvalOperand(part.Trim(), context, out string converted))
            {
                result = string.Empty;
                return false;
            }

            convertedParts.Add(IsSimpleTagExpression(converted)
                ? converted
                : FormatQuotedString(converted));
        }

        result = $"{{or {string.Join(' ', convertedParts)}}}";
        return true;
    }

    /// <summary>
    /// Converts a simple <c>$(eval)</c> operand (string literal, numeric literal, <c>decodeURIComponent(...)</c>, or Nightbot tag) when safe.
    /// </summary>
    /// <param name="operand">The raw JS operand text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="converted">The converted AST tag or literal string.</param>
    /// <returns><see langword="true"/> if safe and recognized; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertSimpleEvalOperand(string operand, MigrationContext context, out string converted)
    {
        string trimmed = operand.Trim();
        if (trimmed.StartsWith("decodeURIComponent(", StringComparison.Ordinal) && trimmed.EndsWith(')'))
        {
            trimmed = trimmed["decodeURIComponent(".Length..^1].Trim();
        }

        if (IsSingleJsStringLiteral(trimmed))
        {
            string unescaped = UnescapeJsStringLiteral(trimmed);
            converted = ConvertNightbot(unescaped, context, insideUrl: false);
            return true;
        }

        if (trimmed.StartsWith("$(", StringComparison.Ordinal)
            && trimmed.EndsWith(')')
            && TryFindBalancedClosingParen(trimmed, 1, respectJsStrings: true, out int closeIdx)
            && closeIdx == trimmed.Length - 1)
        {
            converted = ConvertNightbot(trimmed, context, insideUrl: false);
            return !converted.Contains("$(", StringComparison.Ordinal);
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            converted = trimmed;
            return true;
        }

        converted = string.Empty;
        return false;
    }

    #endregion

    #region StreamElements ${...} Transpiler

    /// <summary>
    /// Transpiles StreamElements <c>${tag}</c> expressions into StreamActions <c>{tag}</c> syntax.
    /// </summary>
    /// <param name="input">The StreamElements template fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The converted template fragment.</returns>
    private static string ConvertStreamElements(string input, MigrationContext context)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains("${", StringComparison.Ordinal))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (index + 1 < input.Length
                && input[index] == '$'
                && input[index + 1] == '{'
                && TryFindBalancedClosingBrace(input, index + 1, out int closeIndex))
            {
                string inner = input[(index + 2)..closeIndex];
                if (TryConvertStreamElementsTag(inner, context, out string replacement))
                {
                    _ = output.Append(replacement);
                    index = closeIndex + 1;
                    continue;
                }
            }

            _ = output.Append(input[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Attempts to convert a single StreamElements <c>${inner}</c> expression.
    /// </summary>
    /// <param name="inner">The text inside <c>${...}</c>.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="replacement">The converted StreamActions expression.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions tag names are lowercase ASCII identifiers.")]
    private static bool TryConvertStreamElementsTag(string inner, MigrationContext context, out string replacement)
    {
        string trimmed = inner.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            replacement = string.Empty;
            return false;
        }

        // Positional: ${1}..${9}, ${1:}..${9:}, ${1|'default'}..${9|default}
        if (trimmed[0] is >= '1' and <= '9')
        {
            int digit = trimmed[0] - '0';
            if (trimmed.Length == 1)
            {
                replacement = $"{{{digit}}}";
                return true;
            }

            if (trimmed.Length == 2 && trimmed[1] == ':')
            {
                replacement = $"{{args {digit}}}";
                return true;
            }

            if (trimmed[1] == '|')
            {
                string fallback = StripOuterSingleOrDoubleQuotes(trimmed[2..].Trim());
                string convertedFallback = ConvertStreamElements(fallback, context);
                replacement = $"{{or {{{digit}}} {FormatQuotedString(convertedFallback)}}}";
                return true;
            }
        }

        // Dot-prefixed namespaces: customapi., random., time., user.
        if (trimmed.StartsWith("customapi.", StringComparison.OrdinalIgnoreCase))
        {
            string spec = trimmed["customapi.".Length..].Trim();
            replacement = ConvertStreamElementsCustomApi(spec, context);
            return true;
        }

        if (trimmed.StartsWith("random.", StringComparison.OrdinalIgnoreCase))
        {
            string randomSpec = trimmed["random.".Length..].Trim();
            if (string.Equals(randomSpec, "chatter", StringComparison.OrdinalIgnoreCase))
            {
                replacement = "{random_chatter}";
                return true;
            }

            if (randomSpec.StartsWith("pick", StringComparison.OrdinalIgnoreCase))
            {
                string pickArgs = randomSpec.Length > 4 ? randomSpec[4..].Trim() : string.Empty;
                List<string> choices = SplitShellStyleArgs(pickArgs);
                if (choices.Count > 0)
                {
                    List<string> formattedChoices = [.. choices.Select(c => FormatQuotedString(ConvertStreamElements(StripOuterSingleOrDoubleQuotes(c), context)))];
                    replacement = $"{{random_choice {string.Join(' ', formattedChoices)}}}";
                    return true;
                }
            }

            int dashIdx = randomSpec.IndexOf('-', StringComparison.Ordinal);
            if (dashIdx > 0
                && int.TryParse(randomSpec[..dashIdx].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int min)
                && int.TryParse(randomSpec[(dashIdx + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int max))
            {
                replacement = $"{{random_int {min.ToString(CultureInfo.InvariantCulture)} {max.ToString(CultureInfo.InvariantCulture)}}}";
                return true;
            }
        }

        if (trimmed.StartsWith("time.", StringComparison.OrdinalIgnoreCase))
        {
            string tz = trimmed["time.".Length..].Trim();
            string convertedTz = ConvertStreamElements(StripOuterSingleOrDoubleQuotes(tz), context);
            replacement = string.IsNullOrEmpty(convertedTz) ? "{time}" : $"{{time {FormatAstArgument(convertedTz)}}}";
            return true;
        }

        if (string.Equals(trimmed, "user.name", StringComparison.OrdinalIgnoreCase))
        {
            replacement = "{bare_sender}";
            return true;
        }

        if (string.Equals(trimmed, "user.id", StringComparison.OrdinalIgnoreCase))
        {
            replacement = "{sender_id}";
            return true;
        }

        if (string.Equals(trimmed, "urlfetch.encode", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("urlfetch.encode ", StringComparison.OrdinalIgnoreCase))
        {
            string rawArg = trimmed.Length > "urlfetch.encode".Length ? trimmed["urlfetch.encode".Length..].Trim() : string.Empty;
            string convertedArg = ConvertStreamElements(StripOuterSingleOrDoubleQuotes(rawArg), context);
            replacement = $"{{url_encode {FormatAstArgument(convertedArg)}}}";
            return true;
        }

        SplitFirstToken(trimmed, out string rawName, out string rawArgs);
        string name = rawName.ToLowerInvariant();

        switch (name)
        {
            case "user":
            case "sender":
                replacement = "{sender}";
                return true;
            case "touser":
                replacement = "{target}";
                return true;
            case "channel":
                replacement = "{channel}";
                return true;
            case "args":
                replacement = "{args}";
                return true;
            case "toupper":
                replacement = $"{{upper {FormatAstArgument(ConvertStreamElements(StripOuterSingleOrDoubleQuotes(rawArgs), context))}}}";
                return true;
            case "tolower":
                replacement = $"{{lower {FormatAstArgument(ConvertStreamElements(StripOuterSingleOrDoubleQuotes(rawArgs), context))}}}";
                return true;
            case "repeat":
                {
                    SplitFirstToken(rawArgs.Trim(), out string countRaw, out string textRaw);
                    string count = ConvertStreamElements(countRaw, context);
                    string text = ConvertStreamElements(StripOuterSingleOrDoubleQuotes(textRaw.Trim()), context);
                    replacement = $"{{repeat {FormatAstArgument(count)} {FormatAstArgument(text)}}}";
                    return true;
                }
            case "urlfetch":
                {
                    string convertedUrl = ConvertStreamElements(StripOuterSingleOrDoubleQuotes(rawArgs.Trim()), context);
                    replacement = $"{{api {FormatUrlArgument(convertedUrl)}}}";
                    return true;
                }
            case "math":
                if (TryTranspileArithmeticExpression(rawArgs, LegacyBotSource.StreamElements, context, out string mathTag))
                {
                    replacement = mathTag;
                    return true;
                }

                replacement = string.Empty;
                return false;
            case "countdown":
                replacement = $"{{countdown {FormatAstArgument(StripOuterSingleOrDoubleQuotes(ConvertStreamElements(rawArgs.Trim(), context)))}}}";
                return true;
            case "countup":
                replacement = $"{{countup {FormatAstArgument(StripOuterSingleOrDoubleQuotes(ConvertStreamElements(rawArgs.Trim(), context)))}}}";
                return true;
            case "count":
                replacement = string.IsNullOrWhiteSpace(rawArgs)
                    ? "{count}"
                    : $"{{count {FormatAstArgument(StripOuterSingleOrDoubleQuotes(ConvertStreamElements(rawArgs.Trim(), context)))}}}";
                return true;
            case "getcount":
                replacement = $"{{counter_get {FormatQuotedString(StripOuterSingleOrDoubleQuotes(ConvertStreamElements(rawArgs.Trim(), context)))}}}";
                return true;
            case "points":
                replacement = FormatStreamElementsOptionalArgTag("points", rawArgs, context);
                return true;
            case "pointsname":
                replacement = "{pointname}";
                return true;
            case "cost":
                replacement = "{price}";
                return true;
            case "game":
                replacement = FormatStreamElementsOptionalArgTag("game", rawArgs, context);
                return true;
            case "title":
                replacement = FormatStreamElementsOptionalArgTag("title", rawArgs, context);
                return true;
            case "uptime":
                replacement = FormatStreamElementsOptionalArgTag("uptime", rawArgs, context);
                return true;
            case "viewers":
                replacement = FormatStreamElementsOptionalArgTag("viewers", rawArgs, context);
                return true;
            case "followers":
                replacement = FormatStreamElementsOptionalArgTag("followers", rawArgs, context);
                return true;
            case "subscribers":
                replacement = "{subscribers}";
                return true;
            case "followage":
                {
                    List<string> args = SplitShellStyleArgs(rawArgs.Trim());
                    List<string> converted = [.. args.Select(a => FormatAstArgument(ConvertStreamElements(StripOuterSingleOrDoubleQuotes(a), context)))];
                    replacement = converted.Count == 0 ? "{followage}" : $"{{followage {string.Join(' ', converted)}}}";
                    return true;
                }
            case "time_online":
                replacement = FormatStreamElementsOptionalArgTag("hours", rawArgs, context);
                return true;
            case "level":
                replacement = FormatStreamElementsOptionalArgTag("rank", rawArgs, context);
                return true;
            case "accountage":
                replacement = FormatStreamElementsOptionalArgTag("account_age", rawArgs, context);
                return true;
            case "lasttip":
                replacement = "{last_tip}";
                return true;
            default:
                replacement = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Converts StreamElements <c>${customapi.&lt;url&gt;[.&lt;path&gt;]}</c> expressions into <c>{api ...}</c>.
    /// </summary>
    /// <param name="spec">The text following <c>customapi.</c>.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The converted <c>{api ...}</c> tag.</returns>
    private static string ConvertStreamElementsCustomApi(string spec, MigrationContext context)
    {
        string converted = ConvertStreamElements(spec, context);
        int spaceIndex = FindTopLevelWhitespace(converted);
        if (spaceIndex > 0)
        {
            string urlPart = converted[..spaceIndex].Trim();
            string pathPart = converted[(spaceIndex + 1)..].Trim();
            return string.IsNullOrEmpty(pathPart)
                ? $"{{api {FormatUrlArgument(urlPart)}}}"
                : $"{{api {FormatUrlArgument(urlPart)} json={FormatQuotedString(pathPart)}}}";
        }

        return TrySplitStreamElementsCustomApiPath(converted, out string splitUrl, out string jsonPath)
            ? $"{{api {FormatUrlArgument(splitUrl)} json={FormatQuotedString(jsonPath)}}}"
            : $"{{api {FormatUrlArgument(converted)}}}";
    }

    /// <summary>
    /// Splits <c>https://host/endpoint.jsonPath</c> into URL and JSON path when a trailing dot-delimited JSON path is present after the URL path.
    /// </summary>
    /// <param name="raw">The converted <c>customapi.</c> body.</param>
    /// <param name="url">The extracted URL.</param>
    /// <param name="jsonPath">The extracted JSON path.</param>
    /// <returns><see langword="true"/> if a trailing JSON path was identified; otherwise, <see langword="false"/>.</returns>
    private static bool TrySplitStreamElementsCustomApiPath(string raw, out string url, out string jsonPath)
    {
        url = raw;
        jsonPath = string.Empty;

        int schemeIndex = raw.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex < 0)
        {
            return false;
        }

        int pathStart = raw.IndexOf('/', schemeIndex + 3);
        if (pathStart < 0 || raw.IndexOf('?', pathStart) >= 0)
        {
            return false;
        }

        int lastSlash = raw.LastIndexOf('/');
        string lastSegment = raw[(lastSlash + 1)..];
        int firstDotInLastSegment = lastSegment.IndexOf('.', StringComparison.Ordinal);
        if (firstDotInLastSegment <= 0 || firstDotInLastSegment == lastSegment.Length - 1)
        {
            return false;
        }

        string afterFirstDot = lastSegment[(firstDotInLastSegment + 1)..];
        if (IsKnownWebFileExtension(afterFirstDot))
        {
            return false;
        }

        // Preserve e.g. /data.json before .user.name
        int secondDot = afterFirstDot.IndexOf('.', StringComparison.Ordinal);
        if (secondDot > 0 && IsKnownWebFileExtension(afterFirstDot[..secondDot]))
        {
            int splitPoint = lastSlash + 1 + firstDotInLastSegment + 1 + secondDot;
            url = raw[..splitPoint];
            jsonPath = raw[(splitPoint + 1)..];
            return !string.IsNullOrEmpty(jsonPath);
        }

        int dotSplit = lastSlash + 1 + firstDotInLastSegment;
        url = raw[..dotSplit];
        jsonPath = raw[(dotSplit + 1)..];
        return !string.IsNullOrEmpty(jsonPath);
    }

    /// <summary>
    /// Checks whether <paramref name="extension"/> is a common URL path file extension rather than a JSON property path.
    /// </summary>
    /// <param name="extension">The candidate extension text without leading dot.</param>
    /// <returns><see langword="true"/> if recognized as a URL file extension; otherwise, <see langword="false"/>.</returns>
    private static bool IsKnownWebFileExtension(string extension) =>
        extension.Equals("json", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("txt", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("php", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("html", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("htm", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("xml", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Formats a StreamElements tag with an optional single argument.
    /// </summary>
    /// <param name="targetTag">The StreamActions tag name.</param>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The formatted <c>{tag}</c> or <c>{tag arg}</c> string.</returns>
    private static string FormatStreamElementsOptionalArgTag(string targetTag, string rawArgs, MigrationContext context)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            return $"{{{targetTag}}}";
        }

        string converted = ConvertStreamElements(StripOuterSingleOrDoubleQuotes(rawArgs.Trim()), context);
        return $"{{{targetTag} {FormatAstArgument(converted)}}}";
    }

    #endregion

    #region Streamlabs $tag / $tag(...) Transpiler

    /// <summary>
    /// Transpiles Streamlabs Chatbot <c>$tag</c> and <c>$tag(...)</c> expressions into StreamActions <c>{tag}</c> syntax.
    /// </summary>
    /// <param name="input">The Streamlabs template fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The converted template fragment.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions tag names are lowercase ASCII identifiers.")]
    private static string ConvertStreamlabs(string input, MigrationContext context)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('$', StringComparison.Ordinal))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (input[index] == '$' && index + 1 < input.Length)
            {
                char next = input[index + 1];

                // Positional $1..$9 (not followed by another digit so currency amounts like $100 remain intact)
                if (next is >= '1' and <= '9' && (index + 2 >= input.Length || !char.IsDigit(input[index + 2])))
                {
                    _ = output.Append('{').Append(next).Append('}');
                    index += 2;
                    continue;
                }

                if (char.IsLetter(next))
                {
                    int endId = index + 1;
                    while (endId < input.Length && (char.IsLetterOrDigit(input[endId]) || input[endId] == '_'))
                    {
                        endId++;
                    }

                    string name = input[(index + 1)..endId].ToLowerInvariant();
                    string? parenArgs = null;
                    int afterToken = endId;

                    if (endId < input.Length
                        && input[endId] == '('
                        && TryFindBalancedClosingParen(input, endId, respectJsStrings: false, out int closeParen))
                    {
                        parenArgs = input[(endId + 1)..closeParen];
                        afterToken = closeParen + 1;
                    }

                    string rawFragment = input[index..afterToken];
                    if (TryConvertStreamlabsTag(name, parenArgs, rawFragment, context, out string replacement))
                    {
                        _ = output.Append(replacement);
                        index = afterToken;
                        continue;
                    }

                    if (parenArgs is not null && TryConvertStreamlabsTag(name, null, input[index..endId], context, out string bareReplacement))
                    {
                        _ = output.Append(bareReplacement);
                        index = endId;
                        continue;
                    }
                }
            }

            _ = output.Append(input[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Attempts to convert a single Streamlabs <c>$tag</c> or <c>$tag(...)</c> expression.
    /// </summary>
    /// <param name="name">The lowercase tag name without <c>$</c>.</param>
    /// <param name="parenArgs">The optional parenthesized argument string.</param>
    /// <param name="rawFragment">The original fragment for diagnostics.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="replacement">The converted StreamActions output.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertStreamlabsTag(
        string name,
        string? parenArgs,
        string rawFragment,
        MigrationContext context,
        out string replacement)
    {
        if (name.Length == 4
            && name.StartsWith("num", StringComparison.Ordinal)
            && name[3] is >= '1' and <= '9'
            && parenArgs is null)
        {
            replacement = $"{{{name[3]}}}";
            return true;
        }

        if (parenArgs is not null)
        {
            switch (name)
            {
                case "addpoints":
                case "removepoints":
                    {
                        string targetTag = name == "addpoints" ? "points_add" : "points_take";
                        List<string> args = SplitTopLevelCommaArgs(parenArgs);
                        List<string> converted = [.. args.Select(a => StripOuterSingleOrDoubleQuotes(ConvertStreamlabs(a.Trim(), context)))];
                        if (converted.Count >= 2)
                        {
                            // Streamlabs commonly uses $addpoints(user, amount); {points_add} expects <amt> [user].
                            bool firstIsNumeric = double.TryParse(converted[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                            bool secondIsNumeric = double.TryParse(converted[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                            (string amt, string user) = !firstIsNumeric && secondIsNumeric
                                ? (converted[1], converted[0])
                                : (converted[0], converted[1]);
                            replacement = $"{{{targetTag} {FormatAstArgument(amt)} {FormatAstArgument(user)}}}";
                            return true;
                        }

                        if (converted.Count == 1 && !string.IsNullOrEmpty(converted[0]))
                        {
                            replacement = $"{{{targetTag} {FormatAstArgument(converted[0])}}}";
                            return true;
                        }

                        replacement = string.Empty;
                        return false;
                    }

                case "randnum":
                    {
                        List<string> args = SplitTopLevelCommaArgs(parenArgs);
                        if (args.Count >= 2)
                        {
                            string min = ConvertStreamlabs(args[0].Trim(), context);
                            string max = ConvertStreamlabs(args[1].Trim(), context);
                            replacement = $"{{random_int {FormatAstArgument(min)} {FormatAstArgument(max)}}}";
                            return true;
                        }

                        if (args.Count == 1)
                        {
                            string max = ConvertStreamlabs(args[0].Trim(), context);
                            replacement = $"{{random_int 1 {FormatAstArgument(max)}}}";
                            return true;
                        }

                        replacement = "{random_int 1 100}";
                        return true;
                    }

                case "math":
                    if (TryTranspileArithmeticExpression(parenArgs, LegacyBotSource.Streamlabs, context, out string mathTag))
                    {
                        replacement = mathTag;
                        return true;
                    }

                    replacement = string.Empty;
                    return false;

                case "readapi":
                    {
                        string convertedUrl = ConvertStreamlabs(StripOuterSingleOrDoubleQuotes(parenArgs.Trim()), context);
                        replacement = $"{{api {FormatUrlArgument(convertedUrl)}}}";
                        return true;
                    }

                case "readfile":
                    {
                        string fileName = StripOuterSingleOrDoubleQuotes(ConvertStreamlabs(parenArgs.Trim(), context));
                        context.AddVirtualFile(fileName);
                        context.Diagnostics.Add(new MigrationDiagnostic(
                            MigrationDiagnosticSeverity.Info,
                            MigrationDiagnosticCode.ReplacedFileTag,
                            $"Replaced $readfile with scoped {{kv_get}} against virtual file '{fileName}'.",
                            rawFragment));
                        replacement = $"{{kv_get \"files\" \"default\" {FormatQuotedString(fileName)}}}";
                        return true;
                    }

                case "overwritefile":
                    {
                        List<string> args = SplitTopLevelCommaArgs(parenArgs);
                        if (args.Count < 2)
                        {
                            replacement = string.Empty;
                            return false;
                        }

                        string fileName = StripOuterSingleOrDoubleQuotes(ConvertStreamlabs(args[0].Trim(), context));
                        string text = StripOuterSingleOrDoubleQuotes(ConvertStreamlabs(args[1].Trim(), context));
                        bool isAppend = args.Count >= 3
                            && (string.Equals(args[2].Trim(), "true", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2].Trim(), "1", StringComparison.Ordinal));

                        context.AddVirtualFile(fileName);
                        string kvTag = isAppend ? "kv_append" : "kv_set";
                        context.Diagnostics.Add(new MigrationDiagnostic(
                            MigrationDiagnosticSeverity.Info,
                            MigrationDiagnosticCode.ReplacedFileTag,
                            $"Replaced $overwritefile with scoped {{{kvTag}}} against virtual file '{fileName}'.",
                            rawFragment));
                        replacement = $"{{{kvTag} \"files\" \"default\" {FormatQuotedString(fileName)} {FormatAstArgument(text)}}}";
                        return true;
                    }

                default:
                    break;
            }
        }

        if (parenArgs is not null)
        {
            replacement = string.Empty;
            return false;
        }

        replacement = name switch
        {
            "user" or "username" => "{sender}",
            "userid" => "{bare_sender}",
            "touserid" or "target" => "{target}",
            "mychannel" => "{channel}",
            "msg" => "{args}",
            "game" or "mygame" => "{game}",
            "status" or "mystatus" => "{title}",
            "uptime" => "{uptime}",
            "viewers" => "{viewers}",
            "subcount" => "{subscribers}",
            "followage" => "{followage}",
            "followdate" => "{followdate}",
            "hours" => "{hours}",
            "points" => "{points}",
            "currencyname" => "{pointname}",
            "cost" => "{price}",
            "count" or "deathcounter" => "{count}",
            "randuser" => "{random_chatter}",
            "time" => "{time}",
            "rank" => "{rank}",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(replacement);
    }

    #endregion

    #region Firebot $tag / $tag[...] Transpiler

    /// <summary>
    /// Transpiles Firebot <c>$tag</c> and <c>$tag[...]</c> variables into StreamActions <c>{tag}</c> syntax.
    /// </summary>
    /// <param name="input">The Firebot template fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The converted template fragment.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions tag names are lowercase ASCII identifiers.")]
    private static string ConvertFirebot(string input, MigrationContext context)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('$', StringComparison.Ordinal))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (input[index] == '$' && index + 1 < input.Length && char.IsLetter(input[index + 1]))
            {
                int endId = index + 1;
                while (endId < input.Length && (char.IsLetterOrDigit(input[endId]) || input[endId] == '_'))
                {
                    endId++;
                }

                string name = input[(index + 1)..endId].ToLowerInvariant();
                string? bracketArgs = null;
                int afterToken = endId;

                if (endId < input.Length
                    && input[endId] == '['
                    && TryFindBalancedClosingBracket(input, endId, out int closeBracket))
                {
                    bracketArgs = input[(endId + 1)..closeBracket];
                    afterToken = closeBracket + 1;
                }

                if (TryConvertFirebotTag(name, bracketArgs, context, out string replacement))
                {
                    _ = output.Append(replacement);
                    index = afterToken;
                    continue;
                }

                if (bracketArgs is not null && TryConvertFirebotTag(name, null, context, out string bareReplacement))
                {
                    _ = output.Append(bareReplacement);
                    index = endId;
                    continue;
                }
            }

            _ = output.Append(input[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Attempts to convert a single Firebot <c>$tag</c> or <c>$tag[...]</c> expression.
    /// </summary>
    /// <param name="name">The lowercase variable name without <c>$</c>.</param>
    /// <param name="bracketArgs">The optional bracketed argument string.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="replacement">The converted StreamActions output.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertFirebotTag(
        string name,
        string? bracketArgs,
        MigrationContext context,
        out string replacement)
    {
        if (bracketArgs is not null)
        {
            switch (name)
            {
                case "arg":
                    {
                        string argSpec = bracketArgs.Trim();
                        if (string.Equals(argSpec, "all", StringComparison.OrdinalIgnoreCase))
                        {
                            replacement = "{args}";
                            return true;
                        }

                        if (int.TryParse(argSpec, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pos) && pos is >= 1 and <= 9)
                        {
                            replacement = $"{{{pos.ToString(CultureInfo.InvariantCulture)}}}";
                            return true;
                        }

                        replacement = string.Empty;
                        return false;
                    }

                case "counter":
                    {
                        string counterName = StripOuterSingleOrDoubleQuotes(ConvertFirebot(bracketArgs.Trim(), context));
                        replacement = $"{{counter_get {FormatQuotedString(counterName)}}}";
                        return true;
                    }

                case "readapi":
                    {
                        string convertedUrl = ConvertFirebot(StripOuterSingleOrDoubleQuotes(bracketArgs.Trim()), context);
                        replacement = $"{{api {FormatUrlArgument(convertedUrl)}}}";
                        return true;
                    }

                case "points":
                    {
                        string userArg = StripOuterSingleOrDoubleQuotes(ConvertFirebot(bracketArgs.Trim(), context));
                        replacement = string.IsNullOrEmpty(userArg) ? "{points}" : $"{{points {FormatAstArgument(userArg)}}}";
                        return true;
                    }

                case "math":
                    if (TryTranspileArithmeticExpression(bracketArgs, LegacyBotSource.Firebot, context, out string mathTag))
                    {
                        replacement = mathTag;
                        return true;
                    }

                    replacement = string.Empty;
                    return false;

                case "randomnumber":
                    {
                        List<string> args = SplitTopLevelCommaArgs(bracketArgs);
                        if (args.Count >= 2)
                        {
                            string min = ConvertFirebot(args[0].Trim(), context);
                            string max = ConvertFirebot(args[1].Trim(), context);
                            replacement = $"{{random_int {FormatAstArgument(min)} {FormatAstArgument(max)}}}";
                            return true;
                        }

                        if (args.Count == 1 && !string.IsNullOrWhiteSpace(args[0]))
                        {
                            string max = ConvertFirebot(args[0].Trim(), context);
                            replacement = $"{{random_int 1 {FormatAstArgument(max)}}}";
                            return true;
                        }

                        replacement = "{random_int 1 100}";
                        return true;
                    }

                case "time":
                    {
                        string format = StripOuterSingleOrDoubleQuotes(ConvertFirebot(bracketArgs.Trim(), context));
                        replacement = string.IsNullOrEmpty(format)
                            ? "{time}"
                            : $"{{time \"UTC\" {FormatQuotedString(format)}}}";
                        return true;
                    }

                default:
                    break;
            }
        }

        if (bracketArgs is not null)
        {
            replacement = string.Empty;
            return false;
        }

        replacement = name switch
        {
            "user" => "{sender}",
            "streamer" => "{channel}",
            "target" => "{target}",
            "game" => "{game}",
            "streamtitle" => "{title}",
            "uptime" => "{uptime}",
            "viewers" => "{viewers}",
            "followcount" => "{followers}",
            "subcount" => "{subscribers}",
            "points" => "{points}",
            "time" => "{time}",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(replacement);
    }

    #endregion

    #region Streamer.bot %var% & Mix It Up $var Transpiler

    /// <summary>
    /// Transpiles Streamer.bot <c>%var%</c> variables and, when <paramref name="allowMixItUpDollarVars"/> is <see langword="true"/>, Mix It Up <c>$var</c> variables.
    /// </summary>
    /// <param name="input">The template fragment.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="allowMixItUpDollarVars">Whether to also transpile Mix It Up <c>$var</c> variables.</param>
    /// <returns>The converted template fragment.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Variable lookups are normalized to lowercase ASCII identifiers.")]
    private static string ConvertStreamerBotAndMixItUp(
        string input,
        MigrationContext context,
        bool allowMixItUpDollarVars)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        StringBuilder output = new(input.Length);
        int index = 0;

        while (index < input.Length)
        {
            if (input[index] == '%')
            {
                int closePercent = input.IndexOf('%', index + 1);
                if (closePercent > index + 1)
                {
                    string candidate = input[(index + 1)..closePercent];
                    if (IsIdentifierToken(candidate) && TryMapStreamerBotPercentVar(candidate.ToLowerInvariant(), out string mappedPercent))
                    {
                        _ = output.Append(mappedPercent);
                        index = closePercent + 1;
                        continue;
                    }
                }
            }

            if (allowMixItUpDollarVars
                && input[index] == '$'
                && index + 1 < input.Length
                && char.IsLetter(input[index + 1]))
            {
                int endId = index + 1;
                while (endId < input.Length && (char.IsLetterOrDigit(input[endId]) || input[endId] == '_'))
                {
                    endId++;
                }

                string candidate = input[(index + 1)..endId].ToLowerInvariant();
                if (TryMapMixItUpDollarVar(candidate, out string mappedDollar))
                {
                    _ = output.Append(mappedDollar);
                    index = endId;
                    continue;
                }
            }

            _ = output.Append(input[index]);
            index++;
        }

        _ = context;
        return output.ToString();
    }

    /// <summary>
    /// Maps a lowercase Streamer.bot <c>%var%</c> identifier to a StreamActions <c>{tag}</c>.
    /// </summary>
    /// <param name="name">The lowercase variable name.</param>
    /// <param name="mapped">The mapped StreamActions tag.</param>
    /// <returns><see langword="true"/> if mapped; otherwise, <see langword="false"/>.</returns>
    private static bool TryMapStreamerBotPercentVar(string name, out string mapped)
    {
        if (name.Length == 6
            && name.StartsWith("input", StringComparison.Ordinal)
            && name[5] is >= '0' and <= '8')
        {
            int oneBased = name[5] - '0' + 1;
            mapped = $"{{{oneBased.ToString(CultureInfo.InvariantCulture)}}}";
            return true;
        }

        mapped = name switch
        {
            "user" or "username" => "{sender}",
            "userid" => "{sender_id}",
            "rawinput" => "{args}",
            "targetuser" or "targetusername" or "targetuserid" => "{target}",
            "targetusergame" => "{game {target}}",
            "broadcastuser" or "broadcastusername" => "{channel}",
            "game" => "{game}",
            "title" => "{title}",
            "counter" => "{count}",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(mapped);
    }

    /// <summary>
    /// Maps a lowercase Mix It Up <c>$var</c> identifier to a StreamActions <c>{tag}</c>.
    /// </summary>
    /// <param name="name">The lowercase variable name.</param>
    /// <param name="mapped">The mapped StreamActions tag.</param>
    /// <returns><see langword="true"/> if mapped; otherwise, <see langword="false"/>.</returns>
    private static bool TryMapMixItUpDollarVar(string name, out string mapped)
    {
        if (name.Length == 8
            && name.StartsWith("arg", StringComparison.Ordinal)
            && name.EndsWith("text", StringComparison.Ordinal)
            && name[3] is >= '1' and <= '9')
        {
            mapped = $"{{{name[3]}}}";
            return true;
        }

        mapped = name switch
        {
            "username" => "{sender}",
            "userid" => "{sender_id}",
            "allargs" => "{args}",
            "targetusername" => "{target}",
            "targetusergame" => "{game {target}}",
            "streamerusername" => "{channel}",
            "game" => "{game}",
            "streamtitle" => "{title}",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(mapped);
    }

    #endregion

    #region Shared Arithmetic Expression Parser

    /// <summary>
    /// Parses a simple arithmetic expression across Nightbot, StreamElements, Streamlabs, or Firebot into nested <c>{math ...}</c> AST tags.
    /// </summary>
    /// <param name="expression">The raw arithmetic expression.</param>
    /// <param name="source">The bot syntax source for nested tag conversion.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="mathTag">When this method returns <see langword="true"/>, contains the transpiled <c>{math ...}</c> expression.</param>
    /// <returns><see langword="true"/> if the entire expression safely parsed as pure arithmetic; otherwise, <see langword="false"/>.</returns>
    private static bool TryTranspileArithmeticExpression(
        string expression,
        LegacyBotSource source,
        MigrationContext context,
        out string mathTag)
    {
        ArithmeticParser parser = new(expression.Trim(), source, context);
        if (!parser.TryParse(out string parsed) || !parser.IsAtEnd)
        {
            mathTag = string.Empty;
            return false;
        }

        // Only return {math ...} when an actual {math ...} expression was formed (avoid turning bare "123" into "{math ...}").
        if (!parsed.StartsWith("{math ", StringComparison.Ordinal))
        {
            mathTag = string.Empty;
            return false;
        }

        mathTag = parsed;
        return true;
    }

    /// <summary>
    /// Recursive-descent parser for safe arithmetic expressions producing <c>{math op ...}</c> tags.
    /// </summary>
    /// <param name="input">The raw arithmetic expression text.</param>
    /// <param name="source">The originating bot source syntax.</param>
    /// <param name="context">The migration context.</param>
    private sealed class ArithmeticParser(string input, LegacyBotSource source, MigrationContext context)
    {
        /// <summary>
        /// The raw expression string.
        /// </summary>
        private readonly string _input = input;

        /// <summary>
        /// The originating bot source syntax.
        /// </summary>
        private readonly LegacyBotSource _source = source;

        /// <summary>
        /// The migration context for nested tag conversion.
        /// </summary>
        private readonly MigrationContext _context = context;

        /// <summary>
        /// The current character cursor into <see cref="_input"/>.
        /// </summary>
        private int _pos;

        /// <summary>
        /// Gets a value indicating whether the parser consumed the entire expression (ignoring trailing whitespace or a single JS semicolon).
        /// </summary>
        public bool IsAtEnd
        {
            get
            {
                this.SkipWhitespace();
                if (this._pos < this._input.Length && this._input[this._pos] == ';')
                {
                    this._pos++;
                    this.SkipWhitespace();
                }

                return this._pos >= this._input.Length;
            }
        }

        /// <summary>
        /// Parses the top-level additive arithmetic expression.
        /// </summary>
        /// <param name="result">The converted AST node or literal operand.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        public bool TryParse(out string result) => this.TryParseAdditive(out result);

        /// <summary>
        /// Parses <c>+</c> and <c>-</c> binary operators.
        /// </summary>
        /// <param name="result">The parsed AST or operand.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        private bool TryParseAdditive(out string result)
        {
            if (!this.TryParseMultiplicative(out result))
            {
                return false;
            }

            while (true)
            {
                this.SkipWhitespace();
                if (this._pos >= this._input.Length)
                {
                    return true;
                }

                char op = this._input[this._pos];
                if (op is not ('+' or '-'))
                {
                    return true;
                }

                this._pos++;
                if (!this.TryParseMultiplicative(out string right))
                {
                    return false;
                }

                string opName = op == '+' ? "add" : "sub";
                result = $"{{math {opName} {result} {right}}}";
            }
        }

        /// <summary>
        /// Parses <c>*</c>, <c>/</c>, and <c>%</c> binary operators.
        /// </summary>
        /// <param name="result">The parsed AST or operand.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        private bool TryParseMultiplicative(out string result)
        {
            if (!this.TryParsePower(out result))
            {
                return false;
            }

            while (true)
            {
                this.SkipWhitespace();
                if (this._pos >= this._input.Length)
                {
                    return true;
                }

                char op = this._input[this._pos];
                if (op is not ('*' or '/' or '%'))
                {
                    return true;
                }

                this._pos++;
                if (!this.TryParsePower(out string right))
                {
                    return false;
                }

                string opName = op switch
                {
                    '*' => "mul",
                    '/' => "div",
                    _ => "mod",
                };
                result = $"{{math {opName} {result} {right}}}";
            }
        }

        /// <summary>
        /// Parses <c>^</c> exponentiation operators.
        /// </summary>
        /// <param name="result">The parsed AST or operand.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        private bool TryParsePower(out string result)
        {
            if (!this.TryParseUnary(out result))
            {
                return false;
            }

            this.SkipWhitespace();
            if (this._pos < this._input.Length && this._input[this._pos] == '^')
            {
                this._pos++;
                if (!this.TryParsePower(out string exponent))
                {
                    return false;
                }

                result = $"{{math pow {result} {exponent}}}";
            }

            return true;
        }

        /// <summary>
        /// Parses unary <c>+</c> or <c>-</c> prefixes.
        /// </summary>
        /// <param name="result">The parsed AST or operand.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        private bool TryParseUnary(out string result)
        {
            this.SkipWhitespace();
            if (this._pos < this._input.Length && this._input[this._pos] == '-')
            {
                this._pos++;
                this.SkipWhitespace();
                if (this._pos < this._input.Length && char.IsDigit(this._input[this._pos]))
                {
                    if (!this.TryReadNumericLiteral(out string number))
                    {
                        result = string.Empty;
                        return false;
                    }

                    result = "-" + number;
                    return true;
                }

                if (!this.TryParseUnary(out string inner))
                {
                    result = string.Empty;
                    return false;
                }

                result = $"{{math mul -1 {inner}}}";
                return true;
            }

            if (this._pos < this._input.Length && this._input[this._pos] == '+')
            {
                this._pos++;
            }

            return this.TryParsePrimary(out result);
        }

        /// <summary>
        /// Parses a primary operand: parenthesized group, quoted tag, legacy bot tag, numeric literal, or math function call.
        /// </summary>
        /// <param name="result">The parsed AST or operand.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Math function names are lowercase ASCII identifiers.")]
        private bool TryParsePrimary(out string result)
        {
            this.SkipWhitespace();
            if (this._pos >= this._input.Length)
            {
                result = string.Empty;
                return false;
            }

            char current = this._input[this._pos];

            if (current == '(')
            {
                this._pos++;
                if (!this.TryParseAdditive(out result))
                {
                    return false;
                }

                this.SkipWhitespace();
                if (this._pos >= this._input.Length || this._input[this._pos] != ')')
                {
                    return false;
                }

                this._pos++;
                return true;
            }

            // Quoted numeric/tag operand in Nightbot $(eval), e.g. '$(1)' + 1
            if (current is '\'' or '"')
            {
                if (!TryReadQuotedString(this._input, this._pos, out string innerQuoted, out int afterQuote))
                {
                    result = string.Empty;
                    return false;
                }

                this._pos = afterQuote;
                string trimmedQuoted = innerQuoted.Trim();
                if (double.TryParse(trimmedQuoted, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    result = trimmedQuoted;
                    return true;
                }

                string convertedQuoted = ConvertForSource(trimmedQuoted, this._source, this._context);
                if (IsSimpleTagExpression(convertedQuoted))
                {
                    result = convertedQuoted;
                    return true;
                }

                result = string.Empty;
                return false;
            }

            // Legacy bot tag operand ($(1), ${1}, $1, $arg[1], etc.)
            if (current == '$')
            {
                if (this.TryReadLegacyTagOperand(out result))
                {
                    return true;
                }

                result = string.Empty;
                return false;
            }

            // Numeric literal
            if (char.IsDigit(current))
            {
                return this.TryReadNumericLiteral(out result);
            }

            // Function call: Math.floor(...), parseInt(...), Number(...), abs(...), min(...), etc.
            if (char.IsLetter(current))
            {
                int start = this._pos;
                while (this._pos < this._input.Length && (char.IsLetterOrDigit(this._input[this._pos]) || this._input[this._pos] is '.' or '_'))
                {
                    this._pos++;
                }

                string ident = this._input[start..this._pos];
                this.SkipWhitespace();
                if (this._pos >= this._input.Length || this._input[this._pos] != '(')
                {
                    result = string.Empty;
                    return false;
                }

                this._pos++;
                List<string> args = [];
                this.SkipWhitespace();
                if (this._pos < this._input.Length && this._input[this._pos] != ')')
                {
                    while (true)
                    {
                        if (!this.TryParseAdditive(out string argValue))
                        {
                            result = string.Empty;
                            return false;
                        }

                        args.Add(argValue);
                        this.SkipWhitespace();
                        if (this._pos < this._input.Length && this._input[this._pos] == ',')
                        {
                            this._pos++;
                            continue;
                        }

                        break;
                    }
                }

                if (this._pos >= this._input.Length || this._input[this._pos] != ')')
                {
                    result = string.Empty;
                    return false;
                }

                this._pos++;
                string lowerIdent = ident.ToLowerInvariant();
                if (lowerIdent.StartsWith("math.", StringComparison.Ordinal))
                {
                    lowerIdent = lowerIdent[5..];
                }

                if (lowerIdent is "number" or "parsefloat" && args.Count == 1)
                {
                    result = args[0];
                    return true;
                }

                if (lowerIdent == "parseint" && args.Count is 1 or 2)
                {
                    result = args[0];
                    return true;
                }

                if (lowerIdent is "abs" or "ceil" or "floor" && args.Count == 1)
                {
                    result = $"{{math {lowerIdent} {args[0]}}}";
                    return true;
                }

                if (lowerIdent == "round" && args.Count is 1 or 2)
                {
                    result = args.Count == 1
                        ? $"{{math round {args[0]}}}"
                        : $"{{math round {args[0]} {args[1]}}}";
                    return true;
                }

                if (lowerIdent is "min" or "max" && args.Count >= 2)
                {
                    result = $"{{math {lowerIdent} {string.Join(' ', args)}}}";
                    return true;
                }

                if (lowerIdent == "pow" && args.Count == 2)
                {
                    result = $"{{math pow {args[0]} {args[1]}}}";
                    return true;
                }

                result = string.Empty;
                return false;
            }

            result = string.Empty;
            return false;
        }

        /// <summary>
        /// Reads a legacy bot tag starting with <c>$</c> and converts it into a StreamActions <c>{tag}</c> operand.
        /// </summary>
        /// <param name="result">The converted AST tag.</param>
        /// <returns><see langword="true"/> if valid and converted; otherwise, <see langword="false"/>.</returns>
        private bool TryReadLegacyTagOperand(out string result)
        {
            int start = this._pos;
            if (this._pos + 1 >= this._input.Length)
            {
                result = string.Empty;
                return false;
            }

            char second = this._input[this._pos + 1];
            int end;

            if (second == '(' && TryFindBalancedClosingParen(this._input, this._pos + 1, respectJsStrings: true, out int closeParen))
            {
                end = closeParen + 1;
            }
            else if (second == '{' && TryFindBalancedClosingBrace(this._input, this._pos + 1, out int closeBrace))
            {
                end = closeBrace + 1;
            }
            else if (char.IsDigit(second))
            {
                end = this._pos + 2;
            }
            else if (char.IsLetter(second))
            {
                end = this._pos + 1;
                while (end < this._input.Length && (char.IsLetterOrDigit(this._input[end]) || this._input[end] == '_'))
                {
                    end++;
                }

                if (end < this._input.Length && this._input[end] == '[' && TryFindBalancedClosingBracket(this._input, end, out int closeBracket))
                {
                    end = closeBracket + 1;
                }
            }
            else
            {
                result = string.Empty;
                return false;
            }

            string rawOperand = this._input[start..end];
            string converted = ConvertForSource(rawOperand, this._source, this._context);
            if (!IsSimpleTagExpression(converted))
            {
                result = string.Empty;
                return false;
            }

            this._pos = end;
            result = converted;
            return true;
        }

        /// <summary>
        /// Reads a positive integer or decimal literal at the current cursor.
        /// </summary>
        /// <param name="number">The parsed numeric literal text.</param>
        /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
        private bool TryReadNumericLiteral(out string number)
        {
            int start = this._pos;
            bool seenDot = false;
            while (this._pos < this._input.Length)
            {
                char c = this._input[this._pos];
                if (char.IsDigit(c))
                {
                    this._pos++;
                    continue;
                }

                if (c == '.' && !seenDot)
                {
                    seenDot = true;
                    this._pos++;
                    continue;
                }

                break;
            }

            number = this._input[start..this._pos];
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        /// <summary>
        /// Advances <see cref="_pos"/> past whitespace characters.
        /// </summary>
        private void SkipWhitespace()
        {
            while (this._pos < this._input.Length && char.IsWhiteSpace(this._input[this._pos]))
            {
                this._pos++;
            }
        }
    }

    #endregion

    #region Helpers & Context

    /// <summary>
    /// Mutable per-conversion state tracking extracted guards, secrets, virtual files, upgraded Action Block actions, and diagnostics.
    /// </summary>
    private sealed class MigrationContext
    {
        /// <summary>
        /// Case-insensitive set of allowed usernames already recorded.
        /// </summary>
        private readonly HashSet<string> _allowedUserSet = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Case-insensitive set of allowed game names already recorded.
        /// </summary>
        private readonly HashSet<string> _allowedGameSet = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Case-insensitive set of referenced virtual files already recorded.
        /// </summary>
        private readonly HashSet<string> _virtualFileSet = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Map of normalized secret names to their discovered allowed hostnames.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _secrets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Ordered list of secret names in discovery order.
        /// </summary>
        private readonly List<string> _secretOrder = [];

        /// <summary>
        /// Initializes a new <see cref="MigrationContext"/> for a conversion run.
        /// </summary>
        /// <param name="commandTrigger">The command trigger name.</param>
        /// <param name="source">The legacy bot source.</param>
        public MigrationContext(string commandTrigger, LegacyBotSource source)
        {
            _ = source;
            this.SecretName = $"{NormalizeTriggerForSecretName(commandTrigger)}_TOKEN";
        }

        /// <summary>
        /// Gets the normalized secret name used when migrating <c>(token)</c> placeholders inside HTTP tags.
        /// </summary>
        public string SecretName { get; }

        /// <summary>
        /// Gets or sets a value indicating whether <c>(adminonlyedit)</c> was extracted.
        /// </summary>
        public bool LockModeratorEdits { get; set; }

        /// <summary>
        /// Gets the ordered list of unique allowed usernames.
        /// </summary>
        public List<string> AllowedUsers { get; } = [];

        /// <summary>
        /// Gets or sets the extracted stream status requirement.
        /// </summary>
        public StreamStatusRequirement StreamStatusRequirement { get; set; } = StreamStatusRequirement.Both;

        /// <summary>
        /// Gets the ordered list of unique allowed games.
        /// </summary>
        public List<string> AllowedGames { get; } = [];

        /// <summary>
        /// Gets or sets the extracted minimum required argument count.
        /// </summary>
        public int? MinRequiredArgs { get; set; }

        /// <summary>
        /// Gets the ordered list of unique referenced virtual files.
        /// </summary>
        public List<string> ReferencedVirtualFiles { get; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether an Action Blocks routine upgrade is required.
        /// </summary>
        public bool RequiresActionBlockUpgrade { get; set; }

        /// <summary>
        /// Gets the list of synthesized Action Block descriptors.
        /// </summary>
        public List<MigratedActionBlockDescriptor> UpgradedActions { get; } = [];

        /// <summary>
        /// Gets the list of collected migration diagnostics.
        /// </summary>
        public List<MigrationDiagnostic> Diagnostics { get; } = [];

        /// <summary>
        /// Adds a username to <see cref="AllowedUsers"/> if not already present.
        /// </summary>
        /// <param name="username">The username to add.</param>
        public void AddAllowedUser(string username)
        {
            if (this._allowedUserSet.Add(username))
            {
                this.AllowedUsers.Add(username);
            }
        }

        /// <summary>
        /// Adds a game name to <see cref="AllowedGames"/> if not already present.
        /// </summary>
        /// <param name="game">The game/category name to add.</param>
        public void AddAllowedGame(string game)
        {
            if (this._allowedGameSet.Add(game))
            {
                this.AllowedGames.Add(game);
            }
        }

        /// <summary>
        /// Adds a virtual filename to <see cref="ReferencedVirtualFiles"/> if non-empty and not already present.
        /// </summary>
        /// <param name="fileName">The virtual file name.</param>
        public void AddVirtualFile(string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName) && this._virtualFileSet.Add(fileName))
            {
                this.ReferencedVirtualFiles.Add(fileName);
            }
        }

        /// <summary>
        /// Registers <paramref name="secretName"/> in discovery order even if no host pattern could be parsed.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        public void EnsureSecretRegistered(string secretName)
        {
            if (!this._secrets.ContainsKey(secretName))
            {
                this._secrets[secretName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                this._secretOrder.Add(secretName);
            }
        }

        /// <summary>
        /// Records an allowed target host for <paramref name="secretName"/>.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <param name="host">The extracted hostname.</param>
        public void AddSecretHost(string secretName, string host)
        {
            this.EnsureSecretRegistered(secretName);
            if (!string.IsNullOrWhiteSpace(host))
            {
                _ = this._secrets[secretName].Add(host);
            }
        }

        /// <summary>
        /// Builds the immutable list of <see cref="DiscoveredSecretReference"/> descriptors.
        /// </summary>
        /// <returns>The read-only list of discovered secret references.</returns>
        public ReadOnlyCollection<DiscoveredSecretReference> BuildSecretReferences()
        {
            List<DiscoveredSecretReference> result = new(this._secretOrder.Count);
            foreach (string secretName in this._secretOrder)
            {
                result.Add(new DiscoveredSecretReference(secretName, this._secrets[secretName].ToList().AsReadOnly()));
            }

            return result.AsReadOnly();
        }
    }

    /// <summary>
    /// Converts a fragment using the converter appropriate for <paramref name="source"/>.
    /// </summary>
    /// <param name="fragment">The fragment text.</param>
    /// <param name="source">The legacy bot source.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The converted fragment.</returns>
    private static string ConvertForSource(string fragment, LegacyBotSource source, MigrationContext context) =>
        source switch
        {
            LegacyBotSource.PhantomBot => ConvertPhantomBot(fragment, context, insideHttpTag: false),
            LegacyBotSource.Nightbot => ConvertNightbot(fragment, context, insideUrl: false),
            LegacyBotSource.StreamElements => ConvertStreamElements(fragment, context),
            LegacyBotSource.Streamlabs => ConvertStreamlabs(fragment, context),
            LegacyBotSource.Firebot => ConvertFirebot(fragment, context),
            LegacyBotSource.StreamerBot => ConvertStreamerBotAndMixItUp(fragment, context, allowMixItUpDollarVars: false),
            LegacyBotSource.MixItUp => ConvertStreamerBotAndMixItUp(fragment, context, allowMixItUpDollarVars: true),
            _ => fragment,
        };

    /// <summary>
    /// Normalizes a legacy command trigger into an uppercase alphanumeric-underscore secret prefix.
    /// </summary>
    /// <param name="trigger">The raw command trigger.</param>
    /// <returns>The normalized secret name prefix.</returns>
    private static string NormalizeTriggerForSecretName(string? trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return "COMMAND";
        }

        string trimmed = trigger.Trim().TrimStart('!', '/', '?', '.');
        StringBuilder builder = new(trimmed.Length);
        bool lastUnderscore = false;

        foreach (char c in trimmed)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                _ = builder.Append(char.ToUpperInvariant(c));
                lastUnderscore = false;
            }
            else if (!lastUnderscore && builder.Length > 0)
            {
                _ = builder.Append('_');
                lastUnderscore = true;
            }
        }

        string normalized = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(normalized) ? "COMMAND" : normalized;
    }

    /// <summary>
    /// Determines whether an <c>(unescape ...)</c> argument wraps a dangerous file or external HTTP tag.
    /// </summary>
    /// <param name="rawArgs">The argument inside <c>(unescape ...)</c>.</param>
    /// <returns><see langword="true"/> if wrapping a file or HTTP tag; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsDangerousUnescapeTarget(string rawArgs)
    {
        string trimmed = rawArgs.TrimStart();
        return trimmed.StartsWith("(readfile", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("(writefile", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("(customapi", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("(api", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the hostname from <paramref name="url"/> when a <c>$secret.*</c> placeholder is present and records it in <paramref name="context"/>.
    /// </summary>
    /// <param name="url">The converted URL string.</param>
    /// <param name="context">The migration context.</param>
    private static void RecordSecretHostIfReferenced(string url, MigrationContext context)
    {
        if (!url.Contains("$secret.", StringComparison.Ordinal))
        {
            return;
        }

        string sanitized = SecretPlaceholderRegex().Replace(url, "REDACTED");
        if (TryExtractHost(sanitized, out string host))
        {
            context.AddSecretHost(context.SecretName, host);
        }
    }

    /// <summary>
    /// Attempts to extract the hostname from an HTTP/HTTPS URL string, even if nested <c>{tag}</c> expressions appear in the path or query.
    /// </summary>
    /// <param name="url">The URL string.</param>
    /// <param name="host">When this method returns <see langword="true"/>, contains the extracted hostname.</param>
    /// <returns><see langword="true"/> if a valid hostname was extracted; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractHost(string url, out string host)
    {
        host = string.Empty;
        string trimmed = StripOuterQuotes(url.Trim());
        int schemeIdx = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx < 0)
        {
            return false;
        }

        int authorityStart = schemeIdx + 3;
        int authorityEnd = trimmed.IndexOfAny(['/', '?', '#'], authorityStart);
        string authority = authorityEnd < 0
            ? trimmed[authorityStart..]
            : trimmed[authorityStart..authorityEnd];

        int atIdx = authority.LastIndexOf('@');
        if (atIdx >= 0)
        {
            authority = authority[(atIdx + 1)..];
        }

        if (authority.StartsWith('['))
        {
            int closeBracket = authority.IndexOf(']', StringComparison.Ordinal);
            if (closeBracket > 1)
            {
                host = authority[1..closeBracket];
                return !string.IsNullOrWhiteSpace(host);
            }

            return false;
        }

        int colonIdx = authority.IndexOf(':', StringComparison.Ordinal);
        host = colonIdx >= 0 ? authority[..colonIdx] : authority;
        return !string.IsNullOrWhiteSpace(host) && !host.Contains('{', StringComparison.Ordinal);
    }

    /// <summary>
    /// Formats a URL argument for <c>{api ...}</c>, quoting URLs that contain nested <c>{tag}</c> interpolations, spaces, or quotes.
    /// </summary>
    /// <param name="url">The converted URL text.</param>
    /// <returns>The formatted <c>{api}</c> URL argument.</returns>
    private static string FormatUrlArgument(string url)
    {
        string unwrapped = StripOuterQuotes(url.Trim());
        return unwrapped.Contains('{', StringComparison.Ordinal)
            || unwrapped.Contains('}', StringComparison.Ordinal)
            || unwrapped.Contains("$secret.", StringComparison.Ordinal)
            || unwrapped.Any(char.IsWhiteSpace)
            || unwrapped.Contains('"', StringComparison.Ordinal)
            ? FormatQuotedString(unwrapped)
            : unwrapped;
    }

    /// <summary>
    /// Formats an AST tag argument, keeping standalone <c>{tag}</c> nodes or simple unspaced tokens unquoted, and quoting strings with spaces or mixed literals.
    /// </summary>
    /// <param name="arg">The argument value.</param>
    /// <returns>The formatted AST argument.</returns>
    private static string FormatAstArgument(string arg)
    {
        string trimmed = arg.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? "\"\""
            : IsSimpleTagExpression(trimmed)
            ? trimmed
            : !trimmed.Any(char.IsWhiteSpace)
            && !trimmed.Contains('"', StringComparison.Ordinal)
            && !trimmed.Contains('{', StringComparison.Ordinal)
            && !trimmed.Contains('}', StringComparison.Ordinal)
            ? trimmed
            : FormatQuotedString(trimmed);
    }

    /// <summary>
    /// Wraps <paramref name="value"/> in double quotes while escaping literal quotes outside nested <c>{...}</c> tag expressions.
    /// </summary>
    /// <param name="value">The string value to quote.</param>
    /// <returns>The quoted string suitable for <see cref="TemplateParser"/>.</returns>
    private static string FormatQuotedString(string value)
    {
        StringBuilder builder = new(value.Length + 2);
        _ = builder.Append('"');
        int braceDepth = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\\' && i + 1 < value.Length && value[i + 1] is '{' or '}')
            {
                _ = builder.Append(c).Append(value[i + 1]);
                i++;
                continue;
            }

            if (c == '{')
            {
                braceDepth++;
                _ = builder.Append(c);
                continue;
            }

            if (c == '}' && braceDepth > 0)
            {
                braceDepth--;
                _ = builder.Append(c);
                continue;
            }

            if (c == '"' && braceDepth == 0)
            {
                _ = builder.Append("\\\"");
                continue;
            }

            if (c == '\\' && braceDepth == 0 && (i + 1 >= value.Length || value[i + 1] is not ('{' or '}' or '"' or '\\' or 'n')))
            {
                _ = builder.Append("\\\\");
                continue;
            }

            _ = builder.Append(c);
        }

        _ = builder.Append('"');
        return builder.ToString();
    }

    /// <summary>
    /// Determines whether <paramref name="value"/> is a single complete <c>{tag ...}</c> expression without trailing text.
    /// </summary>
    /// <param name="value">The string to inspect.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> starts with <c>{</c> and closes at the final character; otherwise, <see langword="false"/>.</returns>
    private static bool IsSimpleTagExpression(string value) =>
        value.Length >= 3
        && value[0] == '{'
        && value[^1] == '}'
        && TryFindBalancedClosingBrace(value, 0, out int closeIdx)
        && closeIdx == value.Length - 1;

    /// <summary>
    /// Formats a tag taking zero or one optional PhantomBot argument.
    /// </summary>
    /// <param name="targetTag">The StreamActions tag name.</param>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP tag.</param>
    /// <returns>The formatted <c>{tag}</c> or <c>{tag arg}</c> expression.</returns>
    private static string FormatOptionalSingleArgTag(string targetTag, string rawArgs, MigrationContext context, bool insideHttpTag)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            return $"{{{targetTag}}}";
        }

        string converted = ConvertPhantomBot(rawArgs.Trim(), context, insideHttpTag);
        return $"{{{targetTag} {FormatAstArgument(converted)}}}";
    }

    /// <summary>
    /// Formats a tag taking zero or more space-separated PhantomBot arguments.
    /// </summary>
    /// <param name="targetTag">The StreamActions tag name.</param>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP tag.</param>
    /// <returns>The formatted <c>{tag ...}</c> expression.</returns>
    private static string FormatSpaceSeparatedTag(string targetTag, string rawArgs, MigrationContext context, bool insideHttpTag)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            return $"{{{targetTag}}}";
        }

        List<string> tokens = SplitTopLevelWhitespaceTokens(rawArgs.Trim());
        List<string> converted = [.. tokens.Select(t => FormatAstArgument(ConvertPhantomBot(t, context, insideHttpTag)))];
        return $"{{{targetTag} {string.Join(' ', converted)}}}";
    }

    /// <summary>
    /// Formats a tag taking comma- or space-separated PhantomBot arguments.
    /// </summary>
    /// <param name="targetTag">The StreamActions tag name.</param>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP tag.</param>
    /// <returns>The formatted <c>{tag ...}</c> expression.</returns>
    private static string FormatCommaOrSpaceTag(string targetTag, string rawArgs, MigrationContext context, bool insideHttpTag)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            return $"{{{targetTag}}}";
        }

        List<string> args = SplitCommaOrSpaceArgs(rawArgs, context, insideHttpTag, isPhantomBot: true);
        List<string> formatted = [.. args.Select(FormatAstArgument)];
        return $"{{{targetTag} {string.Join(' ', formatted)}}}";
    }

    /// <summary>
    /// Formats a PhantomBot math function tag into <c>{math op arg1 ...}</c>.
    /// </summary>
    /// <param name="mathOp">The StreamActions <c>{math}</c> operator name.</param>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP tag.</param>
    /// <returns>The formatted <c>{math ...}</c> expression.</returns>
    private static string FormatMathTag(string mathOp, string rawArgs, MigrationContext context, bool insideHttpTag)
    {
        List<string> args = SplitCommaOrSpaceArgs(rawArgs, context, insideHttpTag, isPhantomBot: true);
        List<string> formatted = [.. args.Select(FormatAstArgument)];
        return formatted.Count == 0
            ? $"{{math {mathOp}}}"
            : $"{{math {mathOp} {string.Join(' ', formatted)}}}";
    }

    /// <summary>
    /// Finds the matching closing parenthesis for an opening <c>(</c> at <paramref name="openIndex"/>.
    /// </summary>
    /// <param name="text">The source string.</param>
    /// <param name="openIndex">The index of the opening <c>(</c>.</param>
    /// <param name="respectJsStrings">Whether to ignore parentheses inside JS string literals.</param>
    /// <param name="closeIndex">When this method returns <see langword="true"/>, contains the index of the matching <c>)</c>.</param>
    /// <returns><see langword="true"/> if a matching <c>)</c> was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryFindBalancedClosingParen(
        string text,
        int openIndex,
        bool respectJsStrings,
        out int closeIndex)
    {
        int depth = 0;
        char activeQuote = '\0';

        for (int i = openIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (respectJsStrings)
            {
                if (activeQuote != '\0')
                {
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        continue;
                    }

                    if (c == activeQuote)
                    {
                        activeQuote = '\0';
                    }

                    continue;
                }

                if (c is '\'' or '"' or '`')
                {
                    activeQuote = c;
                    continue;
                }
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    closeIndex = i;
                    return true;
                }
            }
        }

        closeIndex = -1;
        return false;
    }

    /// <summary>
    /// Finds the matching closing brace for an opening <c>{</c> at <paramref name="openIndex"/>.
    /// </summary>
    /// <param name="text">The source string.</param>
    /// <param name="openIndex">The index of the opening <c>{</c>.</param>
    /// <param name="closeIndex">When this method returns <see langword="true"/>, contains the index of the matching <c>}</c>.</param>
    /// <returns><see langword="true"/> if a matching <c>}</c> was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryFindBalancedClosingBrace(string text, int openIndex, out int closeIndex)
    {
        int depth = 0;
        char activeQuote = '\0';

        for (int i = openIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (activeQuote != '\0')
            {
                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

                if (c == activeQuote)
                {
                    activeQuote = '\0';
                }

                continue;
            }

            if (c is '\'' or '"')
            {
                activeQuote = c;
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    closeIndex = i;
                    return true;
                }
            }
        }

        closeIndex = -1;
        return false;
    }

    /// <summary>
    /// Finds the matching closing bracket for an opening <c>[</c> at <paramref name="openIndex"/>.
    /// </summary>
    /// <param name="text">The source string.</param>
    /// <param name="openIndex">The index of the opening <c>[</c>.</param>
    /// <param name="closeIndex">When this method returns <see langword="true"/>, contains the index of the matching <c>]</c>.</param>
    /// <returns><see langword="true"/> if a matching <c>]</c> was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryFindBalancedClosingBracket(string text, int openIndex, out int closeIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    closeIndex = i;
                    return true;
                }
            }
        }

        closeIndex = -1;
        return false;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into its first whitespace-delimited token and the remaining trimmed text.
    /// </summary>
    /// <param name="text">The trimmed input text.</param>
    /// <param name="first">The first token.</param>
    /// <param name="rest">The remainder after whitespace.</param>
    private static void SplitFirstToken(string text, out string first, out string rest)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                first = text[..i];
                rest = text[(i + 1)..].TrimStart();
                return;
            }
        }

        first = text;
        rest = string.Empty;
    }

    /// <summary>
    /// Splits <paramref name="rawArgs"/> on top-level commas when present, or top-level whitespace otherwise, converting PhantomBot sub-expressions.
    /// </summary>
    /// <param name="rawArgs">The raw argument text.</param>
    /// <param name="context">The migration context.</param>
    /// <param name="insideHttpTag">Whether inside an HTTP tag.</param>
    /// <param name="isPhantomBot">Whether to convert PhantomBot nested tags in each argument.</param>
    /// <returns>The list of split and converted argument strings.</returns>
    private static List<string> SplitCommaOrSpaceArgs(string rawArgs, MigrationContext context, bool insideHttpTag, bool isPhantomBot)
    {
        List<string> rawParts = HasTopLevelComma(rawArgs)
            ? SplitTopLevelCommaArgs(rawArgs)
            : SplitTopLevelWhitespaceTokens(rawArgs.Trim());

        List<string> results = new(rawParts.Count);
        foreach (string part in rawParts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            string converted = isPhantomBot
                ? ConvertPhantomBot(trimmed, context, insideHttpTag)
                : trimmed;
            results.Add(converted);
        }

        return results;
    }

    /// <summary>
    /// Splits a two-argument PhantomBot string (comma-separated if a top-level comma exists, otherwise first whitespace).
    /// </summary>
    /// <param name="rawArgs">The raw argument string.</param>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    private static void SplitCommaOrSpacePair(string rawArgs, out string first, out string second)
    {
        if (HasTopLevelComma(rawArgs))
        {
            List<string> parts = SplitTopLevelCommaArgs(rawArgs);
            first = parts.Count >= 1 ? parts[0].Trim() : string.Empty;
            second = parts.Count >= 2 ? string.Join(", ", parts.Skip(1)).Trim() : string.Empty;
            return;
        }

        SplitFirstToken(rawArgs, out first, out second);
    }

    /// <summary>
    /// Checks whether <paramref name="text"/> contains a comma outside nested parentheses, braces, or brackets.
    /// </summary>
    /// <param name="text">The string to inspect.</param>
    /// <returns><see langword="true"/> if a top-level comma is present; otherwise, <see langword="false"/>.</returns>
    private static bool HasTopLevelComma(string text)
    {
        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;

        foreach (char c in text)
        {
            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')' when parenDepth > 0:
                    parenDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}' when braceDepth > 0:
                    braceDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']' when bracketDepth > 0:
                    bracketDepth--;
                    break;
                case ',' when parenDepth == 0 && braceDepth == 0 && bracketDepth == 0:
                    return true;
                default:
                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits <paramref name="text"/> on commas outside nested parentheses, braces, brackets, and quotes.
    /// </summary>
    /// <param name="text">The string to split.</param>
    /// <returns>The split argument segments.</returns>
    private static List<string> SplitTopLevelCommaArgs(string text)
    {
        List<string> parts = [];
        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;
        char activeQuote = '\0';
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (activeQuote != '\0')
            {
                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

                if (c == activeQuote)
                {
                    activeQuote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    activeQuote = c;
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')' when parenDepth > 0:
                    parenDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}' when braceDepth > 0:
                    braceDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']' when bracketDepth > 0:
                    bracketDepth--;
                    break;
                case ',' when parenDepth == 0 && braceDepth == 0 && bracketDepth == 0:
                    parts.Add(text[start..i]);
                    start = i + 1;
                    break;
                default:
                    break;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>
    /// Splits <paramref name="text"/> on whitespace outside nested parentheses, braces, brackets, and quotes.
    /// </summary>
    /// <param name="text">The string to split.</param>
    /// <returns>The split tokens.</returns>
    private static List<string> SplitTopLevelWhitespaceTokens(string text)
    {
        List<string> tokens = [];
        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;
        char activeQuote = '\0';
        int start = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (activeQuote != '\0')
            {
                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

                if (c == activeQuote)
                {
                    activeQuote = '\0';
                }

                continue;
            }

            if (char.IsWhiteSpace(c) && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
            {
                if (start >= 0)
                {
                    tokens.Add(text[start..i]);
                    start = -1;
                }

                continue;
            }

            if (start < 0)
            {
                start = i;
            }

            switch (c)
            {
                case '"' or '\'':
                    activeQuote = c;
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')' when parenDepth > 0:
                    parenDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}' when braceDepth > 0:
                    braceDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']' when bracketDepth > 0:
                    bracketDepth--;
                    break;
                default:
                    break;
            }
        }

        if (start >= 0)
        {
            tokens.Add(text[start..]);
        }

        return tokens;
    }

    /// <summary>
    /// Splits shell-style arguments while respecting single/double quotes and nested <c>$(...)</c> / <c>${...}</c> groups.
    /// </summary>
    /// <param name="text">The raw argument text.</param>
    /// <returns>The split argument list.</returns>
    private static List<string> SplitShellStyleArgs(string text) => SplitTopLevelWhitespaceTokens(text);

    /// <summary>
    /// Finds the index of the first whitespace character outside nested braces/parens/quotes, or <c>-1</c> if none exists.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The index of the first top-level whitespace character, or <c>-1</c>.</returns>
    private static int FindTopLevelWhitespace(string text)
    {
        int braceDepth = 0;
        int parenDepth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}' && braceDepth > 0)
            {
                braceDepth--;
            }
            else if (c == '(')
            {
                parenDepth++;
            }
            else if (c == ')' && parenDepth > 0)
            {
                parenDepth--;
            }
            else if (char.IsWhiteSpace(c) && braceDepth == 0 && parenDepth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Parses elements of a JavaScript array literal containing only string or numeric literals.
    /// </summary>
    /// <param name="itemsBody">The text inside <c>[...]</c>.</param>
    /// <returns>The parsed list of string items, or <see langword="null"/> if any element is unsafe or invalid.</returns>
    private static List<string>? TryParseJsStringOrNumericArrayItems(string itemsBody)
    {
        List<string> rawElements = SplitTopLevelCommaArgs(itemsBody);
        List<string> items = new(rawElements.Count);

        foreach (string rawElement in rawElements)
        {
            string trimmed = rawElement.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (IsSingleJsStringLiteral(trimmed))
            {
                items.Add(UnescapeJsStringLiteral(trimmed));
                continue;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                items.Add(trimmed);
                continue;
            }

            return null;
        }

        return items;
    }

    /// <summary>
    /// Splits a JavaScript expression on top-level <c>||</c> operators outside quotes and parentheses.
    /// </summary>
    /// <param name="expression">The JS expression text.</param>
    /// <returns>The split parts, or <see langword="null"/> if fewer than two parts exist.</returns>
    private static List<string>? SplitTopLevelLogicalOr(string expression)
    {
        List<string> parts = [];
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;
        char activeQuote = '\0';
        int start = 0;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            if (activeQuote != '\0')
            {
                if (c == '\\' && i + 1 < expression.Length)
                {
                    i++;
                    continue;
                }

                if (c == activeQuote)
                {
                    activeQuote = '\0';
                }

                continue;
            }

            if (c is '\'' or '"' or '`')
            {
                activeQuote = c;
                continue;
            }

            if (c == '(')
            {
                parenDepth++;
            }
            else if (c == ')' && parenDepth > 0)
            {
                parenDepth--;
            }
            else if (c == '[')
            {
                bracketDepth++;
            }
            else if (c == ']' && bracketDepth > 0)
            {
                bracketDepth--;
            }
            else if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}' && braceDepth > 0)
            {
                braceDepth--;
            }
            else if (c == '|'
                && i + 1 < expression.Length
                && expression[i + 1] == '|'
                && parenDepth == 0
                && bracketDepth == 0
                && braceDepth == 0)
            {
                parts.Add(expression[start..i]);
                i++;
                start = i + 1;
            }
        }

        parts.Add(expression[start..]);
        return parts.Count >= 2 ? parts : null;
    }

    /// <summary>
    /// Determines whether <paramref name="literal"/> is a single complete single-quoted or double-quoted JS string literal.
    /// </summary>
    /// <param name="literal">The candidate string literal.</param>
    /// <returns><see langword="true"/> if a single valid string literal; otherwise, <see langword="false"/>.</returns>
    private static bool IsSingleJsStringLiteral(string literal) => literal.Length >= 2 && (literal[0] == '\'' || literal[0] == '"') && TryReadQuotedString(literal, 0, out _, out int afterIndex) && afterIndex == literal.Length;

    /// <summary>
    /// Reads a single-quoted or double-quoted string literal starting at <paramref name="startIndex"/>.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="startIndex">The index of the opening quote.</param>
    /// <param name="unescaped">The unescaped string content.</param>
    /// <param name="afterIndex">The index immediately following the closing quote.</param>
    /// <returns><see langword="true"/> if a matching closing quote was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryReadQuotedString(string text, int startIndex, out string unescaped, out int afterIndex)
    {
        char quote = text[startIndex];
        StringBuilder builder = new();

        for (int i = startIndex + 1; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                char escaped = text[i + 1];
                _ = builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => escaped,
                });
                i++;
                continue;
            }

            if (c == quote)
            {
                unescaped = builder.ToString();
                afterIndex = i + 1;
                return true;
            }

            _ = builder.Append(c);
        }

        unescaped = string.Empty;
        afterIndex = -1;
        return false;
    }

    /// <summary>
    /// Unescapes a single-quoted or double-quoted JS string literal.
    /// </summary>
    /// <param name="literal">The quoted literal.</param>
    /// <returns>The unquoted, unescaped string.</returns>
    private static string UnescapeJsStringLiteral(string literal) =>
        TryReadQuotedString(literal, 0, out string unescaped, out _) ? unescaped : StripOuterSingleOrDoubleQuotes(literal);

    /// <summary>
    /// Removes one pair of matching outer double quotes if present.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <returns>The unquoted string.</returns>
    private static string StripOuterQuotes(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;

    /// <summary>
    /// Removes one pair of matching outer single or double quotes if present.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <returns>The unquoted string.</returns>
    private static string StripOuterSingleOrDoubleQuotes(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    /// <summary>
    /// Checks whether <paramref name="value"/> contains only ASCII letters, digits, or underscores.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> if valid identifier characters; otherwise, <see langword="false"/>.</returns>
    private static bool IsIdentifierToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether <paramref name="value"/> starts with an HTTP or HTTPS URL scheme.
    /// </summary>
    /// <param name="value">The token to inspect.</param>
    /// <returns><see langword="true"/> if HTTP/HTTPS URL; otherwise, <see langword="false"/>.</returns>
    private static bool LooksLikeHttpUrl(string value)
    {
        string unwrapped = StripOuterQuotes(value);
        return unwrapped.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || unwrapped.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Collapses redundant spaces left behind when stripping inline meta-guard, standalone secret, or Action Block-upgraded tags.
    /// </summary>
    /// <param name="text">The template text to clean.</param>
    /// <returns>The normalized string.</returns>
    private static string NormalizeStrippedSpacing(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string collapsed = MultiHorizontalSpaceRegex().Replace(text, " ");
        return collapsed.Trim();
    }

    #endregion
}

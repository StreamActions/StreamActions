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

using StreamActions.Core.Templates;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Actions;

/// <summary>
/// Validation diagnostic emitted for an <see cref="ActionBlockConditionGroup"/>.
/// </summary>
/// <param name="Message">The human-readable error message.</param>
public sealed record ConditionValidationDiagnostic(string Message);

/// <summary>
/// Result of validating an <see cref="ActionBlockConditionGroup"/> in either structured or freeform mode.
/// </summary>
/// <param name="IsValid">Whether the condition syntax, operators, and embedded <c>{tag}</c> templates are valid.</param>
/// <param name="Diagnostics">Ordered validation diagnostics when <paramref name="IsValid"/> is <see langword="false"/>.</param>
public sealed record ConditionValidationResult(
    bool IsValid,
    IReadOnlyList<ConditionValidationDiagnostic> Diagnostics);

/// <summary>
/// Evaluates and validates <see cref="ActionBlockConditionGroup"/> predicates across both Action Block structured clauses and freeform boolean expressions.
/// </summary>
public static partial class ActionBlockConditionEvaluator
{
    /// <summary>
    /// Timeout applied to <see cref="ActionBlockComparisonOperator.MatchesRegex"/> and <see cref="ActionBlockComparisonOperator.NotMatchesRegex"/> evaluations.
    /// </summary>
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Validates an <see cref="ActionBlockConditionGroup"/> for syntax, operator validity, regex validity, and embedded <c>{tag}</c> AST errors.
    /// </summary>
    /// <param name="condition">The condition group to validate.</param>
    /// <param name="tagRegistry">Optional tag registry used to validate embedded <c>{tag}</c> templates.</param>
    /// <param name="platformId">Optional platform identifier for platform-scoped tag validation.</param>
    /// <returns>The <see cref="ConditionValidationResult"/>.</returns>
    public static ConditionValidationResult Validate(
        ActionBlockConditionGroup? condition,
        ITagRegistry? tagRegistry = null,
        string? platformId = null)
    {
        if (condition is null)
        {
            return new(true, []);
        }

        List<ConditionValidationDiagnostic> diagnostics = [];

        if (condition.UseFreeform)
        {
            if (string.IsNullOrWhiteSpace(condition.FreeformExpression))
            {
                diagnostics.Add(new("Freeform expression cannot be empty."));
            }
            else if (!TryParseFreeform(condition.FreeformExpression, tagRegistry, platformId, out _, out string? parseError))
            {
                diagnostics.Add(new(parseError ?? "Invalid freeform condition expression."));
            }

            return new(diagnostics.Count == 0, diagnostics);
        }

        for (int i = 0; i < condition.Clauses.Count; i++)
        {
            ActionBlockConditionClause clause = condition.Clauses[i];
            if (string.IsNullOrWhiteSpace(clause.LeftExpression))
            {
                diagnostics.Add(new($"Clause {i + 1}: Left expression cannot be empty."));
            }
            else
            {
                ValidateTemplateExpression(clause.LeftExpression, $"Clause {i + 1} left expression", tagRegistry, platformId, diagnostics);
            }

            if (clause.IndentLevel < 0)
            {
                diagnostics.Add(new($"Clause {i + 1}: IndentLevel cannot be negative."));
            }

            ValidateTemplateExpression(clause.RightExpression, $"Clause {i + 1} right expression", tagRegistry, platformId, diagnostics);

            if ((clause.Operator == ActionBlockComparisonOperator.MatchesRegex
                    || clause.Operator == ActionBlockComparisonOperator.NotMatchesRegex)
                && !clause.RightExpression.Contains('{', StringComparison.Ordinal))
            {
                try
                {
                    _ = new Regex(clause.RightExpression, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, _regexTimeout);
                }
                catch (ArgumentException ex)
                {
                    diagnostics.Add(new($"Clause {i + 1}: Invalid regex pattern: {ex.Message}"));
                }
            }
        }

        return new(diagnostics.Count == 0, diagnostics);
    }

    /// <summary>
    /// Evaluates an <see cref="ActionBlockConditionGroup"/> asynchronously using the provided operand resolver.
    /// </summary>
    /// <param name="condition">The condition group to evaluate (<see langword="null"/> or empty evaluates to <see langword="true"/>).</param>
    /// <param name="resolveExpressionAsync">Callback resolving a template/operand expression into its current runtime string value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the condition holds; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> EvaluateAsync(
        ActionBlockConditionGroup? condition,
        Func<string, CancellationToken, ValueTask<string>> resolveExpressionAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolveExpressionAsync);

        if (condition is null)
        {
            return true;
        }

        if (condition.UseFreeform)
        {
            return string.IsNullOrWhiteSpace(condition.FreeformExpression) || (!TryParseFreeform(condition.FreeformExpression, null, null, out FreeformNode? root, out string? error)
                ? throw new FormatException(error ?? "Invalid freeform condition expression.")
                : await EvaluateFreeformNodeAsync(root, resolveExpressionAsync, cancellationToken).ConfigureAwait(false));
        }

        if (condition.Clauses.Count == 0)
        {
            return true;
        }

        (bool result, _) = await EvaluateStructuredGroupAsync(
            condition.Clauses,
            minIndent: condition.Clauses[0].IndentLevel,
            startIndex: 0,
            resolveExpressionAsync,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Evaluates a single comparison between two resolved string values using the specified <see cref="ActionBlockComparisonOperator"/>.
    /// </summary>
    /// <param name="leftValue">The resolved left-hand value.</param>
    /// <param name="op">The comparison operator.</param>
    /// <param name="rightValue">The resolved right-hand value.</param>
    /// <returns><see langword="true"/> if the comparison succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool EvaluateComparison(
        string? leftValue,
        ActionBlockComparisonOperator op,
        string? rightValue)
    {
        string left = leftValue ?? string.Empty;
        string right = rightValue ?? string.Empty;

        return op switch
        {
            ActionBlockComparisonOperator.Equals => AreEqual(left, right),
            ActionBlockComparisonOperator.NotEquals => !AreEqual(left, right),
            ActionBlockComparisonOperator.GreaterThan => CompareValues(left, right) > 0,
            ActionBlockComparisonOperator.GreaterThanOrEqual => CompareValues(left, right) >= 0,
            ActionBlockComparisonOperator.LessThan => CompareValues(left, right) < 0,
            ActionBlockComparisonOperator.LessThanOrEqual => CompareValues(left, right) <= 0,
            ActionBlockComparisonOperator.Contains => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            ActionBlockComparisonOperator.NotContains => !left.Contains(right, StringComparison.OrdinalIgnoreCase),
            ActionBlockComparisonOperator.In => IsInSet(left, right),
            ActionBlockComparisonOperator.NotIn => !IsInSet(left, right),
            ActionBlockComparisonOperator.MatchesRegex => MatchesNonBacktrackingRegex(left, right),
            ActionBlockComparisonOperator.NotMatchesRegex => !MatchesNonBacktrackingRegex(left, right),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported comparison operator."),
        };
    }

    /// <summary>
    /// Evaluates structured clauses honoring <see cref="ActionBlockConditionClause.IndentLevel"/> grouping and <c>AND</c>/<c>OR</c> combinators.
    /// </summary>
    private static async ValueTask<(bool Result, int NextIndex)> EvaluateStructuredGroupAsync(
        IReadOnlyList<ActionBlockConditionClause> clauses,
        int minIndent,
        int startIndex,
        Func<string, CancellationToken, ValueTask<string>> resolveExpressionAsync,
        CancellationToken cancellationToken)
    {
        List<bool> terms = [];
        List<ActionBlockLogicalCombinator> combinators = [];
        int index = startIndex;

        while (index < clauses.Count)
        {
            ActionBlockConditionClause current = clauses[index];
            if (current.IndentLevel < minIndent)
            {
                break;
            }

            bool termValue;
            ActionBlockLogicalCombinator nextCombinator;

            if (current.IndentLevel > minIndent)
            {
                (termValue, index) = await EvaluateStructuredGroupAsync(
                    clauses,
                    current.IndentLevel,
                    index,
                    resolveExpressionAsync,
                    cancellationToken).ConfigureAwait(false);

                nextCombinator = index > 0 && index - 1 < clauses.Count
                    ? clauses[index - 1].CombinatorWithNext
                    : ActionBlockLogicalCombinator.And;
            }
            else
            {
                string leftResolved = await resolveExpressionAsync(current.LeftExpression, cancellationToken).ConfigureAwait(false);
                string rightResolved = await resolveExpressionAsync(current.RightExpression, cancellationToken).ConfigureAwait(false);
                termValue = EvaluateComparison(leftResolved, current.Operator, rightResolved);
                nextCombinator = current.CombinatorWithNext;
                index++;
            }

            terms.Add(termValue);

            if (index >= clauses.Count || clauses[index].IndentLevel < minIndent)
            {
                break;
            }

            combinators.Add(nextCombinator);
        }

        // Standard boolean precedence: AND binds tighter than OR.
        if (terms.Count == 0)
        {
            return (true, index);
        }

        bool orAccumulator = false;
        bool andAccumulator = terms[0];
        for (int i = 0; i < combinators.Count; i++)
        {
            if (combinators[i] == ActionBlockLogicalCombinator.And)
            {
                andAccumulator = andAccumulator && terms[i + 1];
            }
            else
            {
                orAccumulator = orAccumulator || andAccumulator;
                andAccumulator = terms[i + 1];
            }
        }

        return (orAccumulator || andAccumulator, index);
    }

    /// <summary>
    /// Compares two strings using invariant numeric equality when both parse as numbers, or case-insensitive ordinal equality otherwise.
    /// </summary>
    private static bool AreEqual(string left, string right) =>
        decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal leftNum)
            && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rightNum)
            ? leftNum == rightNum
            : bool.TryParse(left, out bool leftBool) && bool.TryParse(right, out bool rightBool)
            ? leftBool == rightBool
            : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compares two strings numerically when both parse as decimals, or lexicographically (ordinal ignore-case) otherwise.
    /// </summary>
    private static int CompareValues(string left, string right) =>
        decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal leftNum)
        && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rightNum)
            ? leftNum.CompareTo(rightNum)
            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether <paramref name="left"/> matches any element in <paramref name="right"/> (JSON array or comma/pipe-delimited list).
    /// </summary>
    private static bool IsInSet(string left, string right)
    {
        string trimmedRight = right.Trim();
        if (trimmedRight.StartsWith('[') && trimmedRight.EndsWith(']'))
        {
            trimmedRight = trimmedRight[1..^1];
        }

        char separator = trimmedRight.Contains('|', StringComparison.Ordinal) && !trimmedRight.Contains(',', StringComparison.Ordinal)
            ? '|'
            : ',';

        string[] items = trimmedRight.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string rawItem in items)
        {
            string item = Unquote(rawItem);
            if (AreEqual(left, item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates a regular expression match using <see cref="RegexOptions.NonBacktracking"/> and a 50 ms timeout.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Runtime regex evaluation should safely return false on malformed dynamic patterns or timeouts.")]
    private static bool MatchesNonBacktrackingRegex(string input, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(
                input,
                pattern,
                RegexOptions.NonBacktracking | RegexOptions.CultureInvariant,
                _regexTimeout);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Strips matching surrounding single or double quotes from a token.
    /// </summary>
    private static string Unquote(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length >= 2
            && ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\''))
            ? trimmed[1..^1]
            : trimmed;
    }

    /// <summary>
    /// Validates embedded <c>{tag}</c> syntax in a condition operand.
    /// </summary>
    private static void ValidateTemplateExpression(
        string expression,
        string label,
        ITagRegistry? tagRegistry,
        string? platformId,
        List<ConditionValidationDiagnostic> diagnostics)
    {
        if (!expression.Contains('{', StringComparison.Ordinal))
        {
            return;
        }

        TemplateParseResult parseResult = TemplateParser.Parse(expression, tagRegistry, platformId);
        foreach (TemplateDiagnostic diag in parseResult.Diagnostics)
        {
            if (diag.Severity == TemplateDiagnosticSeverity.Error || diag.Code == TemplateDiagnosticCode.UnknownTag)
            {
                diagnostics.Add(new($"{label}: {diag.Message}"));
            }
        }
    }

    /// <summary>
    /// Evaluates a parsed freeform AST node.
    /// </summary>
    private static async ValueTask<bool> EvaluateFreeformNodeAsync(
        FreeformNode node,
        Func<string, CancellationToken, ValueTask<string>> resolveExpressionAsync,
        CancellationToken cancellationToken)
    {
        switch (node)
        {
            case FreeformLogicalNode logicalNode:
                {
                    bool leftResult = await EvaluateFreeformNodeAsync(logicalNode.Left, resolveExpressionAsync, cancellationToken).ConfigureAwait(false);
                    return (logicalNode.Combinator != ActionBlockLogicalCombinator.And || leftResult) && ((logicalNode.Combinator == ActionBlockLogicalCombinator.Or && leftResult) || await EvaluateFreeformNodeAsync(logicalNode.Right, resolveExpressionAsync, cancellationToken).ConfigureAwait(false));
                }

            case FreeformNotNode notNode:
                return !await EvaluateFreeformNodeAsync(notNode.Inner, resolveExpressionAsync, cancellationToken).ConfigureAwait(false);

            case FreeformComparisonNode comparisonNode:
                {
                    string left = await resolveExpressionAsync(comparisonNode.LeftExpression, cancellationToken).ConfigureAwait(false);
                    string right = await resolveExpressionAsync(comparisonNode.RightExpression, cancellationToken).ConfigureAwait(false);
                    return EvaluateComparison(left, comparisonNode.Operator, right);
                }

            case FreeformTruthinessNode truthinessNode:
                {
                    string value = await resolveExpressionAsync(truthinessNode.Expression, cancellationToken).ConfigureAwait(false);
                    return bool.TryParse(value.Trim(), out bool parsedBool)
                        ? parsedBool
                        : !string.IsNullOrWhiteSpace(value) && !string.Equals(value.Trim(), "0", StringComparison.Ordinal);
                }

            default:
                return false;
        }
    }

    /// <summary>
    /// Parses a freeform boolean condition string into an AST node and validates syntax/operators/templates.
    /// </summary>
    private static bool TryParseFreeform(
        string expression,
        ITagRegistry? tagRegistry,
        string? platformId,
        [NotNullWhen(true)] out FreeformNode? root,
        out string? error)
    {
        root = null;
        if (!TryTokenizeFreeform(expression, out List<FreeformToken>? tokens, out error))
        {
            return false;
        }

        if (tokens.Count == 0)
        {
            error = "Freeform expression cannot be empty.";
            return false;
        }

        FreeformParser parser = new(tokens, tagRegistry, platformId);
        return parser.TryParse(out root, out error);
    }

    /// <summary>
    /// Tokenizes a freeform condition expression preserving <c>{...}</c>, <c>[...]</c>, and quoted strings.
    /// </summary>
    private static bool TryTokenizeFreeform(
        string input,
        [NotNullWhen(true)] out List<FreeformToken>? tokens,
        out string? error)
    {
        tokens = [];
        int i = 0;

        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new(FreeformTokenKind.OpenParen, "("));
                i++;
                continue;
            }

            if (c == ')')
            {
                tokens.Add(new(FreeformTokenKind.CloseParen, ")"));
                i++;
                continue;
            }

            if (i + 1 < input.Length)
            {
                string twoChar = input.Substring(i, 2);
                if (twoChar is "&&" or "||" or "==" or "!=" or ">=" or "<=")
                {
                    FreeformTokenKind kind = twoChar switch
                    {
                        "&&" => FreeformTokenKind.And,
                        "||" => FreeformTokenKind.Or,
                        _ => FreeformTokenKind.Operator,
                    };
                    tokens.Add(new(kind, twoChar));
                    i += 2;
                    continue;
                }
            }

            if (c is '>' or '<')
            {
                tokens.Add(new(FreeformTokenKind.Operator, c.ToString()));
                i++;
                continue;
            }

            if (c == '!')
            {
                tokens.Add(new(FreeformTokenKind.Not, "!"));
                i++;
                continue;
            }

            if (c is '"' or '\'')
            {
                if (!TryReadQuoted(input, ref i, c, out string? quoted))
                {
                    tokens = null;
                    error = "Unterminated quoted string in freeform condition.";
                    return false;
                }

                tokens.Add(new(FreeformTokenKind.Operand, quoted));
                continue;
            }

            if (c == '{')
            {
                if (!TryReadBalanced(input, ref i, '{', '}', out string? braced))
                {
                    tokens = null;
                    error = "Unterminated '{...}' template tag in freeform condition.";
                    return false;
                }

                tokens.Add(new(FreeformTokenKind.Operand, braced));
                continue;
            }

            if (c == '[')
            {
                if (!TryReadBalanced(input, ref i, '[', ']', out string? bracketed))
                {
                    tokens = null;
                    error = "Unterminated '[...]' list literal in freeform condition.";
                    return false;
                }

                tokens.Add(new(FreeformTokenKind.Operand, bracketed));
                continue;
            }

            int start = i;
            while (i < input.Length
                && !char.IsWhiteSpace(input[i])
                && input[i] is not '(' and not ')' and not '>' and not '<' and not '!' and not '=' and not '&' and not '|')
            {
                i++;
            }

            string word = input[start..i];
            if (string.Equals(word, "AND", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new(FreeformTokenKind.And, word));
            }
            else if (string.Equals(word, "OR", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new(FreeformTokenKind.Or, word));
            }
            else if (string.Equals(word, "NOT", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new(FreeformTokenKind.Not, word));
            }
            else if (TryParseNamedOperator(word, out _))
            {
                tokens.Add(new(FreeformTokenKind.Operator, word));
            }
            else
            {
                tokens.Add(new(FreeformTokenKind.Operand, word));
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Reads a quoted string literal supporting backslash escapes.
    /// </summary>
    private static bool TryReadQuoted(string input, ref int index, char quoteChar, [NotNullWhen(true)] out string? value)
    {
        int start = index + 1;
        index++;
        while (index < input.Length)
        {
            if (input[index] == '\\' && index + 1 < input.Length)
            {
                index += 2;
                continue;
            }

            if (input[index] == quoteChar)
            {
                value = input[start..index].Replace($"\\{quoteChar}", quoteChar.ToString(), StringComparison.Ordinal);
                index++;
                return true;
            }

            index++;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Reads a balanced delimiter block such as <c>{...}</c> or <c>[...]</c>.
    /// </summary>
    private static bool TryReadBalanced(string input, ref int index, char openChar, char closeChar, [NotNullWhen(true)] out string? value)
    {
        int start = index;
        int depth = 0;
        while (index < input.Length)
        {
            char c = input[index];
            if (c == openChar)
            {
                depth++;
            }
            else if (c == closeChar)
            {
                depth--;
                if (depth == 0)
                {
                    index++;
                    value = input[start..index];
                    return true;
                }
            }

            index++;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Maps a freeform operator token to its <see cref="ActionBlockComparisonOperator"/>.
    /// </summary>
    private static bool TryParseNamedOperator(string token, out ActionBlockComparisonOperator op)
    {
        switch (token.ToUpperInvariant())
        {
            case "==":
            case "EQUALS":
                op = ActionBlockComparisonOperator.Equals;
                return true;
            case "!=":
            case "NOTEQUALS":
                op = ActionBlockComparisonOperator.NotEquals;
                return true;
            case ">":
            case "GREATERTHAN":
                op = ActionBlockComparisonOperator.GreaterThan;
                return true;
            case ">=":
            case "GREATERTHANOREQUAL":
                op = ActionBlockComparisonOperator.GreaterThanOrEqual;
                return true;
            case "<":
            case "LESSTHAN":
                op = ActionBlockComparisonOperator.LessThan;
                return true;
            case "<=":
            case "LESSTHANOREQUAL":
                op = ActionBlockComparisonOperator.LessThanOrEqual;
                return true;
            case "CONTAINS":
                op = ActionBlockComparisonOperator.Contains;
                return true;
            case "NOTCONTAINS":
                op = ActionBlockComparisonOperator.NotContains;
                return true;
            case "IN":
                op = ActionBlockComparisonOperator.In;
                return true;
            case "NOTIN":
                op = ActionBlockComparisonOperator.NotIn;
                return true;
            case "MATCHES":
            case "MATCHESREGEX":
                op = ActionBlockComparisonOperator.MatchesRegex;
                return true;
            case "NOTMATCHES":
            case "NOTMATCHESREGEX":
                op = ActionBlockComparisonOperator.NotMatchesRegex;
                return true;
            default:
                op = default;
                return false;
        }
    }

    /// <summary>
    /// Freeform lexer token kind.
    /// </summary>
    private enum FreeformTokenKind
    {
        /// <summary>An operand literal, state key, or template.</summary>
        Operand,

        /// <summary>A comparison operator.</summary>
        Operator,

        /// <summary>Logical AND (<c>AND</c> or <c>&amp;&amp;</c>).</summary>
        And,

        /// <summary>Logical OR (<c>OR</c> or <c>||</c>).</summary>
        Or,

        /// <summary>Logical NOT (<c>NOT</c> or <c>!</c>).</summary>
        Not,

        /// <summary>Left parenthesis <c>(</c>.</summary>
        OpenParen,

        /// <summary>Right parenthesis <c>)</c>.</summary>
        CloseParen,
    }

    /// <summary>
    /// Single token produced by the freeform lexer.
    /// </summary>
    /// <param name="Kind">Token kind.</param>
    /// <param name="Value">Raw token text.</param>
    private sealed record FreeformToken(FreeformTokenKind Kind, string Value);

    /// <summary>
    /// Base AST node for parsed freeform boolean conditions.
    /// </summary>
    private abstract record FreeformNode;

    /// <summary>
    /// Binary logical node (<c>AND</c> / <c>OR</c>).
    /// </summary>
    /// <param name="Left">Left child node.</param>
    /// <param name="Combinator">Logical combinator.</param>
    /// <param name="Right">Right child node.</param>
    private sealed record FreeformLogicalNode(FreeformNode Left, ActionBlockLogicalCombinator Combinator, FreeformNode Right) : FreeformNode;

    /// <summary>
    /// Unary logical negation node.
    /// </summary>
    /// <param name="Inner">Negated inner node.</param>
    private sealed record FreeformNotNode(FreeformNode Inner) : FreeformNode;

    /// <summary>
    /// Comparison node between two expressions.
    /// </summary>
    /// <param name="LeftExpression">Left operand expression.</param>
    /// <param name="Operator">Comparison operator.</param>
    /// <param name="RightExpression">Right operand expression.</param>
    private sealed record FreeformComparisonNode(string LeftExpression, ActionBlockComparisonOperator Operator, string RightExpression) : FreeformNode;

    /// <summary>
    /// Standalone boolean truthiness node.
    /// </summary>
    /// <param name="Expression">Expression evaluated as a boolean.</param>
    private sealed record FreeformTruthinessNode(string Expression) : FreeformNode;

    /// <summary>
    /// Recursive-descent parser for freeform Action Block conditions.
    /// </summary>
    /// <param name="tokens">Lexer tokens.</param>
    /// <param name="tagRegistry">Optional tag registry for template validation.</param>
    /// <param name="platformId">Optional platform identifier for template validation.</param>
    private sealed class FreeformParser(
        IReadOnlyList<FreeformToken> tokens,
        ITagRegistry? tagRegistry,
        string? platformId)
    {
        /// <summary>
        /// Token stream.
        /// </summary>
        private readonly IReadOnlyList<FreeformToken> _tokens = tokens;

        /// <summary>
        /// Optional tag registry for embedded template validation.
        /// </summary>
        private readonly ITagRegistry? _tagRegistry = tagRegistry;

        /// <summary>
        /// Optional platform identifier for embedded template validation.
        /// </summary>
        private readonly string? _platformId = platformId;

        /// <summary>
        /// Current token cursor.
        /// </summary>
        private int _position;

        /// <summary>
        /// Parses the full token stream into a root <see cref="FreeformNode"/>.
        /// </summary>
        /// <param name="root">Outputs the parsed root node.</param>
        /// <param name="error">Outputs an error message when parsing fails.</param>
        /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
        public bool TryParse([NotNullWhen(true)] out FreeformNode? root, out string? error)
        {
            if (!this.TryParseOr(out root, out error))
            {
                return false;
            }

            if (this._position < this._tokens.Count)
            {
                error = $"Unexpected token '{this._tokens[this._position].Value}' in freeform condition.";
                root = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses an <c>OR</c> expression level.
        /// </summary>
        private bool TryParseOr([NotNullWhen(true)] out FreeformNode? node, out string? error)
        {
            if (!this.TryParseAnd(out node, out error))
            {
                return false;
            }

            while (this.PeekKind() == FreeformTokenKind.Or)
            {
                this._position++;
                if (!this.TryParseAnd(out FreeformNode? right, out error))
                {
                    node = null;
                    return false;
                }

                node = new FreeformLogicalNode(node, ActionBlockLogicalCombinator.Or, right);
            }

            return true;
        }

        /// <summary>
        /// Parses an <c>AND</c> expression level.
        /// </summary>
        private bool TryParseAnd([NotNullWhen(true)] out FreeformNode? node, out string? error)
        {
            if (!this.TryParseUnary(out node, out error))
            {
                return false;
            }

            while (this.PeekKind() == FreeformTokenKind.And)
            {
                this._position++;
                if (!this.TryParseUnary(out FreeformNode? right, out error))
                {
                    node = null;
                    return false;
                }

                node = new FreeformLogicalNode(node, ActionBlockLogicalCombinator.And, right);
            }

            return true;
        }

        /// <summary>
        /// Parses a unary <c>NOT</c> or primary expression.
        /// </summary>
        private bool TryParseUnary([NotNullWhen(true)] out FreeformNode? node, out string? error)
        {
            if (this.PeekKind() == FreeformTokenKind.Not)
            {
                this._position++;
                if (!this.TryParseUnary(out FreeformNode? inner, out error))
                {
                    node = null;
                    return false;
                }

                node = new FreeformNotNode(inner);
                return true;
            }

            return this.TryParsePrimary(out node, out error);
        }

        /// <summary>
        /// Parses parenthesized groups or comparison/truthiness terms.
        /// </summary>
        private bool TryParsePrimary([NotNullWhen(true)] out FreeformNode? node, out string? error)
        {
            node = null;
            if (this._position >= this._tokens.Count)
            {
                error = "Unexpected end of freeform condition.";
                return false;
            }

            FreeformToken current = this._tokens[this._position];
            if (current.Kind == FreeformTokenKind.OpenParen)
            {
                this._position++;
                if (!this.TryParseOr(out FreeformNode? inner, out error))
                {
                    return false;
                }

                if (this.PeekKind() != FreeformTokenKind.CloseParen)
                {
                    error = "Expected closing ')' in freeform condition.";
                    return false;
                }

                this._position++;
                node = inner;
                return true;
            }

            if (current.Kind != FreeformTokenKind.Operand)
            {
                error = $"Expected operand before '{current.Value}' in freeform condition.";
                return false;
            }

            this._position++;
            if (!this.ValidateOperandTemplate(current.Value, out error))
            {
                return false;
            }

            if (this.PeekKind() == FreeformTokenKind.Operator)
            {
                FreeformToken opToken = this._tokens[this._position++];
                if (!TryParseNamedOperator(opToken.Value, out ActionBlockComparisonOperator op))
                {
                    error = $"Unsupported operator '{opToken.Value}' in freeform condition.";
                    return false;
                }

                if (this._position >= this._tokens.Count || this._tokens[this._position].Kind != FreeformTokenKind.Operand)
                {
                    error = $"Expected right-hand operand after '{opToken.Value}'.";
                    return false;
                }

                FreeformToken rightToken = this._tokens[this._position++];
                if (!this.ValidateOperandTemplate(rightToken.Value, out error))
                {
                    return false;
                }

                if (op is ActionBlockComparisonOperator.MatchesRegex or ActionBlockComparisonOperator.NotMatchesRegex
                    && !rightToken.Value.Contains('{', StringComparison.Ordinal))
                {
                    try
                    {
                        _ = new Regex(rightToken.Value, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, _regexTimeout);
                    }
                    catch (ArgumentException ex)
                    {
                        error = $"Invalid regex pattern '{rightToken.Value}': {ex.Message}";
                        return false;
                    }
                }

                node = new FreeformComparisonNode(current.Value, op, rightToken.Value);
                error = null;
                return true;
            }

            node = new FreeformTruthinessNode(current.Value);
            error = null;
            return true;
        }

        /// <summary>
        /// Validates embedded <c>{tag}</c> templates when validating syntax.
        /// </summary>
        private bool ValidateOperandTemplate(string expression, out string? error)
        {
            if (this._tagRegistry is not null && expression.Contains('{', StringComparison.Ordinal))
            {
                TemplateParseResult parseResult = TemplateParser.Parse(expression, this._tagRegistry, this._platformId);
                foreach (TemplateDiagnostic diag in parseResult.Diagnostics)
                {
                    if (diag.Severity == TemplateDiagnosticSeverity.Error || diag.Code == TemplateDiagnosticCode.UnknownTag)
                    {
                        error = diag.Message;
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Peeks the next token kind without advancing the cursor.
        /// </summary>
        private FreeformTokenKind? PeekKind() =>
            this._position < this._tokens.Count ? this._tokens[this._position].Kind : null;
    }
}

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

using System.Text;

namespace StreamActions.Core.Templates;

/// <summary>
/// Parses curly-brace <c>{tag ...}</c> template strings into an immutable <see cref="TemplateNode"/> AST with structured syntax and registry diagnostics.
/// </summary>
public sealed class TemplateParser
{
    /// <summary>
    /// Hard recursion ceiling to protect the parser call stack against pathological inputs with thousands of unclosed braces.
    /// </summary>
    private const int _hardRecursionLimit = TemplateExecutionLimits.MaxAstNestingDepth * 4;

    /// <summary>
    /// The raw source template text being parsed.
    /// </summary>
    private readonly string _input;

    /// <summary>
    /// Optional tag registry used to emit warnings for unknown tag names.
    /// </summary>
    private readonly ITagRegistry? _tagRegistry;

    /// <summary>
    /// Optional platform identifier used during registry validation.
    /// </summary>
    private readonly string? _platformId;

    /// <summary>
    /// Mutable list collecting parse diagnostics in source order.
    /// </summary>
    private readonly List<TemplateDiagnostic> _diagnostics = [];

    /// <summary>
    /// Current zero-based character cursor in <see cref="_input"/>.
    /// </summary>
    private int _index;

    /// <summary>
    /// Initializes a new parser state for <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The raw template text.</param>
    /// <param name="tagRegistry">Optional tag registry for unknown-tag warnings.</param>
    /// <param name="platformId">Optional platform identifier for layered registry checks.</param>
    private TemplateParser(string input, ITagRegistry? tagRegistry, string? platformId)
    {
        this._input = input;
        this._tagRegistry = tagRegistry;
        this._platformId = platformId;
    }

    /// <summary>
    /// Parses <paramref name="template"/> into a <see cref="TemplateParseResult"/> containing the AST root and any syntax or validation diagnostics.
    /// </summary>
    /// <param name="template">The raw template string to parse.</param>
    /// <param name="tagRegistry">Optional tag registry; when supplied, unrecognized tag names emit <see cref="TemplateDiagnosticCode.UnknownTag"/> warnings.</param>
    /// <param name="platformId">Optional platform identifier used when checking <paramref name="tagRegistry"/>.</param>
    /// <returns>The parsed <see cref="TemplateParseResult"/>.</returns>
    public static TemplateParseResult Parse(
        string? template,
        ITagRegistry? tagRegistry = null,
        string? platformId = null)
    {
        if (string.IsNullOrEmpty(template))
        {
            return new TemplateParseResult(new LiteralNode(string.Empty), []);
        }

        TemplateParser parser = new(template, tagRegistry, platformId);
        TemplateNode root = parser.ParseTopLevel();
        return new TemplateParseResult(root, parser._diagnostics.AsReadOnly());
    }

    /// <summary>
    /// Parses top-level plain text, escape sequences, and root-level <c>{tag}</c> expressions.
    /// </summary>
    /// <returns>The root <see cref="TemplateNode"/>.</returns>
    private TemplateNode ParseTopLevel()
    {
        List<TemplateNode> nodes = [];
        StringBuilder literal = new();

        while (this._index < this._input.Length)
        {
            char c = this._input[this._index];
            if (c == '\\')
            {
                if (this.TryConsumeEscape(literal))
                {
                    continue;
                }

                _ = literal.Append('\\');
                this._index++;
                continue;
            }

            if (c == '{')
            {
                FlushLiteral(literal, nodes);
                nodes.Add(this.ParseTag(1));
                continue;
            }

            if (c == '}')
            {
                this._diagnostics.Add(new TemplateDiagnostic(
                    TemplateDiagnosticSeverity.Error,
                    TemplateDiagnosticCode.UnexpectedClosingBrace,
                    "Unexpected closing '}' brace.",
                    this._index,
                    1));
                this._index++;
                continue;
            }

            _ = literal.Append(c);
            this._index++;
        }

        FlushLiteral(literal, nodes);

        return nodes.Count == 0
            ? new LiteralNode(string.Empty)
            : nodes.Count == 1
            ? nodes[0]
            : new CompositeNode(nodes.AsReadOnly());
    }

    /// <summary>
    /// Parses a <c>{tagName arg1 arg2 ...}</c> expression starting at the opening <c>{</c> brace.
    /// </summary>
    /// <param name="tagDepth">The 1-based tag nesting depth of this tag.</param>
    /// <returns>The parsed <see cref="TagNode"/>.</returns>
    private TagNode ParseTag(int tagDepth)
    {
        int openBracePos = this._index;
        this._index++;

        if (tagDepth > TemplateExecutionLimits.MaxAstNestingDepth)
        {
            this._diagnostics.Add(new TemplateDiagnostic(
                TemplateDiagnosticSeverity.Error,
                TemplateDiagnosticCode.MaxNestingDepthExceeded,
                $"Template nesting depth {tagDepth} exceeds maximum of {TemplateExecutionLimits.MaxAstNestingDepth}.",
                openBracePos,
                1));
        }

        if (tagDepth > _hardRecursionLimit)
        {
            this._index = this._input.Length;
            return new TagNode(string.Empty, []);
        }

        this.SkipWhitespace();

        if (this._index >= this._input.Length)
        {
            this._diagnostics.Add(new TemplateDiagnostic(
                TemplateDiagnosticSeverity.Error,
                TemplateDiagnosticCode.UnclosedBrace,
                "Unclosed '{' brace.",
                openBracePos,
                1));
            return new TagNode(string.Empty, []);
        }

        if (this._input[this._index] == '}')
        {
            int length = this._index - openBracePos + 1;
            this._diagnostics.Add(new TemplateDiagnostic(
                TemplateDiagnosticSeverity.Error,
                TemplateDiagnosticCode.EmptyTag,
                "Empty '{}' tag expression.",
                openBracePos,
                length));
            this._index++;
            return new TagNode(string.Empty, []);
        }

        int tagNameStart = this._index;
        StringBuilder tagNameBuilder = new();
        while (this._index < this._input.Length)
        {
            char c = this._input[this._index];
            if (char.IsWhiteSpace(c) || c is '}' or '{' or '"')
            {
                break;
            }

            if (c == '\\' && this.TryConsumeEscape(tagNameBuilder))
            {
                continue;
            }

            _ = tagNameBuilder.Append(c);
            this._index++;
        }

        string tagName = tagNameBuilder.ToString();
        int tagNameLength = this._index - tagNameStart;

        if (string.IsNullOrEmpty(tagName))
        {
            this._diagnostics.Add(new TemplateDiagnostic(
                TemplateDiagnosticSeverity.Error,
                TemplateDiagnosticCode.EmptyTag,
                "Missing tag name after '{'.",
                openBracePos,
                1));
        }
        else if (this._tagRegistry is not null && !this._tagRegistry.IsRegistered(tagName, this._platformId))
        {
            this._diagnostics.Add(new TemplateDiagnostic(
                TemplateDiagnosticSeverity.Warning,
                TemplateDiagnosticCode.UnknownTag,
                $"Unknown tag '{tagName}'.",
                tagNameStart,
                Math.Max(1, tagNameLength)));
        }

        List<TemplateNode> arguments = [];
        while (true)
        {
            this.SkipWhitespace();

            if (this._index >= this._input.Length)
            {
                this._diagnostics.Add(new TemplateDiagnostic(
                    TemplateDiagnosticSeverity.Error,
                    TemplateDiagnosticCode.UnclosedBrace,
                    "Unclosed '{' brace.",
                    openBracePos,
                    1));
                break;
            }

            char c = this._input[this._index];
            if (c == '}')
            {
                this._index++;
                break;
            }

            if (c == '"')
            {
                arguments.Add(this.ParseQuotedArgument(tagDepth));
            }
            else
            {
                arguments.Add(this.ParseUnquotedArgument(tagDepth));
            }
        }

        return new TagNode(tagName, arguments.AsReadOnly());
    }

    /// <summary>
    /// Parses a double-quoted argument string as a <see cref="CompositeNode"/>, supporting escape sequences and nested tag interpolation.
    /// </summary>
    /// <param name="currentDepth">The current tag nesting depth.</param>
    /// <returns>A <see cref="CompositeNode"/> representing the quoted argument.</returns>
    private CompositeNode ParseQuotedArgument(int currentDepth)
    {
        int quoteStart = this._index;
        this._index++;

        List<TemplateNode> children = [];
        StringBuilder literal = new();
        bool closed = false;

        while (this._index < this._input.Length)
        {
            char c = this._input[this._index];
            if (c == '"')
            {
                this._index++;
                closed = true;
                break;
            }

            if (c == '\\')
            {
                if (this.TryConsumeEscape(literal))
                {
                    continue;
                }

                _ = literal.Append('\\');
                this._index++;
                continue;
            }

            if (c == '{')
            {
                FlushLiteral(literal, children);
                children.Add(this.ParseTag(currentDepth + 1));
                continue;
            }

            if (c == '}')
            {
                this._diagnostics.Add(new TemplateDiagnostic(
                    TemplateDiagnosticSeverity.Error,
                    TemplateDiagnosticCode.UnexpectedClosingBrace,
                    "Unexpected closing '}' brace inside quoted string.",
                    this._index,
                    1));
                this._index++;
                continue;
            }

            _ = literal.Append(c);
            this._index++;
        }

        if (!closed)
        {
            this._diagnostics.Add(new TemplateDiagnostic(
                TemplateDiagnosticSeverity.Error,
                TemplateDiagnosticCode.UnclosedQuote,
                "Unclosed quoted string argument.",
                quoteStart,
                1));
        }

        FlushLiteral(literal, children);
        return new CompositeNode(children.AsReadOnly());
    }

    /// <summary>
    /// Parses an unquoted word, inline tag, or key-value <c>key="..."</c> argument until whitespace or a closing <c>}</c> brace.
    /// </summary>
    /// <param name="currentDepth">The current tag nesting depth.</param>
    /// <returns>A <see cref="TemplateNode"/> representing the parsed argument.</returns>
    private TemplateNode ParseUnquotedArgument(int currentDepth)
    {
        List<TemplateNode> parts = [];
        StringBuilder literal = new();

        while (this._index < this._input.Length)
        {
            char c = this._input[this._index];
            if (char.IsWhiteSpace(c) || c == '}')
            {
                break;
            }

            if (c == '"')
            {
                FlushLiteral(literal, parts);
                CompositeNode quoted = this.ParseQuotedArgument(currentDepth);
                foreach (TemplateNode child in quoted.Children)
                {
                    AppendMergedPart(parts, child);
                }

                continue;
            }

            if (c == '{')
            {
                FlushLiteral(literal, parts);
                parts.Add(this.ParseTag(currentDepth + 1));
                continue;
            }

            if (c == '\\')
            {
                if (this.TryConsumeEscape(literal))
                {
                    continue;
                }

                _ = literal.Append('\\');
                this._index++;
                continue;
            }

            _ = literal.Append(c);
            this._index++;
        }

        FlushLiteral(literal, parts);

        return parts.Count == 0
            ? new LiteralNode(string.Empty)
            : parts.Count == 1
            ? parts[0]
            : new CompositeNode(parts.AsReadOnly());
    }

    /// <summary>
    /// Consumes a recognized backslash escape sequence (<c>\{</c>, <c>\}</c>, <c>\"</c>, <c>\\</c>) at the current cursor.
    /// </summary>
    /// <param name="target">The destination string builder.</param>
    /// <returns><see langword="true"/> if an escape sequence was consumed; otherwise, <see langword="false"/>.</returns>
    private bool TryConsumeEscape(StringBuilder target)
    {
        if (this._index + 1 >= this._input.Length)
        {
            return false;
        }

        char next = this._input[this._index + 1];
        if (next is '{' or '}' or '"' or '\\')
        {
            _ = target.Append(next);
            this._index += 2;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Advances the cursor past any whitespace characters.
    /// </summary>
    private void SkipWhitespace()
    {
        while (this._index < this._input.Length && char.IsWhiteSpace(this._input[this._index]))
        {
            this._index++;
        }
    }

    /// <summary>
    /// Flushes accumulated text from <paramref name="literal"/> into <paramref name="nodes"/> if non-empty.
    /// </summary>
    /// <param name="literal">The pending literal character buffer.</param>
    /// <param name="nodes">The destination AST node list.</param>
    private static void FlushLiteral(StringBuilder literal, List<TemplateNode> nodes)
    {
        if (literal.Length == 0)
        {
            return;
        }

        AppendMergedPart(nodes, new LiteralNode(literal.ToString()));
        _ = literal.Clear();
    }

    /// <summary>
    /// Appends a child node while coalescing adjacent <see cref="LiteralNode"/> instances.
    /// </summary>
    /// <param name="nodes">The destination node list.</param>
    /// <param name="node">The node to append.</param>
    private static void AppendMergedPart(List<TemplateNode> nodes, TemplateNode node)
    {
        if (node is LiteralNode newLiteral && nodes.Count > 0 && nodes[^1] is LiteralNode prevLiteral)
        {
            nodes[^1] = new LiteralNode(prevLiteral.Text + newLiteral.Text);
            return;
        }

        nodes.Add(node);
    }
}

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

using FluentAssertions;
using StreamActions.Core.Templates;
using System.Text;
using Xunit;

namespace StreamActions.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TemplateParser"/>.
/// </summary>
public sealed class TemplateParserTests
{
    #region Parse

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Hello chat!", "Hello chat!")]
    [InlineData("Literal \\{braces\\} and \\\\backslash", "Literal {braces} and \\backslash")]
    [InlineData("Keep \"quotes\" in plain text", "Keep \"quotes\" in plain text")]
    [Trait("Member", "Parse")]
    public void ParsePlainTextAndEscapesProducesValidLiteralNode(string? template, string expectedText)
    {
        TemplateParseResult result = TemplateParser.Parse(template);

        _ = result.IsValid.Should().BeTrue();
        _ = result.HasErrors.Should().BeFalse();
        _ = result.Diagnostics.Should().BeEmpty();
        LiteralNode literal = result.Root.Should().BeOfType<LiteralNode>().Subject;
        _ = literal.Text.Should().Be(expectedText);
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseQuotedArgumentWithInterpolationAndEscapedQuotesProducesCompositeNode()
    {
        TemplateParseResult result = TemplateParser.Parse("{if {1} \"{sender} throws \\\"it\\\" at {1}!\"}");

        _ = result.IsValid.Should().BeTrue();
        TagNode ifTag = result.Root.Should().BeOfType<TagNode>().Subject;
        _ = ifTag.TagName.Should().Be("if");
        _ = ifTag.Arguments.Should().HaveCount(2);
        _ = ifTag.Arguments[0].Should().BeOfType<TagNode>().Which.TagName.Should().Be("1");

        CompositeNode quoted = ifTag.Arguments[1].Should().BeOfType<CompositeNode>().Subject;
        _ = quoted.Children.Should().HaveCount(4);
        _ = quoted.Children[0].Should().BeOfType<TagNode>().Which.TagName.Should().Be("sender");
        _ = quoted.Children[1].Should().BeOfType<LiteralNode>().Which.Text.Should().Be(" throws \"it\" at ");
        _ = quoted.Children[2].Should().BeOfType<TagNode>().Which.TagName.Should().Be("1");
        _ = quoted.Children[3].Should().BeOfType<LiteralNode>().Which.Text.Should().Be("!");
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseNestedTagsAndUnquotedInlineTagArgumentsBuildsExpectedAst()
    {
        TemplateParseResult result = TemplateParser.Parse("{game {or {1} {channel}} https://example.com/{1}}");

        _ = result.IsValid.Should().BeTrue();
        TagNode gameTag = result.Root.Should().BeOfType<TagNode>().Subject;
        _ = gameTag.TagName.Should().Be("game");
        _ = gameTag.Arguments.Should().HaveCount(2);

        TagNode orTag = gameTag.Arguments[0].Should().BeOfType<TagNode>().Subject;
        _ = orTag.TagName.Should().Be("or");
        _ = orTag.Arguments.Should().HaveCount(2);
        _ = orTag.Arguments[0].Should().BeOfType<TagNode>().Which.TagName.Should().Be("1");
        _ = orTag.Arguments[1].Should().BeOfType<TagNode>().Which.TagName.Should().Be("channel");

        CompositeNode inlineArg = gameTag.Arguments[1].Should().BeOfType<CompositeNode>().Subject;
        _ = inlineArg.Children.Should().HaveCount(2);
        _ = inlineArg.Children[0].Should().BeOfType<LiteralNode>().Which.Text.Should().Be("https://example.com/");
        _ = inlineArg.Children[1].Should().BeOfType<TagNode>().Which.TagName.Should().Be("1");
    }

    [Theory]
    [InlineData("{sender", TemplateDiagnosticCode.UnclosedBrace)]
    [InlineData("{if {1} \"unclosed", TemplateDiagnosticCode.UnclosedQuote)]
    [InlineData("hello } world", TemplateDiagnosticCode.UnexpectedClosingBrace)]
    [InlineData("{}", TemplateDiagnosticCode.EmptyTag)]
    [InlineData("{   }", TemplateDiagnosticCode.EmptyTag)]
    [Trait("Member", "Parse")]
    public void ParseSyntaxErrorsEmitsStructuredErrorDiagnostics(string template, TemplateDiagnosticCode expectedCode)
    {
        TemplateParseResult result = TemplateParser.Parse(template);

        _ = result.IsValid.Should().BeFalse();
        _ = result.HasErrors.Should().BeTrue();
        _ = result.Diagnostics.Should().Contain(d =>
            d.Severity == TemplateDiagnosticSeverity.Error && d.Code == expectedCode);
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseUnknownTagWithRegistryEmitsWarningDiagnostic()
    {
        TagRegistry registry = CoreTagRegistryFactory.CreateDefaultRegistry();
        TemplateParseResult result = TemplateParser.Parse("Hello {sneder}!", registry);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Diagnostics.Should().ContainSingle(d =>
            d.Severity == TemplateDiagnosticSeverity.Warning
            && d.Code == TemplateDiagnosticCode.UnknownTag
            && d.Message.Contains("sneder", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseNestingDepthAt16SucceedsAndDepthAt17FailsWithDiagnostic()
    {
        string depth16 = BuildNestedOrTemplate(TemplateExecutionLimits.MaxAstNestingDepth);
        string depth17 = BuildNestedOrTemplate(TemplateExecutionLimits.MaxAstNestingDepth + 1);
        TagRegistry registry = CoreTagRegistryFactory.CreateDefaultRegistry();

        TemplateParseResult validResult = TemplateParser.Parse(depth16, registry);
        TemplateParseResult invalidResult = TemplateParser.Parse(depth17, registry);

        _ = validResult.IsValid.Should().BeTrue();
        _ = validResult.Diagnostics.Should().BeEmpty();

        _ = invalidResult.IsValid.Should().BeFalse();
        _ = invalidResult.Diagnostics.Should().ContainSingle(d =>
            d.Severity == TemplateDiagnosticSeverity.Error
            && d.Code == TemplateDiagnosticCode.MaxNestingDepthExceeded);
    }

    /// <summary>
    /// Builds a nested <c>{or ...}</c> template of exact depth <paramref name="depth"/>.
    /// </summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The nested template string.</returns>
    private static string BuildNestedOrTemplate(int depth)
    {
        StringBuilder builder = new();
        for (int i = 0; i < depth - 1; i++)
        {
            _ = builder.Append("{or ");
        }

        _ = builder.Append("{sender}");
        for (int i = 0; i < depth - 1; i++)
        {
            _ = builder.Append('}');
        }

        return builder.ToString();
    }

    #endregion
}

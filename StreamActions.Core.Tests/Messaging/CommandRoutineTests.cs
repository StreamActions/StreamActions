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
using StreamActions.Core.Commands;
using Xunit;

namespace StreamActions.Core.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="CommandRoutine"/>.
/// </summary>
public sealed class CommandRoutineTests
{
    #region Parse

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseTier1JsonDeserializesChatSendSteps()
    {
        const string json = """{"steps":[{"$type":"chat.send","message":"Hello $(user)!"},{"$type":"chat.send","message":"Welcome!"}]}""";

        CommandRoutine routine = CommandRoutine.Parse(json);

        _ = routine.Steps.Should().HaveCount(2);
        _ = routine.Steps[0].Should().BeOfType<ChatSendActionStep>()
            .Which.Message.Should().Be("Hello $(user)!");
        _ = routine.GetChatMessages().Should().Equal("Hello $(user)!", "Welcome!");
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseTier1JsonSupportsOutOfOrderMetadataAndUnknownActionBlockSteps()
    {
        const string json = """{"steps":[{"message":"First","$type":"chat.send"},{"$type":"action_block.delay","durationMs":1000}]}""";

        CommandRoutine routine = CommandRoutine.Parse(json);

        _ = routine.Steps.Should().HaveCount(2);
        _ = routine.Steps[0].Should().BeOfType<ChatSendActionStep>().Which.Message.Should().Be("First");
        _ = routine.Steps[1].Should().BeOfType<UnknownCommandRoutineStep>().Which.StepType.Should().Be("action_block.delay");
        _ = routine.GetChatMessages().Should().Equal("First");
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParsePlainTextFallsBackToSingleChatSendStep()
    {
        CommandRoutine routine = CommandRoutine.Parse("Check out our Discord at https://example.com!");

        _ = routine.Steps.Should().ContainSingle()
            .Which.Should().BeOfType<ChatSendActionStep>()
            .Which.Message.Should().Be("Check out our Discord at https://example.com!");
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void ParseInvalidJsonStartingWithBraceFallsBackToPlainText()
    {
        CommandRoutine routine = CommandRoutine.Parse("{not valid json}");

        _ = routine.GetChatMessages().Should().Equal("{not valid json}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Member", "Parse")]
    public void ParseBlankInputReturnsEmptyRoutine(string? input)
    {
        CommandRoutine routine = CommandRoutine.Parse(input);

        _ = routine.Steps.Should().BeEmpty();
        _ = routine.GetChatMessages().Should().BeEmpty();
    }

    #endregion

    #region ToJson

    [Fact]
    [Trait("Member", "ToJson")]
    public void ToJsonSerializesCompatibleWithTier1SchemaAndRoundTrips()
    {
        CommandRoutine original = CommandRoutine.FromPlainText("Stream is live!");

        string json = original.ToJson();
        CommandRoutine roundTripped = CommandRoutine.Parse(json);

        _ = json.Should().Contain("\"$type\":\"chat.send\"");
        _ = json.Should().Contain("\"message\":\"Stream is live!\"");
        _ = roundTripped.GetChatMessages().Should().Equal("Stream is live!");
    }

    #endregion
}

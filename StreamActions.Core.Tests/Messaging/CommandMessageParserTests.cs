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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;
using Xunit;

namespace StreamActions.Core.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="CommandMessageParser"/>.
/// </summary>
public sealed class CommandMessageParserTests
{
    #region TryParse

    [Theory]
    [InlineData("!ping", "!", "ping", new string[0], "")]
    [InlineData("  !SO @Streamer  ", "!", "so", new[] { "@Streamer" }, "@Streamer")]
    [InlineData("!Counter Set \"Dark Souls III\" 42", "!", "counter", new[] { "Set", "Dark Souls III", "42" }, "Set \"Dark Souls III\" 42")]
    [InlineData("!echo \"say \\\"hello\\\"\" world", "!", "echo", new[] { "say \"hello\"", "world" }, "\"say \\\"hello\\\"\" world")]
    [InlineData("!quote 'First Quote' second", "!", "quote", new[] { "First Quote", "second" }, "'First Quote' second")]
    [InlineData("!say don't stop 'Tis great", "!", "say", new[] { "don't", "stop", "'Tis", "great" }, "don't stop 'Tis great")]
    [InlineData("!unclosed \"never ending arg", "!", "unclosed", new[] { "never ending arg" }, "\"never ending arg")]
    [InlineData("!empty \"\" second", "!", "empty", new[] { "", "second" }, "\"\" second")]
    [Trait("Member", "TryParse")]
    public void TryParseValidMessagesNormalizesTriggerAndParsesArguments(
        string rawText,
        string expectedPrefix,
        string expectedTrigger,
        string[] expectedArgs,
        string expectedRawArgs)
    {
        bool success = CommandMessageParser.TryParse(rawText, out ParsedCommandMessage? parsed);

        _ = success.Should().BeTrue();
        _ = parsed.Should().NotBeNull();
        _ = parsed!.Prefix.Should().Be(expectedPrefix);
        _ = parsed.Trigger.Should().Be(expectedTrigger);
        _ = parsed.Arguments.Should().Equal(expectedArgs);
        _ = parsed.RawArguments.Should().Be(expectedRawArgs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("!")]
    [InlineData("! hello")]
    [InlineData("!   ping")]
    [Trait("Member", "TryParse")]
    public void TryParseInvalidOrNonCommandMessagesReturnsFalse(string? rawText)
    {
        bool success = CommandMessageParser.TryParse(rawText, out ParsedCommandMessage? parsed);

        _ = success.Should().BeFalse();
        _ = parsed.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "TryParse")]
    public void TryParseWithMultiplePrefixesMatchesLongestPrefixFirst()
    {
        string[] prefixes = ["!", "!!", "?"];

        bool successDouble = CommandMessageParser.TryParse("!!MOD ban user1", prefixes, out ParsedCommandMessage? doubleParsed);
        bool successQuestion = CommandMessageParser.TryParse("?uptime", prefixes, out ParsedCommandMessage? questionParsed);

        _ = successDouble.Should().BeTrue();
        _ = doubleParsed!.Prefix.Should().Be("!!");
        _ = doubleParsed.Trigger.Should().Be("mod");
        _ = doubleParsed.Arguments.Should().Equal("ban", "user1");

        _ = successQuestion.Should().BeTrue();
        _ = questionParsed!.Prefix.Should().Be("?");
        _ = questionParsed.Trigger.Should().Be("uptime");
    }

    #endregion

    #region TryParseChatCommand

    [Fact]
    [Trait("Member", "TryParseChatCommand")]
    public void TryParseChatCommandPreservesEnvelopeChannelAndMetadata()
    {
        Guid workspaceId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 11, 21, 15, 0, 0, TimeSpan.Zero);
        ChatUser sender = new("twitch", "42", "viewer", "Viewer", PlatformUserDesignations.Subscriber);
        ChatChannel channel = new(workspaceId, "twitch", "chan-42", "streamer");
        Dictionary<string, string> tags = new(StringComparer.Ordinal) { ["color"] = "#FF0000" };
        ChatMessage message = new(
            workspaceId,
            "twitch",
            "msg-99",
            sender,
            channel,
            "!Death +1",
            now,
            ReplyToMessageId: "msg-98",
            IsAction: false,
            Metadata: tags);

        bool success = CommandMessageParser.TryParseChatCommand(message, out IChatCommand? command);

        _ = success.Should().BeTrue();
        _ = command.Should().NotBeNull();
        _ = command!.WorkspaceId.Should().Be(workspaceId);
        _ = command.Channel.Should().Be(channel);
        _ = command.Sender.Should().Be(sender);
        _ = command.ReplyToMessageId.Should().Be("msg-98");
        _ = command.Metadata.Should().ContainKey("color");
        _ = command.Trigger.Should().Be("death");
        _ = command.Arguments.Should().Equal("+1");
    }

    #endregion

    #region TryParseWhisperCommand

    [Fact]
    [Trait("Member", "TryParseWhisperCommand")]
    public void TryParseWhisperCommandPreservesWhisperEnvelope()
    {
        Guid workspaceId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 11, 21, 15, 0, 0, TimeSpan.Zero);
        ChatUser sender = new("twitch", "77", "moduser", "ModUser", PlatformUserDesignations.Moderator);
        WhisperMessage whisper = new(workspaceId, "twitch", "w-5", sender, "!Permit \"cool user\"", now, "bot-99");

        bool success = CommandMessageParser.TryParseWhisperCommand(whisper, out IWhisperCommand? command);

        _ = success.Should().BeTrue();
        _ = command.Should().NotBeNull();
        _ = command!.TargetPlatformUserId.Should().Be("bot-99");
        _ = command.Trigger.Should().Be("permit");
        _ = command.Arguments.Should().Equal("cool user");
    }

    #endregion
}

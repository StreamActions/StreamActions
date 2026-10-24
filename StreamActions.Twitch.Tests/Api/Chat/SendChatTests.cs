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
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="SendChat"/> class and <see cref="SendChatParameters"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class SendChatTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SendChatTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public SendChatTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region SendChatMessage

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        SendChatParameters parameters = new()
        {
            BroadcasterId = "123",
            SenderId = "456",
            Message = "hello"
        };

        Func<Task> action = async () => await SendChat.SendChatMessage(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserWriteChat);
        Func<Task> action = async () => await SendChat.SendChatMessage(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="ArgumentNullException"/> when required fields are missing.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenRequiredFieldsNullOrWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserWriteChat);

        SendChatParameters paramNoBroadcaster = new() { BroadcasterId = "", SenderId = "456", Message = "hi" };
        Func<Task> action1 = async () => await SendChat.SendChatMessage(session, paramNoBroadcaster).ConfigureAwait(true);
        _ = (await action1.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("BroadcasterId");

        SendChatParameters paramNoSender = new() { BroadcasterId = "123", SenderId = " ", Message = "hi" };
        Func<Task> action2 = async () => await SendChat.SendChatMessage(session, paramNoSender).ConfigureAwait(true);
        _ = (await action2.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("SenderId");

        SendChatParameters paramNoMsg = new() { BroadcasterId = "123", SenderId = "456", Message = "" };
        Func<Task> action3 = async () => await SendChat.SendChatMessage(session, paramNoMsg).ConfigureAwait(true);
        _ = (await action3.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Message");
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="ArgumentOutOfRangeException"/> when message length exceeds 500.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenMessageExceeds500Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserWriteChat);
        SendChatParameters parameters = new()
        {
            BroadcasterId = "123",
            SenderId = "456",
            Message = new string('x', 501)
        };

        Func<Task> action = async () => await SendChat.SendChatMessage(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Message");
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="ArgumentOutOfRangeException"/> when user token has ForSourceOnly set.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenUserTokenAndForSourceOnlySet_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserWriteChat);
        SendChatParameters parameters = new()
        {
            BroadcasterId = "123",
            SenderId = "456",
            Message = "hello",
            ForSourceOnly = true
        };

        Func<Task> action = async () => await SendChat.SendChatMessage(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("ForSourceOnly");
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="ArgumentOutOfRangeException"/> when Pin is combined with ReplyParentMessageId.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenPinCombinedWithReplyParentMessageId_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserWriteChat, Scope.ModeratorManageChatMessages);
        SendChatParameters parameters = new()
        {
            BroadcasterId = "123",
            SenderId = "456",
            Message = "hello",
            Pin = true,
            ReplyParentMessageId = "reply123"
        };

        Func<Task> action = async () => await SendChat.SendChatMessage(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Pin");
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        SendChatParameters parameters = new()
        {
            BroadcasterId = "123",
            SenderId = "456",
            Message = "hello"
        };

        Func<Task> action = async () => await SendChat.SendChatMessage(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="SendChat.SendChatMessage"/> sends POST request and maps response.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatMessage")]
    public async Task SendChatMessage_WhenValidParameters_SendsPostRequestAndReturnsResponse()
    {
        this._fixture.RegisterJsonResponse("POST", "/helix/chat/messages", new
        {
            data = new[]
            {
                new
                {
                    message_id = "msg999",
                    is_sent = true
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserWriteChat);
        SendChatParameters parameters = new()
        {
            BroadcasterId = "12345",
            SenderId = "67890",
            Message = "Hello from test!"
        };

        ResponseData<SendChat>? response = await SendChat.SendChatMessage(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        SendChat result = response.Data[0];
        _ = result.MessageId.Should().Be("msg999");
        _ = result.IsSent.Should().BeTrue();
        _ = result.DropReason.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/chat/messages");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"12345\"");
        _ = request.Body.Should().Contain("\"sender_id\":\"67890\"");
        _ = request.Body.Should().Contain("\"message\":\"Hello from test!\"");
    }

    /// <summary>
    /// Tests that <see cref="SendChatParameters.ConvertMessageToSlashMe"/> converts a message into a slash-me format.
    /// </summary>
    [Fact]
    [Trait("Member", "ConvertMessageToSlashMe")]
    public void ConvertMessageToSlashMe_WhenCalled_FormatsActionMessage()
    {
        string converted = SendChatParameters.ConvertMessageToSlashMe("waves hello");
        _ = converted.Should().Be("\u0001ACTION waves hello\u0001");

        string convertedWithPrefix = SendChatParameters.ConvertMessageToSlashMe("/me jumps");
        _ = convertedWithPrefix.Should().Be("\u0001ACTION jumps\u0001");
    }

    #endregion SendChatMessage
}

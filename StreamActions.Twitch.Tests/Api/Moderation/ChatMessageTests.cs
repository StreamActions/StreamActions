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

using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="ChatMessage"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChatMessageTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChatMessageTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region DeleteChatMessages

    [Fact]
    [Trait("Member", "DeleteChatMessages")]
    public async Task DeleteChatMessages_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChatMessage.DeleteChatMessages(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "DeleteChatMessages")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteChatMessages_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        Func<Task> action = async () => await ChatMessage.DeleteChatMessages(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "DeleteChatMessages")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteChatMessages_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        Func<Task> action = async () => await ChatMessage.DeleteChatMessages(session, "1234", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "DeleteChatMessages")]
    public async Task DeleteChatMessages_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await ChatMessage.DeleteChatMessages(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "DeleteChatMessages")]
    public async Task DeleteChatMessages_WhenMessageIdProvided_DeletesSingleMessage()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/chat", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        JsonApiResponse? response = await ChatMessage.DeleteChatMessages(session, "11111", "44444", "abc-123-def").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/moderation/chat");
        _ = request.QueryString.Should().Contain("broadcaster_id=11111");
        _ = request.QueryString.Should().Contain("moderator_id=44444");
        _ = request.QueryString.Should().Contain("message_id=abc-123-def");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ChatMessage.DeleteChatMessages"/> throws <see cref="InvalidOperationException"/> when the token has an empty OAuth string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteChatMessages")]
    public async Task DeleteChatMessages_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorManageChatMessages]
            }
        };

        Func<Task> action = async () => await ChatMessage.DeleteChatMessages(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ChatMessage.DeleteChatMessages"/> succeeds with an App token.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteChatMessages")]
    public async Task DeleteChatMessages_WhenAppToken_DeletesMessages()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/chat", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModeratorManageChatMessages);
        JsonApiResponse? response = await ChatMessage.DeleteChatMessages(session, "11111", "44444").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ChatMessage.DeleteChatMessages"/> omits message_id from query parameters when messageId is whitespace.
    /// </para>
    /// </summary>
    /// <param name="whitespaceMessageId">The whitespace message ID to test.</param>
    [Theory]
    [Trait("Member", "DeleteChatMessages")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteChatMessages_WhenMessageIdIsWhitespace_OmitsMessageIdFromQueryString(string whitespaceMessageId)
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/chat", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        JsonApiResponse? response = await ChatMessage.DeleteChatMessages(session, "11111", "44444", whitespaceMessageId).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().NotContain("message_id");
    }

    [Fact]
    [Trait("Member", "DeleteChatMessages")]
    public async Task DeleteChatMessages_WhenMessageIdNotProvided_DeletesAllMessages()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/chat", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        JsonApiResponse? response = await ChatMessage.DeleteChatMessages(session, "11111", "44444").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/moderation/chat");
        _ = request.QueryString.Should().Contain("broadcaster_id=11111");
        _ = request.QueryString.Should().Contain("moderator_id=44444");
        _ = request.QueryString.Should().NotContain("message_id");
    }

    #endregion DeleteChatMessages
}

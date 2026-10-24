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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="PinnedChatMessage"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class PinnedChatMessageTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PinnedChatMessageTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public PinnedChatMessageTests(TwitchKestrelServerFixture fixture)
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

    #region GetPinnedChatMessages

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.GetPinnedChatMessages"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetPinnedChatMessages")]
    public async Task GetPinnedChatMessages_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await PinnedChatMessage.GetPinnedChatMessages(null!, "12345", "67890").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.GetPinnedChatMessages"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetPinnedChatMessages")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPinnedChatMessages_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatMessages);
        Func<Task> action = async () => await PinnedChatMessage.GetPinnedChatMessages(session, invalidId!, "67890").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.GetPinnedChatMessages"/> throws <see cref="ArgumentNullException"/> when moderatorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid moderator ID.</param>
    [Theory]
    [Trait("Member", "GetPinnedChatMessages")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPinnedChatMessages_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatMessages);
        Func<Task> action = async () => await PinnedChatMessage.GetPinnedChatMessages(session, "12345", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.GetPinnedChatMessages"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetPinnedChatMessages")]
    public async Task GetPinnedChatMessages_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await PinnedChatMessage.GetPinnedChatMessages(session, "12345", "67890").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.GetPinnedChatMessages"/> retrieves pinned messages and maps all properties.
    /// </summary>
    [Fact]
    [Trait("Member", "GetPinnedChatMessages")]
    public async Task GetPinnedChatMessages_WhenCalledWithValidParameters_ReturnsPinnedMessages()
    {
        DateTime startsAt = DateTime.UtcNow;
        DateTime endsAt = startsAt.AddSeconds(60);

        this._fixture.RegisterJsonResponse("GET", "/helix/chat/pins", new
        {
            data = new[]
            {
                new
                {
                    message_id = "msg123",
                    broadcaster_id = "12345",
                    sender_user_id = "22222",
                    sender_user_login = "sender",
                    sender_user_name = "Sender",
                    pinned_by_user_id = "67890",
                    pinned_by_user_login = "mod",
                    pinned_by_user_name = "Mod",
                    message = new
                    {
                        text = "Important announcement!"
                    },
                    starts_at = startsAt,
                    ends_at = endsAt,
                    updated_at = startsAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatMessages);
        ResponseData<PinnedChatMessage>? response = await PinnedChatMessage.GetPinnedChatMessages(session, "12345", "67890").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        PinnedChatMessage pin = response.Data[0];
        _ = pin.MessageId.Should().Be("msg123");
        _ = pin.BroadcasterId.Should().Be("12345");
        _ = pin.SenderUserId.Should().Be("22222");
        _ = pin.SenderUserLogin.Should().Be("sender");
        _ = pin.PinnedByUserId.Should().Be("67890");
        _ = pin.Message.Should().NotBeNull();
        _ = pin.Message!.Text.Should().Be("Important announcement!");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/pins");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
    }

    #endregion GetPinnedChatMessages

    #region PinChatMessage

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.PinChatMessage"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "PinChatMessage")]
    public async Task PinChatMessage_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await PinnedChatMessage.PinChatMessage(null!, "12345", "67890", "msg123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.PinChatMessage"/> throws <see cref="ArgumentNullException"/> when messageId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid message ID.</param>
    [Theory]
    [Trait("Member", "PinChatMessage")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PinChatMessage_WhenMessageIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        Func<Task> action = async () => await PinnedChatMessage.PinChatMessage(session, "12345", "67890", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("messageId");
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.PinChatMessage"/> sends PUT request and returns 204 No Content.
    /// </summary>
    [Fact]
    [Trait("Member", "PinChatMessage")]
    public async Task PinChatMessage_WhenCalledWithValidParameters_SendsPutRequest()
    {
        this._fixture.RegisterStatusCode("PUT", "/helix/chat/pins", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        JsonApiResponse? response = await PinnedChatMessage.PinChatMessage(session, "12345", "67890", "msg123", durationSeconds: 60).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/chat/pins");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
        _ = request.Query["message_id"].ToString().Should().Be("msg123");
        _ = request.Query["duration_seconds"].ToString().Should().Be("60");
    }

    #endregion PinChatMessage

    #region UnpinChatMessage

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.UnpinChatMessage"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UnpinChatMessage")]
    public async Task UnpinChatMessage_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await PinnedChatMessage.UnpinChatMessage(null!, "12345", "67890", "msg123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="PinnedChatMessage.UnpinChatMessage"/> sends DELETE request and returns 204 No Content.
    /// </summary>
    [Fact]
    [Trait("Member", "UnpinChatMessage")]
    public async Task UnpinChatMessage_WhenCalledWithValidParameters_SendsDeleteRequest()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/chat/pins", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatMessages);
        JsonApiResponse? response = await PinnedChatMessage.UnpinChatMessage(session, "12345", "67890", "msg123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/chat/pins");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
        _ = request.Query["message_id"].ToString().Should().Be("msg123");
    }

    #endregion UnpinChatMessage
}

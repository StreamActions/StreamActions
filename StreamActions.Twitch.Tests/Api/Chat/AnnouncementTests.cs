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
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="Announcement"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class AnnouncementTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnouncementTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public AnnouncementTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region SendChatAnnouncement

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Announcement announcement = new() { Message = "Hello world" };
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(null!, "123", "456", announcement).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "SendChatAnnouncement")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendChatAnnouncement_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new() { Message = "Hello world" };
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(session, invalidId!, "456", announcement).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="ArgumentNullException"/> when moderatorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid moderator ID to test.</param>
    [Theory]
    [Trait("Member", "SendChatAnnouncement")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendChatAnnouncement_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new() { Message = "Hello world" };
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(session, "123", invalidId!, announcement).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="ArgumentNullException"/> when message is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenMessageIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(session, "123", "456", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("message");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="ArgumentNullException"/> when message string is whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenMessageStringIsWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new() { Message = "   " };
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(session, "123", "456", announcement).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("message");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="ArgumentOutOfRangeException"/> when using user token and ForSourceOnly is specified.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenUserTokenAndForSourceOnlyNotNull_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new() { Message = "Hello world", ForSourceOnly = true };
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(session, "123", "456", announcement).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("ForSourceOnly");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Announcement announcement = new() { Message = "Hello world" };
        Func<Task> action = async () => await Announcement.SendChatAnnouncement(session, "123", "456", announcement).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> submits the correct request and returns 204 No Content.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenValidParameters_SendsPostRequestAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/chat/announcements", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new()
        {
            Message = "Hello, world!",
            Color = Announcement.AnnouncementColor.Blue
        };

        JsonApiResponse? response = await Announcement.SendChatAnnouncement(session, "12345", "67890", announcement).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/chat/announcements");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
        _ = request.Body.Should().Contain("\"message\":\"Hello, world!\"");
        _ = request.Body.Should().Contain("\"color\":\"blue\"");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> omits the color property from the request body when color is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenColorIsNull_OmitsColorFromBody()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/chat/announcements", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new()
        {
            Message = "Hello without color"
        };

        JsonApiResponse? response = await Announcement.SendChatAnnouncement(session, "12345", "67890", announcement).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("\"message\":\"Hello without color\"");
        _ = request.Body.Should().NotContain("\"color\"");
    }

    /// <summary>
    /// Tests that <see cref="Announcement.SendChatAnnouncement"/> submits for_source_only when using an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "SendChatAnnouncement")]
    public async Task SendChatAnnouncement_WhenAppTokenAndForSourceOnlyIsTrue_SendsForSourceOnlyInBody()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/chat/announcements", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModeratorManageAnnouncements);
        Announcement announcement = new()
        {
            Message = "App announcement",
            ForSourceOnly = true
        };

        JsonApiResponse? response = await Announcement.SendChatAnnouncement(session, "12345", "67890", announcement).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("\"for_source_only\":true");
        _ = request.Body.Should().Contain("\"message\":\"App announcement\"");
    }

    #endregion SendChatAnnouncement
}

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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.Api.Streams.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Streams;

/// <summary>
/// Provides unit tests for stream markers (<see cref="StreamMarker"/> and <see cref="UserMarkers"/>).
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class StreamMarkerTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamMarkerTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public StreamMarkerTests(TwitchKestrelServerFixture fixture)
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

    #region CreateStreamMarker

    /// <summary>
    /// Tests that <see cref="StreamMarker.CreateStreamMarker"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateStreamMarker")]
    public async Task CreateStreamMarker_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        CreateStreamMarkerParameters parameters = new() { UserId = "123", Description = "test" };
        Func<Task> action = async () => await StreamMarker.CreateStreamMarker(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="StreamMarker.CreateStreamMarker"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateStreamMarker")]
    public async Task CreateStreamMarker_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        Func<Task> action = async () => await StreamMarker.CreateStreamMarker(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="StreamMarker.CreateStreamMarker"/> throws <see cref="ArgumentOutOfRangeException"/> when description exceeds 140 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateStreamMarker")]
    public async Task CreateStreamMarker_WhenDescriptionExceeds140Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        CreateStreamMarkerParameters parameters = new()
        {
            UserId = "123",
            Description = new string('x', 141)
        };

        Func<Task> action = async () => await StreamMarker.CreateStreamMarker(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="StreamMarker.CreateStreamMarker"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateStreamMarker")]
    public async Task CreateStreamMarker_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        CreateStreamMarkerParameters parameters = new() { UserId = "123", Description = "test" };

        Func<Task> action = async () => await StreamMarker.CreateStreamMarker(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="StreamMarker.CreateStreamMarker"/> throws <see cref="TwitchScopeMissingException"/> when ChannelManageBroadcast scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateStreamMarker")]
    public async Task CreateStreamMarker_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        CreateStreamMarkerParameters parameters = new() { UserId = "123", Description = "test" };

        Func<Task> action = async () => await StreamMarker.CreateStreamMarker(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="StreamMarker.CreateStreamMarker"/> creates a marker on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateStreamMarker")]
    public async Task CreateStreamMarker_WhenRequestSucceeds_ReturnsCreatedMarker()
    {
        this._fixture.RegisterEndpoint("/helix/streams/markers", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""106b80bd-88f3-4c10-994f-4d9e9b04f3bf"",
      ""created_at"": ""2018-08-20T20:10:03Z"",
      ""position_seconds"": 244,
      ""description"": ""hello world""
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        CreateStreamMarkerParameters parameters = new()
        {
            UserId = "123",
            Description = "hello world"
        };

        ResponseData<StreamMarker>? response = await StreamMarker.CreateStreamMarker(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        StreamMarker marker = response.Data[0];
        _ = marker.Id.Should().Be("106b80bd-88f3-4c10-994f-4d9e9b04f3bf");
        _ = marker.CreatedAt.Should().Be(DateTime.Parse("2018-08-20T20:10:03Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = marker.PositionSeconds.Should().Be(244);
        _ = marker.Position.Should().Be(TimeSpan.FromSeconds(244));
        _ = marker.Description.Should().Be("hello world");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/streams/markers");
        _ = request.Body.Should().Contain("\"user_id\":\"123\"");
        _ = request.Body.Should().Contain("\"description\":\"hello world\"");
    }

    #endregion CreateStreamMarker

    #region GetStreamMarkers

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamMarkers")]
    public async Task GetStreamMarkers_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await UserMarkers.GetStreamMarkers(null!, userId: "123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> throws <see cref="ArgumentNullException"/> when both userId and videoId are null or whitespace.
    /// </summary>
    [Theory]
    [Trait("Member", "GetStreamMarkers")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task GetStreamMarkers_WhenBothUserIdAndVideoIdAreNullOrWhitespace_ThrowsArgumentNullException(string? userId, string? videoId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBroadcast);
        Func<Task> action = async () => await UserMarkers.GetStreamMarkers(session, userId, videoId).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId,videoId");
    }

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> throws <see cref="ArgumentOutOfRangeException"/> when both userId and videoId are specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamMarkers")]
    public async Task GetStreamMarkers_WhenBothUserIdAndVideoIdAreSpecified_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBroadcast);
        Func<Task> action = async () => await UserMarkers.GetStreamMarkers(session, userId: "123", videoId: "456").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userId,videoId");
    }

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> throws <see cref="InvalidOperationException"/> when both before and after are specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamMarkers")]
    public async Task GetStreamMarkers_WhenBothBeforeAndAfterAreSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBroadcast);
        Func<Task> action = async () => await UserMarkers.GetStreamMarkers(session, userId: "123", before: "cur1", after: "cur2").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamMarkers")]
    public async Task GetStreamMarkers_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await UserMarkers.GetStreamMarkers(session, userId: "123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> throws <see cref="TwitchScopeMissingException"/> when required scopes are missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamMarkers")]
    public async Task GetStreamMarkers_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await UserMarkers.GetStreamMarkers(session, userId: "123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserMarkers.GetStreamMarkers"/> returns markers on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamMarkers")]
    public async Task GetStreamMarkers_WhenRequestSucceeds_ReturnsMarkers()
    {
        this._fixture.RegisterEndpoint("/helix/streams/markers", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""user_id"": ""123"",
      ""user_login"": ""testuser"",
      ""user_name"": ""TestUser"",
      ""videos"": [
        {
          ""video_id"": ""456"",
          ""markers"": [
            {
              ""id"": ""marker1"",
              ""created_at"": ""2018-08-20T20:10:03Z"",
              ""position_seconds"": 244,
              ""description"": ""cool moment"",
              ""URL"": ""https://twitch.tv/videos/456?t=0h4m4s""
            }
          ]
        }
      ]
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursorXYZ""
  }
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBroadcast);
        ResponseData<UserMarkers>? response = await UserMarkers.GetStreamMarkers(session, userId: "123", first: 20, after: "cursorXYZ").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        UserMarkers userMarkers = response.Data[0];
        _ = userMarkers.UserId.Should().Be("123");
        _ = userMarkers.UserLogin.Should().Be("testuser");
        _ = userMarkers.UserName.Should().Be("TestUser");
        _ = userMarkers.Videos.Should().NotBeNull();

        List<VideoMarkers> videos = userMarkers.Videos!.ToList();
        _ = videos.Count.Should().Be(1);
        _ = videos[0].VideoId.Should().Be("456");

        List<StreamMarker> markers = videos[0].Markers!.ToList();
        _ = markers.Count.Should().Be(1);
        _ = markers[0].Id.Should().Be("marker1");
        _ = markers[0].CreatedAt.Should().Be(DateTime.Parse("2018-08-20T20:10:03Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = markers[0].PositionSeconds.Should().Be(244);
        _ = markers[0].Position.Should().Be(TimeSpan.FromSeconds(244));
        _ = markers[0].Description.Should().Be("cool moment");
        _ = markers[0].Url.Should().Be(new Uri("https://twitch.tv/videos/456?t=0h4m4s"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/streams/markers");
        _ = request.QueryString.Should().Contain("user_id=123");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=cursorXYZ");
    }

    #endregion GetStreamMarkers
}

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
using StreamActions.Twitch.Api.Videos;
using StreamActions.Twitch.Api.Videos.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Videos;

/// <summary>
/// Provides unit tests for the <see cref="Video"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class VideoTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public VideoTests(TwitchKestrelServerFixture fixture)
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

    #region GetVideos

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Video.GetVideos(null!, userId: "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> throws <see cref="ArgumentNullException"/> when no identifiers are specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenNoIdentifiersProvided_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Video.GetVideos(session).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id,userId,gameId");
    }

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> throws <see cref="ArgumentOutOfRangeException"/> when mutually exclusive identifiers are mixed.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenIdentifiersMixed_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Video.GetVideos(session, userId: "141981764", gameId: "509670").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id,userId,gameId");

        Func<Task> actionWithId = async () => await Video.GetVideos(session, id: ["335921245"], userId: "141981764").ConfigureAwait(true);
        _ = (await actionWithId.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id,userId,gameId");
    }

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> throws <see cref="ArgumentOutOfRangeException"/> when id list exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenIdsExceed100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> ids = Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

        Func<Task> action = async () => await Video.GetVideos(session, id: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> throws <see cref="InvalidOperationException"/> when both before and after are specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenBothBeforeAndAfterAreSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Video.GetVideos(session, userId: "141981764", before: "cur1", after: "cur2").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await Video.GetVideos(session, userId: "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Video.GetVideos"/> returns videos on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetVideos")]
    public async Task GetVideos_WhenRequestSucceeds_ReturnsVideos()
    {
        this._fixture.RegisterEndpoint("/helix/videos", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""335921245"",
      ""stream_id"": null,
      ""user_id"": ""141981764"",
      ""user_login"": ""twitchdev"",
      ""user_name"": ""TwitchDev"",
      ""title"": ""Twitch Developers 101"",
      ""description"": ""Welcome to Twitch development!"",
      ""created_at"": ""2018-11-14T21:40:04Z"",
      ""published_at"": ""2018-11-14T21:40:04Z"",
      ""url"": ""https://www.twitch.tv/videos/335921245"",
      ""thumbnail_url"": ""https://static-cdn.jtvnw.net/cf_vods/thumb-%{width}x%{height}.jpg"",
      ""viewable"": ""public"",
      ""view_count"": 1863062,
      ""language"": ""en"",
      ""type"": ""upload"",
      ""duration"": ""3m21s"",
      ""muted_segments"": [
        {
          ""duration"": 30,
          ""offset"": 120
        }
      ]
    }
  ],
  ""pagination"": {}
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Video>? response = await Video.GetVideos(session, userId: "141981764", first: 20).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Video video = response.Data[0];
        _ = video.Id.Should().Be("335921245");
        _ = video.StreamId.Should().BeNull();
        _ = video.UserId.Should().Be("141981764");
        _ = video.UserLogin.Should().Be("twitchdev");
        _ = video.UserName.Should().Be("TwitchDev");
        _ = video.Title.Should().Be("Twitch Developers 101");
        _ = video.Description.Should().Be("Welcome to Twitch development!");
        _ = video.CreatedAt.Should().Be(DateTime.Parse("2018-11-14T21:40:04Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = video.PublishedAt.Should().Be(DateTime.Parse("2018-11-14T21:40:04Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = video.Url.Should().Be(new Uri("https://www.twitch.tv/videos/335921245"));
        _ = video.ThumbnailUrl.Should().Be("https://static-cdn.jtvnw.net/cf_vods/thumb-%{width}x%{height}.jpg");
        _ = video.Viewable.Should().Be(Video.Viewability.Public);
        _ = video.ViewCount.Should().Be(1863062);
        _ = video.Language.Should().Be("en");
        _ = video.Type.Should().Be(Video.VideoType.Upload);
        _ = video.DurationString.Should().Be("3m21s");
        _ = video.Duration.Should().Be(TimeSpan.FromSeconds(201));

        _ = video.MutedSegments.Should().NotBeNull();
        List<MutedVideoSegment> muted = video.MutedSegments!.ToList();
        _ = muted.Count.Should().Be(1);
        _ = muted[0].DurationSeconds.Should().Be(30);
        _ = muted[0].Duration.Should().Be(TimeSpan.FromSeconds(30));
        _ = muted[0].OffsetSeconds.Should().Be(120);
        _ = muted[0].Offset.Should().Be(TimeSpan.FromSeconds(120));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/videos");
        _ = request.QueryString.Should().Contain("user_id=141981764");
        _ = request.QueryString.Should().Contain("first=20");
    }

    #endregion GetVideos

    #region DeleteVideos

    /// <summary>
    /// Tests that <see cref="Video.DeleteVideos"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteVideos")]
    public async Task DeleteVideos_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Video.DeleteVideos(null!, ["335921245"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Video.DeleteVideos"/> throws <see cref="ArgumentNullException"/> when id is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteVideos")]
    public async Task DeleteVideos_WhenIdIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVideos);
        Func<Task> action = async () => await Video.DeleteVideos(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");

        Func<Task> actionEmpty = async () => await Video.DeleteVideos(session, []).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="Video.DeleteVideos"/> throws <see cref="ArgumentOutOfRangeException"/> when id list exceeds 5 items.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteVideos")]
    public async Task DeleteVideos_WhenIdExceeds5_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVideos);
        List<string> ids = ["1", "2", "3", "4", "5", "6"];

        Func<Task> action = async () => await Video.DeleteVideos(session, ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="Video.DeleteVideos"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteVideos")]
    public async Task DeleteVideos_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Video.DeleteVideos(session, ["335921245"]).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Video.DeleteVideos"/> throws <see cref="TwitchScopeMissingException"/> when ChannelManageVideos scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteVideos")]
    public async Task DeleteVideos_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await Video.DeleteVideos(session, ["335921245"]).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Video.DeleteVideos"/> deletes videos on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteVideos")]
    public async Task DeleteVideos_WhenRequestSucceeds_ReturnsDeletedVideoIds()
    {
        this._fixture.RegisterEndpoint("/helix/videos", HttpStatusCode.OK, @"{
  ""data"": [
    ""335921245""
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVideos);
        ResponseData<string>? response = await Video.DeleteVideos(session, ["335921245"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);
        _ = response.Data[0].Should().Be("335921245");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/videos");
        _ = request.QueryString.Should().Contain("id=335921245");
    }

    #endregion DeleteVideos
}

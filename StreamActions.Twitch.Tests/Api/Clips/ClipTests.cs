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

using StreamActions.Twitch.Api.Clips;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Clips;

/// <summary>
/// Provides unit tests for the <see cref="Clip"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ClipTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ClipTests(TwitchKestrelServerFixture fixture)
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

    #region GetClips

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Clip.GetClips(null!, broadcasterId: "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentNullException"/> when neither broadcasterId, gameId, nor id is provided.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenNoFilterProvided_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Clip.GetClips(session).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id,broadcasterId,gameId");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentOutOfRangeException"/> when mutually exclusive filters are mixed.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenMutuallyExclusiveFiltersMixed_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Clip.GetClips(session, broadcasterId: "123", gameId: "456").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id,broadcasterId,gameId");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentOutOfRangeException"/> when id has more than 100 elements.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenIdCountExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        List<string> ids = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];
        Func<Task> action = async () => await Clip.GetClips(session, id: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="InvalidOperationException"/> when both before and after cursors are defined.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenBeforeAndAfterBothDefined_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Clip.GetClips(session, broadcasterId: "123", before: "c1", after: "c2").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentNullException"/> when only one of startedAt or endedAt is specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenOnlyOneDateSpecified_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Clip.GetClips(session, broadcasterId: "123", startedAt: DateTime.UtcNow).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("startedAt,endedAt");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentNullException"/> when endedAt is specified without startedAt.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenEndedAtSpecifiedWithoutStartedAt_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Clip.GetClips(session, broadcasterId: "123", endedAt: DateTime.UtcNow).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("startedAt,endedAt");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> throws <see cref="ArgumentOutOfRangeException"/> when id is mixed with broadcasterId or gameId.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenIdMixedWithBroadcasterOrGame_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action1 = async () => await Clip.GetClips(session, broadcasterId: "123", id: ["c1"]).ConfigureAwait(true);
        _ = (await action1.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id,broadcasterId,gameId");

        Func<Task> action2 = async () => await Clip.GetClips(session, gameId: "456", id: ["c1"]).ConfigureAwait(true);
        _ = (await action2.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id,broadcasterId,gameId");
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> clamps first parameter between 1 and 100.
    /// </summary>
    /// <param name="inputFirst">The requested first count.</param>
    /// <param name="expectedFirst">The expected clamped query value.</param>
    [Theory]
    [Trait("Member", "GetClips")]
    [InlineData(-5, "1")]
    [InlineData(0, "1")]
    [InlineData(101, "100")]
    [InlineData(500, "100")]
    public async Task GetClips_WhenFirstOutOfRange_ClampsFirstParameter(int inputFirst, string expectedFirst)
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/clips", new { data = Array.Empty<object>() });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        _ = await Clip.GetClips(session, broadcasterId: "12345", first: inputFirst).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Query["first"].ToString().Should().Be(expectedFirst);
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> queries by game ID with date range, after cursor, and isFeatured.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenCalledWithGameIdAndFilters_SendsExpectedQueryParams()
    {
        DateTime startedAt = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime endedAt = new(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/clips", new
        {
            data = new[]
            {
                new
                {
                    id = "GameClip1",
                    game_id = "54321",
                    is_featured = false
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<Clip>? response = await Clip.GetClips(session, gameId: "54321", startedAt: startedAt, endedAt: endedAt, first: 50, after: "cursor123", isFeatured: false).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);
        _ = response.Data[0].Id.Should().Be("GameClip1");
        _ = response.Data[0].GameId.Should().Be("54321");
        _ = response.Data[0].IsFeatured.Should().BeFalse();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/clips");
        _ = request.Query["game_id"].ToString().Should().Be("54321");
        _ = request.Query["first"].ToString().Should().Be("50");
        _ = request.Query["after"].ToString().Should().Be("cursor123");
        _ = request.Query["is_featured"].ToString().Should().Be("false");
        _ = request.Query["started_at"].ToString().Should().NotBeNullOrEmpty();
        _ = request.Query["ended_at"].ToString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> queries by multiple IDs and omits pagination parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenCalledWithClipIds_SendsClipIdsAndOmitsPaginationParams()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/clips", new
        {
            data = new[]
            {
                new { id = "clipA" },
                new { id = "clipB" }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<Clip>? response = await Clip.GetClips(session, id: ["clipA", "clipB"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(2);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/clips");
        _ = request.Query["id"].ToString().Should().Contain("clipA");
        _ = request.Query["id"].ToString().Should().Contain("clipB");
        _ = request.Query.ContainsKey("first").Should().BeFalse();
        _ = request.Query.ContainsKey("started_at").Should().BeFalse();
        _ = request.Query.ContainsKey("ended_at").Should().BeFalse();
        _ = request.Query.ContainsKey("after").Should().BeFalse();
        _ = request.Query.ContainsKey("before").Should().BeFalse();
    }

    /// <summary>
    /// Tests that <see cref="Clip.GetClips"/> retrieves clips and maps properties including calculated Duration.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClips")]
    public async Task GetClips_WhenCalledWithValidParameters_ReturnsClips()
    {
        DateTime createdAt = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/clips", new
        {
            data = new[]
            {
                new
                {
                    id = "RandomClipId",
                    url = "https://clips.twitch.tv/RandomClipId",
                    embed_url = "https://clips.twitch.tv/embed?clip=RandomClipId",
                    broadcaster_id = "12345",
                    broadcaster_name = "BroadcasterUser",
                    creator_id = "67890",
                    creator_name = "CreatorUser",
                    video_id = "11111",
                    game_id = "22222",
                    language = "en",
                    title = "Amazing Gameplay Moment",
                    view_count = 1500,
                    created_at = createdAt,
                    thumbnail_url = "https://clips-media-assets2.twitch.tv/RandomClipId-preview-480x272.jpg",
                    duration = 30.5f,
                    vod_offset = 120,
                    is_featured = true
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<Clip>? response = await Clip.GetClips(session, broadcasterId: "12345", isFeatured: true).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Clip clip = response.Data[0];
        _ = clip.Id.Should().Be("RandomClipId");
        _ = clip.Url.Should().Be(new Uri("https://clips.twitch.tv/RandomClipId"));
        _ = clip.EmbedUrl.Should().Be(new Uri("https://clips.twitch.tv/embed?clip=RandomClipId"));
        _ = clip.BroadcasterId.Should().Be("12345");
        _ = clip.BroadcasterName.Should().Be("BroadcasterUser");
        _ = clip.CreatorId.Should().Be("67890");
        _ = clip.CreatorName.Should().Be("CreatorUser");
        _ = clip.VideoId.Should().Be("11111");
        _ = clip.GameId.Should().Be("22222");
        _ = clip.Language.Should().Be("en");
        _ = clip.Title.Should().Be("Amazing Gameplay Moment");
        _ = clip.ViewCount.Should().Be(1500);
        _ = clip.CreatedAt.Should().Be(createdAt);
        _ = clip.ThumbnailUrl.Should().Be(new Uri("https://clips-media-assets2.twitch.tv/RandomClipId-preview-480x272.jpg"));
        _ = clip.DurationSeconds.Should().Be(30.5f);
        _ = clip.Duration.Should().Be(TimeSpan.FromSeconds(30.5));
        _ = clip.VodOffset.Should().Be(120);
        _ = clip.IsFeatured.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/clips");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["is_featured"].ToString().Should().Be("true");
        _ = request.Query["first"].ToString().Should().Be("20");
    }

    #endregion GetClips

    #region Duration

    /// <summary>
    /// Tests that <see cref="Clip.Duration"/> returns null when DurationSeconds is null.
    /// </summary>
    [Fact]
    [Trait("Member", "Duration")]
    public void Duration_WhenDurationSecondsIsNull_ReturnsNull()
    {
        Clip clip = new() { DurationSeconds = null };
        _ = clip.Duration.Should().BeNull();
    }

    #endregion Duration
}

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

using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Streams;

/// <summary>
/// Provides unit tests for the <see cref="TwitchStream"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class TwitchStreamTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchStreamTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public TwitchStreamTests(TwitchKestrelServerFixture fixture)
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

    #region GetStreams

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await TwitchStream.GetStreams(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="ArgumentOutOfRangeException"/> when userIds exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenUserIdsExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> userIds = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];

        Func<Task> action = async () => await TwitchStream.GetStreams(session, userIds: userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userIds");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="ArgumentOutOfRangeException"/> when userLogins exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenUserLoginsExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> userLogins = [.. Enumerable.Range(1, 101).Select(i => $"login{i}")];

        Func<Task> action = async () => await TwitchStream.GetStreams(session, userLogins: userLogins).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userLogins");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="ArgumentOutOfRangeException"/> when gameIds exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenGameIdsExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> gameIds = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];

        Func<Task> action = async () => await TwitchStream.GetStreams(session, gameIds: gameIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("gameIds");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="ArgumentOutOfRangeException"/> when languages exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenLanguagesExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> languages = [.. Enumerable.Range(1, 101).Select(i => $"l{i}")];

        Func<Task> action = async () => await TwitchStream.GetStreams(session, languages: languages).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("languages");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="InvalidOperationException"/> when both before and after are specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenBothBeforeAndAfterAreSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await TwitchStream.GetStreams(session, before: "cur1", after: "cur2").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> throws <see cref="TokenTypeException"/> when token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await TwitchStream.GetStreams(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetStreams"/> returns streams on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreams")]
    public async Task GetStreams_WhenRequestSucceeds_ReturnsStreams()
    {
        this._fixture.RegisterEndpoint("/helix/streams", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""26007494656"",
      ""user_id"": ""23161357"",
      ""user_login"": ""lirik"",
      ""user_name"": ""LIRIK"",
      ""game_id"": ""417752"",
      ""game_name"": ""Talk Shows & Podcasts"",
      ""type"": ""live"",
      ""title"": ""Hey Guys - What Did I Miss?"",
      ""viewer_count"": 3257,
      ""started_at"": ""2021-03-10T15:04:21Z"",
      ""language"": ""en"",
      ""thumbnail_url"": ""https://static-cdn.jtvnw.net/previews-ttv/live_user_lirik-{width}x{height}.jpg"",
      ""tags"": [""English"", ""Multiplayer""],
      ""is_mature"": false
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursorStream""
  }
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<TwitchStream>? response = await TwitchStream.GetStreams(
            session,
            userIds: ["23161357"],
            userLogins: ["lirik"],
            gameIds: ["417752"],
            type: TwitchStream.StreamTypes.Live,
            languages: ["en"],
            first: 20,
            after: "afterCursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        TwitchStream stream = response.Data[0];
        _ = stream.Id.Should().Be("26007494656");
        _ = stream.UserId.Should().Be("23161357");
        _ = stream.UserLogin.Should().Be("lirik");
        _ = stream.UserName.Should().Be("LIRIK");
        _ = stream.GameId.Should().Be("417752");
        _ = stream.GameName.Should().Be("Talk Shows & Podcasts");
        _ = stream.Type.Should().Be(TwitchStream.StreamTypes.Live);
        _ = stream.Title.Should().Be("Hey Guys - What Did I Miss?");
        _ = stream.ViewerCount.Should().Be(3257);
        _ = stream.StartedAt.Should().Be(DateTime.Parse("2021-03-10T15:04:21Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = stream.Language.Should().Be("en");
        _ = stream.ThumbnailUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/previews-ttv/live_user_lirik-{width}x{height}.jpg"));
        _ = stream.Tags.Should().ContainInOrder("English", "Multiplayer");
        _ = stream.IsMature.Should().BeFalse();

        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursorStream");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/streams");
        _ = request.QueryString.Should().Contain("user_id=23161357");
        _ = request.QueryString.Should().Contain("user_login=lirik");
        _ = request.QueryString.Should().Contain("game_id=417752");
        _ = request.QueryString.Should().Contain("type=live");
        _ = request.QueryString.Should().Contain("language=en");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=afterCursor");
    }

    #endregion GetStreams

    #region GetFollowedStreams

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetFollowedStreams"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedStreams")]
    public async Task GetFollowedStreams_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await TwitchStream.GetFollowedStreams(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetFollowedStreams"/> throws <see cref="ArgumentNullException"/> when userId is null or whitespace.
    /// </summary>
    /// <param name="invalidUserId">The invalid user ID.</param>
    [Theory]
    [Trait("Member", "GetFollowedStreams")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFollowedStreams_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadFollows);
        Func<Task> action = async () => await TwitchStream.GetFollowedStreams(session, invalidUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetFollowedStreams"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedStreams")]
    public async Task GetFollowedStreams_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await TwitchStream.GetFollowedStreams(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetFollowedStreams"/> throws <see cref="TwitchScopeMissingException"/> when UserReadFollows scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedStreams")]
    public async Task GetFollowedStreams_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await TwitchStream.GetFollowedStreams(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="TwitchStream.GetFollowedStreams"/> returns followed streams on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedStreams")]
    public async Task GetFollowedStreams_WhenRequestSucceeds_ReturnsFollowedStreams()
    {
        this._fixture.RegisterEndpoint("/helix/streams/followed", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""26007494656"",
      ""user_id"": ""23161357"",
      ""user_login"": ""lirik"",
      ""user_name"": ""LIRIK"",
      ""game_id"": ""417752"",
      ""game_name"": ""Talk Shows & Podcasts"",
      ""type"": ""live"",
      ""title"": ""Hey Guys - What Did I Miss?"",
      ""viewer_count"": 3257,
      ""started_at"": ""2021-03-10T15:04:21Z"",
      ""language"": ""en"",
      ""thumbnail_url"": ""https://static-cdn.jtvnw.net/previews-ttv/live_user_lirik-{width}x{height}.jpg"",
      ""tags"": [""English""],
      ""is_mature"": false
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursorFollowed""
  }
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadFollows);
        ResponseData<TwitchStream>? response = await TwitchStream.GetFollowedStreams(session, "141981764", 100, "cursorAfter").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        TwitchStream stream = response.Data[0];
        _ = stream.Id.Should().Be("26007494656");
        _ = stream.UserId.Should().Be("23161357");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/streams/followed");
        _ = request.QueryString.Should().Contain("user_id=141981764");
        _ = request.QueryString.Should().Contain("first=100");
        _ = request.QueryString.Should().Contain("after=cursorAfter");
    }

    #endregion GetFollowedStreams
}

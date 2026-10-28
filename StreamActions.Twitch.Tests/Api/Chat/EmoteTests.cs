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

using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="Emote"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class EmoteTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// Sample emote format array containing static format.
    /// </summary>
    private static readonly string[] _staticFormats = ["static"];

    /// <summary>
    /// Sample emote scales array containing all scales (1.0, 2.0, 3.0).
    /// </summary>
    private static readonly string[] _allScales = ["1.0", "2.0", "3.0"];

    /// <summary>
    /// Sample emote scales array containing only the 1.0 scale.
    /// </summary>
    private static readonly string[] _singleScale = ["1.0"];

    /// <summary>
    /// Sample emote theme modes array containing light and dark modes.
    /// </summary>
    private static readonly string[] _allThemeModes = ["light", "dark"];

    /// <summary>
    /// Sample emote theme modes array containing only the light mode.
    /// </summary>
    private static readonly string[] _lightThemeMode = ["light"];

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EmoteTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public EmoteTests(TwitchKestrelServerFixture fixture)
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

    #region GetChannelEmotes

    /// <summary>
    /// Tests that <see cref="Emote.GetChannelEmotes"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelEmotes")]
    public async Task GetChannelEmotes_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Emote.GetChannelEmotes(null!, "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetChannelEmotes"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetChannelEmotes")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelEmotes_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Emote.GetChannelEmotes(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetChannelEmotes"/> correctly retrieves channel emotes and template.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelEmotes")]
    public async Task GetChannelEmotes_WhenCalledWithValidParameters_ReturnsEmotes()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/emotes", new
        {
            data = new[]
            {
                new
                {
                    id = "3001",
                    name = "bleedPurple",
                    images = new
                    {
                        url_1x = "https://static-cdn.jtvnw.net/emoticons/v2/3001/static/light/1.0",
                        url_2x = "https://static-cdn.jtvnw.net/emoticons/v2/3001/static/light/2.0",
                        url_4x = "https://static-cdn.jtvnw.net/emoticons/v2/3001/static/light/3.0"
                    },
                    tier = "1000",
                    emote_type = "subscriptions",
                    emote_set_id = "0",
                    format = _staticFormats,
                    scale = _allScales,
                    theme_mode = _allThemeModes
                }
            },
            template = "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        EmoteResponse? response = await Emote.GetChannelEmotes(session, "12345").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Template.Should().Be("https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}");
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Emote emote = response.Data[0];
        _ = emote.Id.Should().Be("3001");
        _ = emote.Name.Should().Be("bleedPurple");
        _ = emote.Tier.Should().Be("1000");
        _ = emote.EmoteType.Should().Be(Emote.EmoteTypes.Subscriptions);
        _ = emote.EmoteSetId.Should().Be("0");
        _ = emote.Format.Should().Contain(Emote.Formats.Static);
        _ = emote.Scale.Should().Contain(Emote.Scales.Small);
        _ = emote.ThemeMode.Should().Contain(Emote.ThemeModes.Light);
        _ = emote.Images.Should().NotBeNull();
        _ = emote.Images!.Url1X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/3001/static/light/1.0"));
        _ = emote.Images.Url2X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/3001/static/light/2.0"));
        _ = emote.Images.Url4X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/3001/static/light/3.0"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/emotes");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
    }

    #endregion GetChannelEmotes

    #region GetGlobalEmotes

    /// <summary>
    /// Tests that <see cref="Emote.GetGlobalEmotes"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetGlobalEmotes")]
    public async Task GetGlobalEmotes_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Emote.GetGlobalEmotes(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetGlobalEmotes"/> retrieves global emotes.
    /// </summary>
    [Fact]
    [Trait("Member", "GetGlobalEmotes")]
    public async Task GetGlobalEmotes_WhenCalledWithValidParameters_ReturnsGlobalEmotes()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/emotes/global", new
        {
            data = new[]
            {
                new
                {
                    id = "25",
                    name = "Kappa",
                    images = new
                    {
                        url_1x = "https://static-cdn.jtvnw.net/emoticons/v2/25/static/light/1.0",
                        url_2x = "https://static-cdn.jtvnw.net/emoticons/v2/25/static/light/2.0",
                        url_4x = "https://static-cdn.jtvnw.net/emoticons/v2/25/static/light/3.0"
                    },
                    format = _staticFormats,
                    scale = _allScales,
                    theme_mode = _allThemeModes
                }
            },
            template = "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        EmoteResponse? response = await Emote.GetGlobalEmotes(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Template.Should().Be("https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}");
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Emote emote = response.Data[0];
        _ = emote.Id.Should().Be("25");
        _ = emote.Name.Should().Be("Kappa");
        _ = emote.Images.Should().NotBeNull();
        _ = emote.Images!.Url1X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/25/static/light/1.0"));
        _ = emote.Images.Url2X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/25/static/light/2.0"));
        _ = emote.Images.Url4X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/25/static/light/3.0"));
        _ = emote.Format.Should().Contain(Emote.Formats.Static);
        _ = emote.Scale.Should().Contain(Emote.Scales.Small);
        _ = emote.ThemeMode.Should().Contain(Emote.ThemeModes.Light);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/emotes/global");
    }

    #endregion GetGlobalEmotes

    #region GetEmoteSets

    /// <summary>
    /// Tests that <see cref="Emote.GetEmoteSets"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEmoteSets")]
    public async Task GetEmoteSets_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Emote.GetEmoteSets(null!, ["3001"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetEmoteSets"/> throws <see cref="ArgumentNullException"/> when emoteSetIds is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEmoteSets")]
    public async Task GetEmoteSets_WhenEmoteSetIdsIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> actionNull = async () => await Emote.GetEmoteSets(session, null!).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("emoteSetIds");

        Func<Task> actionEmpty = async () => await Emote.GetEmoteSets(session, []).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("emoteSetIds");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetEmoteSets"/> throws <see cref="ArgumentOutOfRangeException"/> when more than 25 IDs are passed.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEmoteSets")]
    public async Task GetEmoteSets_WhenEmoteSetIdsCountExceeds25_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        List<string> ids = [.. Enumerable.Range(1, 26).Select(i => i.ToString(CultureInfo.InvariantCulture))];
        Func<Task> action = async () => await Emote.GetEmoteSets(session, ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("emoteSetIds");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetEmoteSets"/> retrieves emote sets and owner info.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEmoteSets")]
    public async Task GetEmoteSets_WhenCalledWithValidParameters_ReturnsEmoteSets()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/emotes/set", new
        {
            data = new[]
            {
                new
                {
                    id = "3002",
                    name = "coolEmote",
                    images = new
                    {
                        url_1x = "https://static-cdn.jtvnw.net/emoticons/v2/3002/static/light/1.0",
                        url_2x = "https://static-cdn.jtvnw.net/emoticons/v2/3002/static/light/2.0",
                        url_4x = "https://static-cdn.jtvnw.net/emoticons/v2/3002/static/light/3.0"
                    },
                    emote_type = "subscriptions",
                    emote_set_id = "301590448",
                    owner_id = "9876",
                    format = _staticFormats,
                    scale = _singleScale,
                    theme_mode = _lightThemeMode
                }
            },
            template = "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        EmoteResponse? response = await Emote.GetEmoteSets(session, ["301590448", "301590449"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Template.Should().Be("https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}");
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Emote emote = response.Data[0];
        _ = emote.Id.Should().Be("3002");
        _ = emote.Name.Should().Be("coolEmote");
        _ = emote.Images.Should().NotBeNull();
        _ = emote.Images!.Url1X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/3002/static/light/1.0"));
        _ = emote.Images.Url2X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/3002/static/light/2.0"));
        _ = emote.Images.Url4X.Should().Be(new Uri("https://static-cdn.jtvnw.net/emoticons/v2/3002/static/light/3.0"));
        _ = emote.EmoteType.Should().Be(Emote.EmoteTypes.Subscriptions);
        _ = emote.EmoteSetId.Should().Be("301590448");
        _ = emote.OwnerId.Should().Be("9876");
        _ = emote.Format.Should().Contain(Emote.Formats.Static);
        _ = emote.Scale.Should().Contain(Emote.Scales.Small);
        _ = emote.ThemeMode.Should().Contain(Emote.ThemeModes.Light);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/emotes/set");
        _ = request.Query["emote_set_id"].ToString().Should().Contain("301590448");
        _ = request.Query["emote_set_id"].ToString().Should().Contain("301590449");
    }

    #endregion GetEmoteSets

    #region GetUserEmotes

    /// <summary>
    /// Tests that <see cref="Emote.GetUserEmotes"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserEmotes")]
    public async Task GetUserEmotes_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Emote.GetUserEmotes(null!, "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetUserEmotes"/> throws <see cref="ArgumentNullException"/> when userId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid user ID.</param>
    [Theory]
    [Trait("Member", "GetUserEmotes")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserEmotes_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmotes);
        Func<Task> action = async () => await Emote.GetUserEmotes(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetUserEmotes"/> throws <see cref="TokenTypeException"/> when session is an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserEmotes")]
    public async Task GetUserEmotes_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Emote.GetUserEmotes(session, "12345").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetUserEmotes"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserEmotes")]
    public async Task GetUserEmotes_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await Emote.GetUserEmotes(session, "12345").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetUserEmotes"/> retrieves user emotes.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserEmotes")]
    public async Task GetUserEmotes_WhenCalledWithValidParameters_ReturnsUserEmotes()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/emotes/user", new
        {
            data = new[]
            {
                new
                {
                    id = "4001",
                    name = "followerEmote",
                    emote_type = "follower",
                    emote_set_id = "5555",
                    owner_id = "12345",
                    format = _staticFormats,
                    scale = _singleScale,
                    theme_mode = _lightThemeMode
                }
            },
            template = "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}",
            pagination = new
            {
                cursor = "cursor123"
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmotes);
        EmoteResponse? response = await Emote.GetUserEmotes(session, "9999", broadcasterId: "12345", after: "page2").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Template.Should().Be("https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}");
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Emote emote = response.Data[0];
        _ = emote.Id.Should().Be("4001");
        _ = emote.Name.Should().Be("followerEmote");
        _ = emote.EmoteType.Should().Be(Emote.EmoteTypes.Follower);
        _ = emote.EmoteSetId.Should().Be("5555");
        _ = emote.OwnerId.Should().Be("12345");
        _ = emote.Format.Should().Contain(Emote.Formats.Static);
        _ = emote.Scale.Should().Contain(Emote.Scales.Small);
        _ = emote.ThemeMode.Should().Contain(Emote.ThemeModes.Light);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor123");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/emotes/user");
        _ = request.Query["user_id"].ToString().Should().Be("9999");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["after"].ToString().Should().Be("page2");
    }

    /// <summary>
    /// Tests that <see cref="Emote.GetUserEmotes"/> omits broadcaster_id and after from query string when default parameters are used.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserEmotes")]
    public async Task GetUserEmotes_WhenCalledWithDefaultParameters_OmitsBroadcasterIdAndAfterFromQuery()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/emotes/user", new
        {
            data = Array.Empty<object>(),
            template = "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmotes);
        EmoteResponse? response = await Emote.GetUserEmotes(session, "9999").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Query["user_id"].ToString().Should().Be("9999");
        _ = request.QueryString.Should().NotContain("broadcaster_id");
        _ = request.QueryString.Should().NotContain("after");
    }

    #endregion GetUserEmotes
}

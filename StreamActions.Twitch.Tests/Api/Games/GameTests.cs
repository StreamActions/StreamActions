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

using StreamActions.Twitch.Api.Games;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;

namespace StreamActions.Twitch.Tests.Api.Games;

/// <summary>
/// Performs unit and integration testing on <see cref="Game"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class GameTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public GameTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test execution.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetGames

    [Fact]
    [Trait("Member", "GetGames")]
    public async Task GetGames_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Game.GetGames(null!, gameIds: ["33214"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "GetGames")]
    public async Task GetGames_WhenNoIdsOrNamesProvided_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Game.GetGames(session).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("gameIds,gameNames,igdbIds");
    }

    [Fact]
    [Trait("Member", "GetGames")]
    public async Task GetGames_WhenMoreThan100IdsProvided_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> ids = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];

        Func<Task> action = async () => await Game.GetGames(session, gameIds: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("gameIds,gameNames,igdbIds");
    }

    [Fact]
    [Trait("Member", "GetGames")]
    public async Task GetGames_WhenSessionTokenIsNotUserOrApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await Game.GetGames(session, gameIds: ["33214"]).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetGames")]
    public async Task GetGames_WhenValidRequest_SendsGetAndReturnsGames()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "33214",
              "name": "Fortnite",
              "box_art_url": "https://static-cdn.jtvnw.net/ttv-boxart/33214-{width}x{height}.jpg",
              "igdb_id": "1905"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/games", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Game>? response = await Game.GetGames(session, gameIds: ["33214"], gameNames: ["Fortnite"], igdbIds: ["1905"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Game game = response.Data[0];
        _ = game.Id.Should().Be("33214");
        _ = game.Name.Should().Be("Fortnite");
        _ = game.BoxArtUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/ttv-boxart/33214-{width}x{height}.jpg"));
        _ = game.IgdbId.Should().Be("1905");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/games");
        _ = request.QueryString.Should().Contain("id=33214");
        _ = request.QueryString.Should().Contain("name=Fortnite");
        _ = request.QueryString.Should().Contain("igdb_id=1905");
    }

    #endregion GetGames

    #region GetTopGames

    [Fact]
    [Trait("Member", "GetTopGames")]
    public async Task GetTopGames_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Game.GetTopGames(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "GetTopGames")]
    public async Task GetTopGames_WhenSessionTokenIsNotUserOrApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await Game.GetTopGames(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetTopGames")]
    public async Task GetTopGames_WhenValidRequest_SendsGetAndReturnsTopGames()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "493057",
              "name": "PUBG: BATTLEGROUNDS",
              "box_art_url": "https://static-cdn.jtvnw.net/ttv-boxart/493057-{width}x{height}.jpg",
              "igdb_id": "27789"
            }
          ],
          "pagination": {
            "cursor": "eyJiIjpudWxsLCJhIjp7Ik9mZnNldCI6MjB9fQ=="
          }
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/games/top", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Game>? response = await Game.GetTopGames(session, 20, "after_cursor", "before_cursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Game game = response.Data[0];
        _ = game.Id.Should().Be("493057");
        _ = game.Name.Should().Be("PUBG: BATTLEGROUNDS");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/games/top");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=after_cursor");
        _ = request.QueryString.Should().Contain("before=before_cursor");
    }

    #endregion GetTopGames
}

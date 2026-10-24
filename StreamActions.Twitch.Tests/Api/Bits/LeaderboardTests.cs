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
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Bits;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Bits;

/// <summary>
/// Provides unit and integration tests for <see cref="Leaderboard"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class LeaderboardTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaderboardTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public LeaderboardTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test execution.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetBitsLeaderboard

    /// <summary>
    /// Tests that <see cref="Leaderboard.GetBitsLeaderboard"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBitsLeaderboard")]
    public async Task GetBitsLeaderboard_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Leaderboard.GetBitsLeaderboard(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Leaderboard.GetBitsLeaderboard"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBitsLeaderboard")]
    public async Task GetBitsLeaderboard_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Leaderboard.GetBitsLeaderboard(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Leaderboard.GetBitsLeaderboard"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBitsLeaderboard")]
    public async Task GetBitsLeaderboard_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await Leaderboard.GetBitsLeaderboard(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Leaderboard.GetBitsLeaderboard"/> throws <see cref="InvalidOperationException"/> when startedAt is provided with period All.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBitsLeaderboard")]
    public async Task GetBitsLeaderboard_WhenStartedAtSpecifiedWithPeriodAll_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Leaderboard.GetBitsLeaderboard(session, period: Leaderboard.LeaderboardPeriod.All, startedAt: DateTime.UtcNow).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("Can not use startedAt while period is set to all");
    }

    /// <summary>
    /// Tests that <see cref="Leaderboard.GetBitsLeaderboard"/> sends a GET request and returns leaderboard data.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBitsLeaderboard")]
    public async Task GetBitsLeaderboard_WhenValidParameters_ReturnsLeaderboard()
    {
        DateTime startedAt = new(2018, 2, 5, 8, 0, 0, DateTimeKind.Utc);
        DateTime endedAt = new(2018, 2, 12, 8, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/bits/leaderboard", new
        {
            data = new[]
            {
                new
                {
                    user_id = "158010205",
                    user_login = "tundracowboy",
                    user_name = "TundraCowboy",
                    rank = 1,
                    score = 12543
                }
            },
            date_range = new
            {
                started_at = startedAt,
                ended_at = endedAt
            },
            total = 2
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        ResponseData<Leaderboard>? response = await Leaderboard.GetBitsLeaderboard(
            session,
            count: 10,
            period: Leaderboard.LeaderboardPeriod.Week,
            startedAt: startedAt,
            userId: "158010205").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);
        _ = response.Total.Should().Be(2);

        Leaderboard entry = response.Data[0];
        _ = entry.UserId.Should().Be("158010205");
        _ = entry.UserLogin.Should().Be("tundracowboy");
        _ = entry.UserName.Should().Be("TundraCowboy");
        _ = entry.Rank.Should().Be(1);
        _ = entry.Score.Should().Be(12543);
        _ = response.DateRange.Should().NotBeNull();
        _ = response.DateRange!.StartedAt.Should().Be(startedAt);
        _ = response.DateRange.EndedAt.Should().Be(endedAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/leaderboard");
        _ = request.QueryString.Should().Contain("count=10");
        _ = request.QueryString.Should().Contain("period=week");
        _ = request.QueryString.Should().Contain("user_id=158010205");
    }

    #endregion GetBitsLeaderboard
}

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
using System.Globalization;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Bits;

/// <summary>
/// Provides unit and integration tests for <see cref="CustomPowerUp"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class CustomPowerUpTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPowerUpTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public CustomPowerUpTests(TwitchKestrelServerFixture fixture)
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

    #region GetCustomPowerUps

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomPowerUps")]
    public async Task GetCustomPowerUps_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await CustomPowerUp.GetCustomPowerUps(null!, "274637212").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetCustomPowerUps")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCustomPowerUps_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await CustomPowerUp.GetCustomPowerUps(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> throws <see cref="ArgumentOutOfRangeException"/> when id collection has more than 50 elements.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomPowerUps")]
    public async Task GetCustomPowerUps_WhenIdCountExceeds50_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        List<string> ids = Enumerable.Range(1, 51).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
        Func<Task> action = async () => await CustomPowerUp.GetCustomPowerUps(session, "274637212", ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomPowerUps")]
    public async Task GetCustomPowerUps_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await CustomPowerUp.GetCustomPowerUps(session, "274637212").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomPowerUps")]
    public async Task GetCustomPowerUps_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await CustomPowerUp.GetCustomPowerUps(session, "274637212").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> sends a GET request and returns custom power-ups list.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomPowerUps")]
    public async Task GetCustomPowerUps_WhenValidParameters_ReturnsCustomPowerUps()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/bits/custom_power_ups", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = "92af127c-7326-4483-a52b-b0da0be61c02",
                    image = (object?)null,
                    background_color = "#00FF00",
                    is_enabled = true,
                    bits = 100,
                    title = "game analysis",
                    prompt = "",
                    is_user_input_required = false,
                    is_paused = false,
                    is_in_stock = true
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        ResponseData<CustomPowerUp>? response = await CustomPowerUp.GetCustomPowerUps(session, "274637212").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        CustomPowerUp powerUp = response.Data[0];
        _ = powerUp.BroadcasterId.Should().Be("274637212");
        _ = powerUp.BroadcasterName.Should().Be("torpedo09");
        _ = powerUp.BroadcasterLogin.Should().Be("torpedo09");
        _ = powerUp.Id.Should().Be("92af127c-7326-4483-a52b-b0da0be61c02");
        _ = powerUp.Title.Should().Be("game analysis");
        _ = powerUp.Bits.Should().Be(100);
        _ = powerUp.BackgroundColor.Should().Be("#00FF00");
        _ = powerUp.IsEnabled.Should().BeTrue();
        _ = powerUp.IsPaused.Should().BeFalse();
        _ = powerUp.IsInStock.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/custom_power_ups");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
    }

    #endregion GetCustomPowerUps
}

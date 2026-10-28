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

using StreamActions.Twitch.Api.Bits;
using StreamActions.Twitch.Api.Bits.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;

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
    public void Dispose() => this._fixture.Reset();

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
        List<string> ids = [.. Enumerable.Range(1, 51).Select(i => i.ToString(CultureInfo.InvariantCulture))];
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
        DateTime cooldownExpiresAt = new(2023, 9, 1, 12, 0, 0, DateTimeKind.Utc);

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
                    image = new
                    {
                        url_1x = "https://example.com/image_1x.png",
                        url_2x = "https://example.com/image_2x.png",
                        url_4x = "https://example.com/image_4x.png"
                    },
                    default_image = new
                    {
                        url_1x = "https://example.com/def_1x.png",
                        url_2x = "https://example.com/def_2x.png",
                        url_4x = "https://example.com/def_4x.png"
                    },
                    background_color = "#00FF00",
                    is_enabled = true,
                    bits = 100,
                    title = "game analysis",
                    prompt = "enter game",
                    is_user_input_required = true,
                    max_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_stream = 10L
                    },
                    max_per_user_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_user_per_stream = 5L
                    },
                    global_cooldown_setting = new
                    {
                        is_enabled = true,
                        global_cooldown_seconds = 300L
                    },
                    is_paused = false,
                    is_in_stock = true,
                    redemptions_redeemed_current_stream = 2,
                    cooldown_expires_at = cooldownExpiresAt
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
        _ = powerUp.Prompt.Should().Be("enter game");
        _ = powerUp.Bits.Should().Be(100);
        _ = powerUp.Image.Should().NotBeNull();
        _ = powerUp.Image.Url1X.Should().Be(new Uri("https://example.com/image_1x.png"));
        _ = powerUp.Image.Url2X.Should().Be(new Uri("https://example.com/image_2x.png"));
        _ = powerUp.Image.Url4X.Should().Be(new Uri("https://example.com/image_4x.png"));
        _ = powerUp.DefaultImage.Should().NotBeNull();
        _ = powerUp.DefaultImage.Url1X.Should().Be(new Uri("https://example.com/def_1x.png"));
        _ = powerUp.DefaultImage.Url2X.Should().Be(new Uri("https://example.com/def_2x.png"));
        _ = powerUp.DefaultImage.Url4X.Should().Be(new Uri("https://example.com/def_4x.png"));
        _ = powerUp.BackgroundColor.Should().Be("#00FF00");
        _ = powerUp.IsEnabled.Should().BeTrue();
        _ = powerUp.IsUserInputRequired.Should().BeTrue();
        _ = powerUp.MaxPerStreamSetting.Should().NotBeNull();
        _ = powerUp.MaxPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = powerUp.MaxPerStreamSetting.MaxPerStream.Should().Be(10L);
        _ = powerUp.MaxPerUserPerStreamSetting.Should().NotBeNull();
        _ = powerUp.MaxPerUserPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = powerUp.MaxPerUserPerStreamSetting.MaxPerUserPerStream.Should().Be(5L);
        _ = powerUp.GlobalCooldownSetting.Should().NotBeNull();
        _ = powerUp.GlobalCooldownSetting.IsEnabled.Should().BeTrue();
        _ = powerUp.GlobalCooldownSetting.GlobalCooldownSeconds.Should().Be(300L);
        _ = powerUp.GlobalCooldownSetting.GlobalCooldown.Should().Be(TimeSpan.FromSeconds(300));
        _ = powerUp.IsPaused.Should().BeFalse();
        _ = powerUp.IsInStock.Should().BeTrue();
        _ = powerUp.RedemptionsRedeemedCurrentStream.Should().Be(2);
        _ = powerUp.CooldownExpiresAt.Should().Be(cooldownExpiresAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/custom_power_ups");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.GetCustomPowerUps"/> includes id parameters when specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomPowerUps")]
    public async Task GetCustomPowerUps_WhenIdsProvided_IncludesIdsInQuery()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/bits/custom_power_ups", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        ResponseData<CustomPowerUp>? response = await CustomPowerUp.GetCustomPowerUps(session, "274637212", ["id1", "id2"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("id=id1");
        _ = request.QueryString.Should().Contain("id=id2");
    }

    #endregion GetCustomPowerUps

    #region Properties

    /// <summary>
    /// Tests that <see cref="CustomPowerUpGlobalCooldownSetting.GlobalCooldown"/> returns <see langword="null"/> when <see cref="CustomPowerUpGlobalCooldownSetting.GlobalCooldownSeconds"/> is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GlobalCooldown")]
    public void GlobalCooldown_WhenGlobalCooldownSecondsIsNull_ReturnsNull()
    {
        CustomPowerUpGlobalCooldownSetting setting = new() { GlobalCooldownSeconds = null };
        _ = setting.GlobalCooldown.Should().BeNull();
    }

    /// <summary>
    /// Tests that <see cref="CustomPowerUp.BackgroundColor"/> returns <see langword="null"/> when background color is null.
    /// </summary>
    [Fact]
    [Trait("Member", "BackgroundColor")]
    public void BackgroundColor_WhenNull_ReturnsNull()
    {
        CustomPowerUp powerUp = new() { BackgroundColor = null };
        _ = powerUp.BackgroundColor.Should().BeNull();
    }

    #endregion Properties
}

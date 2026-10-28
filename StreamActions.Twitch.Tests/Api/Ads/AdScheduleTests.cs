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

using StreamActions.Twitch.Api.Ads;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Ads;

/// <summary>
/// Provides unit and integration tests for <see cref="AdSchedule"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class AdScheduleTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AdScheduleTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public AdScheduleTests(TwitchKestrelServerFixture fixture)
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

    #region GetAdSchedule

    /// <summary>
    /// Tests that <see cref="AdSchedule.GetAdSchedule"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAdSchedule")]
    public async Task GetAdSchedule_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await AdSchedule.GetAdSchedule(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.GetAdSchedule"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetAdSchedule")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAdSchedule_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadAds);
        Func<Task> action = async () => await AdSchedule.GetAdSchedule(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.GetAdSchedule"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAdSchedule")]
    public async Task GetAdSchedule_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await AdSchedule.GetAdSchedule(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.GetAdSchedule"/> sends a GET request and returns ad schedule data including calculated properties.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAdSchedule")]
    public async Task GetAdSchedule_WhenValidParameters_ReturnsAdScheduleAndCalculatedProperties()
    {
        DateTime nextAd = new(2023, 8, 1, 23, 8, 18, DateTimeKind.Utc);
        DateTime lastAd = new(2023, 8, 1, 23, 0, 0, DateTimeKind.Utc);
        DateTime refreshAt = new(2023, 8, 1, 23, 8, 18, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/channels/ads", new
        {
            data = new[]
            {
                new
                {
                    next_ad_at = nextAd,
                    last_ad_at = lastAd,
                    duration = 60,
                    preroll_free_time = 90,
                    snooze_count = 1,
                    snooze_refresh_at = refreshAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadAds);
        ResponseData<AdSchedule>? response = await AdSchedule.GetAdSchedule(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        AdSchedule schedule = response.Data[0];
        _ = schedule.DurationSeconds.Should().Be(60);
        _ = schedule.Duration.Should().Be(TimeSpan.FromSeconds(60));
        _ = schedule.PrerollFreeTimeSeconds.Should().Be(90);
        _ = schedule.PrerollFreeTime.Should().Be(TimeSpan.FromSeconds(90));
        _ = schedule.SnoozeCount.Should().Be(1);
        _ = schedule.NextAdAt.Should().Be(nextAd);
        _ = schedule.LastAdAt.Should().Be(lastAd);
        _ = schedule.SnoozeRefreshAt.Should().Be(refreshAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channels/ads");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.GetAdSchedule"/> succeeds when authorized with an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAdSchedule")]
    public async Task GetAdSchedule_WhenAppTokenProvided_ReturnsAdSchedule()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/channels/ads", new
        {
            data = new[]
            {
                new
                {
                    snooze_count = 1,
                    duration = 60
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelReadAds);
        ResponseData<AdSchedule>? response = await AdSchedule.GetAdSchedule(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.GetAdSchedule"/> handles null next_ad_at and last_ad_at timestamps.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAdSchedule")]
    public async Task GetAdSchedule_WhenTimestampsAreNull_ReturnsAdScheduleWithNullTimestamps()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/channels/ads", new
        {
            data = new[]
            {
                new
                {
                    next_ad_at = (DateTime?)null,
                    last_ad_at = (DateTime?)null,
                    duration = 30,
                    preroll_free_time = 0,
                    snooze_count = 0,
                    snooze_refresh_at = (DateTime?)null
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadAds);
        ResponseData<AdSchedule>? response = await AdSchedule.GetAdSchedule(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);
        _ = response.Data[0].NextAdAt.Should().BeNull();
        _ = response.Data[0].LastAdAt.Should().BeNull();
    }

    #endregion GetAdSchedule

    #region SnoozeNextAd

    /// <summary>
    /// Tests that <see cref="AdSchedule.SnoozeNextAd"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SnoozeNextAd")]
    public async Task SnoozeNextAd_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await AdSchedule.SnoozeNextAd(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.SnoozeNextAd"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "SnoozeNextAd")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SnoozeNextAd_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageAds);
        Func<Task> action = async () => await AdSchedule.SnoozeNextAd(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.SnoozeNextAd"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "SnoozeNextAd")]
    public async Task SnoozeNextAd_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await AdSchedule.SnoozeNextAd(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.SnoozeNextAd"/> posts to the snooze endpoint and returns the updated schedule.
    /// </summary>
    [Fact]
    [Trait("Member", "SnoozeNextAd")]
    public async Task SnoozeNextAd_WhenValidParameters_PostsRequestAndReturnsAdSchedule()
    {
        DateTime nextAd = new(2023, 8, 1, 23, 13, 18, DateTimeKind.Utc);
        DateTime refreshAt = new(2023, 8, 1, 23, 8, 18, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("POST", "/helix/channels/ads/schedule/snooze", new
        {
            data = new[]
            {
                new
                {
                    snooze_count = 1,
                    snooze_refresh_at = refreshAt,
                    next_ad_at = nextAd
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageAds);
        ResponseData<AdSchedule>? response = await AdSchedule.SnoozeNextAd(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        AdSchedule schedule = response.Data[0];
        _ = schedule.SnoozeCount.Should().Be(1);
        _ = schedule.NextAdAt.Should().Be(nextAd);
        _ = schedule.SnoozeRefreshAt.Should().Be(refreshAt);
        _ = schedule.DurationSeconds.Should().BeNull();
        _ = schedule.Duration.Should().BeNull();
        _ = schedule.LastAdAt.Should().BeNull();
        _ = schedule.PrerollFreeTimeSeconds.Should().BeNull();
        _ = schedule.PrerollFreeTime.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/channels/ads/schedule/snooze");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.SnoozeNextAd"/> succeeds when authorized with an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "SnoozeNextAd")]
    public async Task SnoozeNextAd_WhenAppTokenProvided_PostsRequestAndReturnsAdSchedule()
    {
        this._fixture.RegisterJsonResponse("POST", "/helix/channels/ads/schedule/snooze", new
        {
            data = new[]
            {
                new
                {
                    snooze_count = 1
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManageAds);
        ResponseData<AdSchedule>? response = await AdSchedule.SnoozeNextAd(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);
    }

    #endregion SnoozeNextAd

    #region Properties

    /// <summary>
    /// Tests that <see cref="AdSchedule.Duration"/> returns <see langword="null"/> when <see cref="AdSchedule.DurationSeconds"/> is null.
    /// </summary>
    [Fact]
    [Trait("Member", "Duration")]
    public void Duration_WhenDurationSecondsIsNull_ReturnsNull()
    {
        AdSchedule schedule = new() { DurationSeconds = null };
        _ = schedule.Duration.Should().BeNull();
    }

    /// <summary>
    /// Tests that <see cref="AdSchedule.PrerollFreeTime"/> returns <see langword="null"/> when <see cref="AdSchedule.PrerollFreeTimeSeconds"/> is null.
    /// </summary>
    [Fact]
    [Trait("Member", "PrerollFreeTime")]
    public void PrerollFreeTime_WhenPrerollFreeTimeSecondsIsNull_ReturnsNull()
    {
        AdSchedule schedule = new() { PrerollFreeTimeSeconds = null };
        _ = schedule.PrerollFreeTime.Should().BeNull();
    }

    #endregion Properties
}

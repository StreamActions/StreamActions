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

using StreamActions.Common.Net;
using StreamActions.Twitch.Api.ChannelPoints;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Drawing;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.ChannelPoints;

/// <summary>
/// Provides unit and integration tests for <see cref="ChannelPointsReward"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelPointsRewardTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelPointsRewardTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChannelPointsRewardTests(TwitchKestrelServerFixture fixture)
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

    #region GetCustomReward

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomReward")]
    public async Task GetCustomReward_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelPointsReward.GetCustomReward(null!, "274637212").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetCustomReward")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCustomReward_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        Func<Task> action = async () => await ChannelPointsReward.GetCustomReward(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when id count exceeds 50.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomReward")]
    public async Task GetCustomReward_WhenIdCountExceeds50_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        List<Guid> ids = [.. Enumerable.Range(1, 51).Select(_ => Guid.NewGuid())];
        Func<Task> action = async () => await ChannelPointsReward.GetCustomReward(session, "274637212", id: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomReward")]
    public async Task GetCustomReward_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ChannelPointsReward.GetCustomReward(session, "274637212").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomReward")]
    public async Task GetCustomReward_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ChannelPointsReward.GetCustomReward(session, "274637212").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> sends a GET request and returns reward data.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomReward")]
    public async Task GetCustomReward_WhenValidParameters_ReturnsCustomRewards()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");
        DateTime cooldownExpiresAt = new(2020, 7, 1, 19, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/channel_points/custom_rewards", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = rewardGuid,
                    title = "game analysis",
                    prompt = "enter game",
                    cost = 50000,
                    image = new
                    {
                        url_1x = "https://example.com/custom/1x.png",
                        url_2x = "https://example.com/custom/2x.png",
                        url_4x = "https://example.com/custom/4x.png"
                    },
                    default_image = new
                    {
                        url_1x = "https://example.com/default/1x.png",
                        url_2x = "https://example.com/default/2x.png",
                        url_4x = "https://example.com/default/4x.png"
                    },
                    background_color = "#00FF00",
                    is_enabled = true,
                    is_user_input_required = true,
                    max_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_stream = 10
                    },
                    max_per_user_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_user_per_stream = 5
                    },
                    global_cooldown_setting = new
                    {
                        is_enabled = true,
                        global_cooldown_seconds = 60
                    },
                    is_paused = false,
                    is_in_stock = true,
                    should_redemptions_skip_request_queue = false,
                    redemptions_redeemed_current_stream = 2,
                    cooldown_expires_at = cooldownExpiresAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.GetCustomReward(session, "274637212", [rewardGuid], true).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChannelPointsReward reward = response.Data[0];
        _ = reward.BroadcasterId.Should().Be("274637212");
        _ = reward.BroadcasterLogin.Should().Be("torpedo09");
        _ = reward.BroadcasterName.Should().Be("torpedo09");
        _ = reward.Id.Should().Be(rewardGuid);
        _ = reward.Title.Should().Be("game analysis");
        _ = reward.Prompt.Should().Be("enter game");
        _ = reward.Cost.Should().Be(50000);
        _ = reward.Image.Should().NotBeNull();
        _ = reward.Image.Url1X.Should().Be(new Uri("https://example.com/custom/1x.png"));
        _ = reward.Image.Url2X.Should().Be(new Uri("https://example.com/custom/2x.png"));
        _ = reward.Image.Url4X.Should().Be(new Uri("https://example.com/custom/4x.png"));
        _ = reward.DefaultImage.Should().NotBeNull();
        _ = reward.DefaultImage.Url1X.Should().Be(new Uri("https://example.com/default/1x.png"));
        _ = reward.DefaultImage.Url2X.Should().Be(new Uri("https://example.com/default/2x.png"));
        _ = reward.DefaultImage.Url4X.Should().Be(new Uri("https://example.com/default/4x.png"));
        _ = reward.BackgroundColorString.Should().Be("#00FF00");
        _ = reward.BackgroundColor.Should().Be(Color.FromArgb(0, 255, 0));
        _ = reward.IsEnabled.Should().BeTrue();
        _ = reward.IsUserInputRequired.Should().BeTrue();
        _ = reward.MaxPerStreamSetting.Should().NotBeNull();
        _ = reward.MaxPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = reward.MaxPerStreamSetting.MaxPerStream.Should().Be(10);
        _ = reward.MaxPerUserPerStreamSetting.Should().NotBeNull();
        _ = reward.MaxPerUserPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = reward.MaxPerUserPerStreamSetting.MaxPerUserPerStream.Should().Be(5);
        _ = reward.GlobalCooldownSetting.Should().NotBeNull();
        _ = reward.GlobalCooldownSetting.IsEnabled.Should().BeTrue();
        _ = reward.GlobalCooldownSetting.GlobalCooldownSeconds.Should().Be(60);
        _ = reward.GlobalCooldownSetting.GlobalCooldown.Should().Be(TimeSpan.FromSeconds(60));
        _ = reward.IsPaused.Should().BeFalse();
        _ = reward.IsInStock.Should().BeTrue();
        _ = reward.ShouldRedemptionsSkipRequestQueue.Should().BeFalse();
        _ = reward.RedemptionsRedeemedCurrentStream.Should().Be(2);
        _ = reward.CooldownExpiresAt.Should().Be(cooldownExpiresAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"id={rewardGuid:D}");
        _ = request.QueryString.Should().Contain("only_manageable_rewards=true");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.GetCustomReward"/> omits optional query parameters when not specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomReward")]
    public async Task GetCustomReward_WhenOptionalParametersOmitted_OmitsFromQuery()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/channel_points/custom_rewards", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.GetCustomReward(session, "274637212").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.Query.ContainsKey("id").Should().BeFalse();
        _ = request.QueryString.Should().NotContain("only_manageable_rewards");
    }

    #endregion GetCustomReward

    #region CreateCustomReward

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Reward", Cost = 100 };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(null!, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "CreateCustomReward")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCustomReward_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Reward", Cost = 100 };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when Title is null or whitespace.
    /// </summary>
    /// <param name="invalidTitle">The invalid title to test.</param>
    [Theory]
    [Trait("Member", "CreateCustomReward")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCustomReward_WhenTitleIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTitle)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = invalidTitle, Cost = 100 };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when Cost is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenCostIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Reward", Cost = null };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Cost");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when Cost is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenCostIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Reward", Cost = 0 };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Cost");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when Title has more than 45 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenTitleExceeds45Characters_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = new string('a', 46), Cost = 100 };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when Prompt has more than 200 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenPromptExceeds200Characters_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Title", Cost = 100, Prompt = new string('a', 201) };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Prompt");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="InvalidOperationException"/> when BackgroundColor is not a valid hex color.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenBackgroundColorIsInvalidHex_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Title", Cost = 100, BackgroundColor = "not-hex" };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("BackgroundColor must be a valid hex triplet (#RRGGBB)");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when IsMaxPerStreamEnabled and MaxPerStream mismatch.
    /// </summary>
    /// <param name="isEnabled">Whether max per stream is enabled.</param>
    /// <param name="maxPerStream">The max per stream amount.</param>
    [Theory]
    [Trait("Member", "CreateCustomReward")]
    [InlineData(true, null)]
    [InlineData(null, 10)]
    public async Task CreateCustomReward_WhenMaxPerStreamMismatch_ThrowsArgumentNullException(bool? isEnabled, int? maxPerStream)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "Title",
            Cost = 100,
            IsMaxPerStreamEnabled = isEnabled,
            MaxPerStream = maxPerStream
        };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsMaxPerStreamEnabled,MaxPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when MaxPerStream is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenMaxPerStreamIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "Title",
            Cost = 100,
            IsMaxPerStreamEnabled = true,
            MaxPerStream = 0
        };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("MaxPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when IsMaxPerUserPerStreamEnabled and MaxPerUserPerStream mismatch.
    /// </summary>
    /// <param name="isEnabled">Whether max per user per stream is enabled.</param>
    /// <param name="maxPerUser">The max per user per stream amount.</param>
    [Theory]
    [Trait("Member", "CreateCustomReward")]
    [InlineData(true, null)]
    [InlineData(null, 5)]
    public async Task CreateCustomReward_WhenMaxPerUserPerStreamMismatch_ThrowsArgumentNullException(bool? isEnabled, int? maxPerUser)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "Title",
            Cost = 100,
            IsMaxPerUserPerStreamEnabled = isEnabled,
            MaxPerUserPerStream = maxPerUser
        };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsMaxPerUserPerStreamEnabled,MaxPerUserPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when MaxPerUserPerStream is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenMaxPerUserPerStreamIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "Title",
            Cost = 100,
            IsMaxPerUserPerStreamEnabled = true,
            MaxPerUserPerStream = 0
        };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("MaxPerUserPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentNullException"/> when IsGlobalCooldownEnabled and GlobalCooldownSeconds mismatch.
    /// </summary>
    /// <param name="isEnabled">Whether global cooldown is enabled.</param>
    /// <param name="cooldownSeconds">The cooldown seconds.</param>
    [Theory]
    [Trait("Member", "CreateCustomReward")]
    [InlineData(true, null)]
    [InlineData(null, 60)]
    public async Task CreateCustomReward_WhenGlobalCooldownMismatch_ThrowsArgumentNullException(bool? isEnabled, int? cooldownSeconds)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "Title",
            Cost = 100,
            IsGlobalCooldownEnabled = isEnabled,
            GlobalCooldownSeconds = cooldownSeconds
        };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsGlobalCooldownEnabled,GlobalCooldownSeconds");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when GlobalCooldownSeconds is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenGlobalCooldownSecondsIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "Title",
            Cost = 100,
            IsGlobalCooldownEnabled = true,
            GlobalCooldownSeconds = 0
        };
        Func<Task> action = async () => await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("GlobalCooldownSeconds");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> sends a POST request and returns created reward.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenValidParameters_CreatesAndReturnsCustomReward()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");
        DateTime cooldownExpiresAt = new(2020, 7, 1, 19, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("POST", "/helix/channel_points/custom_rewards", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = rewardGuid,
                    title = "New Reward",
                    prompt = "enter game",
                    cost = 100,
                    image = new
                    {
                        url_1x = "https://example.com/custom/1x.png",
                        url_2x = "https://example.com/custom/2x.png",
                        url_4x = "https://example.com/custom/4x.png"
                    },
                    default_image = new
                    {
                        url_1x = "https://example.com/default/1x.png",
                        url_2x = "https://example.com/default/2x.png",
                        url_4x = "https://example.com/default/4x.png"
                    },
                    background_color = "#00FF00",
                    is_enabled = true,
                    is_user_input_required = true,
                    max_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_stream = 10
                    },
                    max_per_user_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_user_per_stream = 5
                    },
                    global_cooldown_setting = new
                    {
                        is_enabled = true,
                        global_cooldown_seconds = 60
                    },
                    is_paused = false,
                    is_in_stock = true,
                    should_redemptions_skip_request_queue = false,
                    redemptions_redeemed_current_stream = 2,
                    cooldown_expires_at = cooldownExpiresAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new()
        {
            Title = "New Reward",
            Cost = 100,
            Prompt = "enter game",
            IsEnabled = true,
            BackgroundColor = "#00ff00",
            IsUserInputRequired = true,
            IsMaxPerStreamEnabled = true,
            MaxPerStream = 10,
            IsMaxPerUserPerStreamEnabled = true,
            MaxPerUserPerStream = 5,
            IsGlobalCooldownEnabled = true,
            GlobalCooldownSeconds = 60,
            ShouldRedemptionsSkipRequestQueue = false
        };
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChannelPointsReward reward = response.Data[0];
        _ = reward.BroadcasterId.Should().Be("274637212");
        _ = reward.BroadcasterLogin.Should().Be("torpedo09");
        _ = reward.BroadcasterName.Should().Be("torpedo09");
        _ = reward.Id.Should().Be(rewardGuid);
        _ = reward.Title.Should().Be("New Reward");
        _ = reward.Prompt.Should().Be("enter game");
        _ = reward.Cost.Should().Be(100);
        _ = reward.Image.Should().NotBeNull();
        _ = reward.Image.Url1X.Should().Be(new Uri("https://example.com/custom/1x.png"));
        _ = reward.Image.Url2X.Should().Be(new Uri("https://example.com/custom/2x.png"));
        _ = reward.Image.Url4X.Should().Be(new Uri("https://example.com/custom/4x.png"));
        _ = reward.DefaultImage.Should().NotBeNull();
        _ = reward.DefaultImage.Url1X.Should().Be(new Uri("https://example.com/default/1x.png"));
        _ = reward.DefaultImage.Url2X.Should().Be(new Uri("https://example.com/default/2x.png"));
        _ = reward.DefaultImage.Url4X.Should().Be(new Uri("https://example.com/default/4x.png"));
        _ = reward.BackgroundColorString.Should().Be("#00FF00");
        _ = reward.BackgroundColor.Should().Be(Color.FromArgb(0, 255, 0));
        _ = reward.IsEnabled.Should().BeTrue();
        _ = reward.IsUserInputRequired.Should().BeTrue();
        _ = reward.MaxPerStreamSetting.Should().NotBeNull();
        _ = reward.MaxPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = reward.MaxPerStreamSetting.MaxPerStream.Should().Be(10);
        _ = reward.MaxPerUserPerStreamSetting.Should().NotBeNull();
        _ = reward.MaxPerUserPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = reward.MaxPerUserPerStreamSetting.MaxPerUserPerStream.Should().Be(5);
        _ = reward.GlobalCooldownSetting.Should().NotBeNull();
        _ = reward.GlobalCooldownSetting.IsEnabled.Should().BeTrue();
        _ = reward.GlobalCooldownSetting.GlobalCooldownSeconds.Should().Be(60);
        _ = reward.GlobalCooldownSetting.GlobalCooldown.Should().Be(TimeSpan.FromSeconds(60));
        _ = reward.IsPaused.Should().BeFalse();
        _ = reward.IsInStock.Should().BeTrue();
        _ = reward.ShouldRedemptionsSkipRequestQueue.Should().BeFalse();
        _ = reward.RedemptionsRedeemedCurrentStream.Should().Be(2);
        _ = reward.CooldownExpiresAt.Should().Be(cooldownExpiresAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.Body.Should().Contain("\"title\":\"New Reward\"");
        _ = request.Body.Should().Contain("\"cost\":100");
        _ = request.Body.Should().Contain("\"prompt\":\"enter game\"");
        _ = request.Body.Should().Contain("\"background_color\":\"#00FF00\"");
        _ = request.Body.Should().Contain("\"is_enabled\":true");
        _ = request.Body.Should().Contain("\"is_user_input_required\":true");
        _ = request.Body.Should().Contain("\"is_max_per_stream_enabled\":true");
        _ = request.Body.Should().Contain("\"max_per_stream\":10");
        _ = request.Body.Should().Contain("\"is_max_per_user_per_stream_enabled\":true");
        _ = request.Body.Should().Contain("\"max_per_user_per_stream\":5");
        _ = request.Body.Should().Contain("\"is_global_cooldown_enabled\":true");
        _ = request.Body.Should().Contain("\"global_cooldown_seconds\":60");
        _ = request.Body.Should().Contain("\"should_redemptions_skip_request_queue\":false");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> sends a POST request with only required parameters and omits optional fields.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenRequiredParametersOnly_OmitsOptionalFieldsFromBody()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");

        this._fixture.RegisterJsonResponse("POST", "/helix/channel_points/custom_rewards", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = rewardGuid,
                    title = "Reward",
                    cost = 100
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "Reward", Cost = 100 };
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("\"title\":\"Reward\"");
        _ = request.Body.Should().Contain("\"cost\":100");
        _ = request.Body.Should().NotContain("prompt");
        _ = request.Body.Should().NotContain("background_color");
        _ = request.Body.Should().NotContain("is_enabled");
        _ = request.Body.Should().NotContain("is_user_input_required");
        _ = request.Body.Should().NotContain("is_max_per_stream_enabled");
        _ = request.Body.Should().NotContain("max_per_stream");
        _ = request.Body.Should().NotContain("is_max_per_user_per_stream_enabled");
        _ = request.Body.Should().NotContain("max_per_user_per_stream");
        _ = request.Body.Should().NotContain("is_global_cooldown_enabled");
        _ = request.Body.Should().NotContain("global_cooldown_seconds");
        _ = request.Body.Should().NotContain("should_redemptions_skip_request_queue");
    }

    #endregion CreateCustomReward

    #region UpdateCustomReward

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ChannelPointsRewardUpdateParameters parameters = new() { Title = "Updated" };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(null!, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "UpdateCustomReward")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCustomReward_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Title = "Updated" };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, invalidId!, Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when id is empty.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Title = "Updated" };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.Empty, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when Title is not null but empty or whitespace.
    /// </summary>
    /// <param name="invalidTitle">The whitespace title to test.</param>
    [Theory]
    [Trait("Member", "UpdateCustomReward")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCustomReward_WhenTitleIsWhitespace_ThrowsArgumentNullException(string invalidTitle)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Title = invalidTitle };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when Title has more than 45 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenTitleExceeds45Characters_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Title = new string('a', 46) };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when Cost is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenCostIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Cost = 0 };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Cost");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when Prompt has more than 200 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenPromptExceeds200Characters_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Prompt = new string('a', 201) };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Prompt");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="InvalidOperationException"/> when BackgroundColor is not a valid hex color.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenBackgroundColorIsInvalidHex_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { BackgroundColor = "not-hex" };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("BackgroundColor must be a valid hex triplet (#RRGGBB)");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when IsMaxPerStreamEnabled and MaxPerStream mismatch.
    /// </summary>
    /// <param name="isEnabled">Whether max per stream is enabled.</param>
    /// <param name="maxPerStream">The max per stream amount.</param>
    [Theory]
    [Trait("Member", "UpdateCustomReward")]
    [InlineData(true, null)]
    [InlineData(null, 10)]
    public async Task UpdateCustomReward_WhenMaxPerStreamMismatch_ThrowsArgumentNullException(bool? isEnabled, int? maxPerStream)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            IsMaxPerStreamEnabled = isEnabled,
            MaxPerStream = maxPerStream
        };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsMaxPerStreamEnabled,MaxPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when MaxPerStream is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenMaxPerStreamIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            IsMaxPerStreamEnabled = true,
            MaxPerStream = 0
        };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("MaxPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when IsMaxPerUserPerStreamEnabled and MaxPerUserPerStream mismatch.
    /// </summary>
    /// <param name="isEnabled">Whether max per user per stream is enabled.</param>
    /// <param name="maxPerUser">The max per user per stream amount.</param>
    [Theory]
    [Trait("Member", "UpdateCustomReward")]
    [InlineData(true, null)]
    [InlineData(null, 5)]
    public async Task UpdateCustomReward_WhenMaxPerUserPerStreamMismatch_ThrowsArgumentNullException(bool? isEnabled, int? maxPerUser)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            IsMaxPerUserPerStreamEnabled = isEnabled,
            MaxPerUserPerStream = maxPerUser
        };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsMaxPerUserPerStreamEnabled,MaxPerUserPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when MaxPerUserPerStream is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenMaxPerUserPerStreamIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            IsMaxPerUserPerStreamEnabled = true,
            MaxPerUserPerStream = 0
        };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("MaxPerUserPerStream");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentNullException"/> when IsGlobalCooldownEnabled and GlobalCooldownSeconds mismatch.
    /// </summary>
    /// <param name="isEnabled">Whether global cooldown is enabled.</param>
    /// <param name="cooldownSeconds">The cooldown seconds.</param>
    [Theory]
    [Trait("Member", "UpdateCustomReward")]
    [InlineData(true, null)]
    [InlineData(null, 60)]
    public async Task UpdateCustomReward_WhenGlobalCooldownMismatch_ThrowsArgumentNullException(bool? isEnabled, int? cooldownSeconds)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            IsGlobalCooldownEnabled = isEnabled,
            GlobalCooldownSeconds = cooldownSeconds
        };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsGlobalCooldownEnabled,GlobalCooldownSeconds");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="ArgumentOutOfRangeException"/> when GlobalCooldownSeconds is less than 1.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenGlobalCooldownSecondsIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            IsGlobalCooldownEnabled = true,
            GlobalCooldownSeconds = 0
        };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("GlobalCooldownSeconds");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="InvalidOperationException"/> when no update parameters are set.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenNoParametersSet_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new();
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("must specify at least one parameter to update");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ChannelPointsRewardUpdateParameters parameters = new() { Title = "Updated" };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        ChannelPointsRewardUpdateParameters parameters = new() { Title = "Updated" };
        Func<Task> action = async () => await ChannelPointsReward.UpdateCustomReward(session, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> sends a PATCH request and returns updated reward.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenValidParameters_UpdatesAndReturnsCustomReward()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");
        DateTime cooldownExpiresAt = new(2020, 7, 1, 19, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("PATCH", "/helix/channel_points/custom_rewards", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = rewardGuid,
                    title = "Updated Title",
                    prompt = "updated prompt",
                    cost = 200,
                    image = new
                    {
                        url_1x = "https://example.com/custom/1x.png",
                        url_2x = "https://example.com/custom/2x.png",
                        url_4x = "https://example.com/custom/4x.png"
                    },
                    default_image = new
                    {
                        url_1x = "https://example.com/default/1x.png",
                        url_2x = "https://example.com/default/2x.png",
                        url_4x = "https://example.com/default/4x.png"
                    },
                    background_color = "#FF0000",
                    is_enabled = true,
                    is_user_input_required = true,
                    max_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_stream = 10
                    },
                    max_per_user_per_stream_setting = new
                    {
                        is_enabled = true,
                        max_per_user_per_stream = 5
                    },
                    global_cooldown_setting = new
                    {
                        is_enabled = true,
                        global_cooldown_seconds = 60
                    },
                    is_paused = true,
                    is_in_stock = true,
                    should_redemptions_skip_request_queue = true,
                    redemptions_redeemed_current_stream = 2,
                    cooldown_expires_at = cooldownExpiresAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new()
        {
            Title = "Updated Title",
            Cost = 200,
            Prompt = "updated prompt",
            IsEnabled = true,
            BackgroundColor = "#ff0000",
            IsUserInputRequired = true,
            IsMaxPerStreamEnabled = true,
            MaxPerStream = 10,
            IsMaxPerUserPerStreamEnabled = true,
            MaxPerUserPerStream = 5,
            IsGlobalCooldownEnabled = true,
            GlobalCooldownSeconds = 60,
            IsPaused = true,
            ShouldRedemptionsSkipRequestQueue = true
        };
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.UpdateCustomReward(session, "274637212", rewardGuid, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChannelPointsReward reward = response.Data[0];
        _ = reward.BroadcasterId.Should().Be("274637212");
        _ = reward.BroadcasterLogin.Should().Be("torpedo09");
        _ = reward.BroadcasterName.Should().Be("torpedo09");
        _ = reward.Id.Should().Be(rewardGuid);
        _ = reward.Title.Should().Be("Updated Title");
        _ = reward.Prompt.Should().Be("updated prompt");
        _ = reward.Cost.Should().Be(200);
        _ = reward.Image.Should().NotBeNull();
        _ = reward.Image.Url1X.Should().Be(new Uri("https://example.com/custom/1x.png"));
        _ = reward.Image.Url2X.Should().Be(new Uri("https://example.com/custom/2x.png"));
        _ = reward.Image.Url4X.Should().Be(new Uri("https://example.com/custom/4x.png"));
        _ = reward.DefaultImage.Should().NotBeNull();
        _ = reward.DefaultImage.Url1X.Should().Be(new Uri("https://example.com/default/1x.png"));
        _ = reward.DefaultImage.Url2X.Should().Be(new Uri("https://example.com/default/2x.png"));
        _ = reward.DefaultImage.Url4X.Should().Be(new Uri("https://example.com/default/4x.png"));
        _ = reward.BackgroundColorString.Should().Be("#FF0000");
        _ = reward.BackgroundColor.Should().Be(Color.FromArgb(255, 0, 0));
        _ = reward.IsEnabled.Should().BeTrue();
        _ = reward.IsUserInputRequired.Should().BeTrue();
        _ = reward.MaxPerStreamSetting.Should().NotBeNull();
        _ = reward.MaxPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = reward.MaxPerStreamSetting.MaxPerStream.Should().Be(10);
        _ = reward.MaxPerUserPerStreamSetting.Should().NotBeNull();
        _ = reward.MaxPerUserPerStreamSetting.IsEnabled.Should().BeTrue();
        _ = reward.MaxPerUserPerStreamSetting.MaxPerUserPerStream.Should().Be(5);
        _ = reward.GlobalCooldownSetting.Should().NotBeNull();
        _ = reward.GlobalCooldownSetting.IsEnabled.Should().BeTrue();
        _ = reward.GlobalCooldownSetting.GlobalCooldownSeconds.Should().Be(60);
        _ = reward.GlobalCooldownSetting.GlobalCooldown.Should().Be(TimeSpan.FromSeconds(60));
        _ = reward.IsPaused.Should().BeTrue();
        _ = reward.IsInStock.Should().BeTrue();
        _ = reward.ShouldRedemptionsSkipRequestQueue.Should().BeTrue();
        _ = reward.RedemptionsRedeemedCurrentStream.Should().Be(2);
        _ = reward.CooldownExpiresAt.Should().Be(cooldownExpiresAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"id={rewardGuid:D}");
        _ = request.Body.Should().Contain("\"title\":\"Updated Title\"");
        _ = request.Body.Should().Contain("\"cost\":200");
        _ = request.Body.Should().Contain("\"prompt\":\"updated prompt\"");
        _ = request.Body.Should().Contain("\"background_color\":\"#FF0000\"");
        _ = request.Body.Should().Contain("\"is_enabled\":true");
        _ = request.Body.Should().Contain("\"is_user_input_required\":true");
        _ = request.Body.Should().Contain("\"is_max_per_stream_enabled\":true");
        _ = request.Body.Should().Contain("\"max_per_stream\":10");
        _ = request.Body.Should().Contain("\"is_max_per_user_per_stream_enabled\":true");
        _ = request.Body.Should().Contain("\"max_per_user_per_stream\":5");
        _ = request.Body.Should().Contain("\"is_global_cooldown_enabled\":true");
        _ = request.Body.Should().Contain("\"global_cooldown_seconds\":60");
        _ = request.Body.Should().Contain("\"is_paused\":true");
        _ = request.Body.Should().Contain("\"should_redemptions_skip_request_queue\":true");
    }

    #endregion UpdateCustomReward

    #region DeleteCustomReward

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.DeleteCustomReward"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteCustomReward")]
    public async Task DeleteCustomReward_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelPointsReward.DeleteCustomReward(null!, "274637212", Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.DeleteCustomReward"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "DeleteCustomReward")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteCustomReward_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        Func<Task> action = async () => await ChannelPointsReward.DeleteCustomReward(session, invalidId!, Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.DeleteCustomReward"/> throws <see cref="ArgumentNullException"/> when id is empty.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteCustomReward")]
    public async Task DeleteCustomReward_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        Func<Task> action = async () => await ChannelPointsReward.DeleteCustomReward(session, "274637212", Guid.Empty).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.DeleteCustomReward"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteCustomReward")]
    public async Task DeleteCustomReward_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ChannelPointsReward.DeleteCustomReward(session, "274637212", Guid.NewGuid()).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsReward.DeleteCustomReward"/> sends a DELETE request and returns success.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteCustomReward")]
    public async Task DeleteCustomReward_WhenValidParameters_DeletesCustomReward()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");
        this._fixture.RegisterStatusCode("DELETE", "/helix/channel_points/custom_rewards", (int)HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        JsonApiResponse? response = await ChannelPointsReward.DeleteCustomReward(session, "274637212", rewardGuid).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"id={rewardGuid:D}");
    }

    #endregion DeleteCustomReward
}

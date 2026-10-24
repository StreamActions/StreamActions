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
using StreamActions.Twitch.Api.ChannelPoints;
using StreamActions.Twitch.Api.ChannelPoints.Objects;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.ChannelPoints;

/// <summary>
/// Provides unit and integration tests for <see cref="ChannelPointsRedemption"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelPointsRedemptionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelPointsRedemptionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChannelPointsRedemptionTests(TwitchKestrelServerFixture fixture)
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

    #region GetCustomRewardRedemption

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(null!, "274637212", Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetCustomRewardRedemption")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCustomRewardRedemption_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, invalidId!, Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="ArgumentNullException"/> when rewardId is empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenRewardIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, "274637212", Guid.Empty).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("rewardId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="ArgumentOutOfRangeException"/> when id count exceeds 50.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenIdCountExceeds50_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        List<Guid> ids = Enumerable.Range(1, 51).Select(_ => Guid.NewGuid()).ToList();
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, "274637212", Guid.NewGuid(), id: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="ArgumentOutOfRangeException"/> when status is not defined.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenStatusIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, "274637212", Guid.NewGuid(), status: (ChannelPointsRedemption.RedemptionStatus)999).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("status");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="ArgumentOutOfRangeException"/> when sort is not defined.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenSortIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, "274637212", Guid.NewGuid(), sort: (ChannelPointsRedemption.RedemptionSort)999).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("sort");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, "274637212", Guid.NewGuid()).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ChannelPointsRedemption.GetCustomRewardRedemption(session, "274637212", Guid.NewGuid()).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.GetCustomRewardRedemption"/> sends a GET request and returns redemptions data.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCustomRewardRedemption")]
    public async Task GetCustomRewardRedemption_WhenValidParameters_ReturnsRedemptions()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");
        Guid redemptionGuid = Guid.Parse("17fa2df1-ad76-4804-bfa5-a40ef63efe63");
        DateTime redeemedAt = new(2020, 7, 1, 18, 37, 32, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/channel_points/custom_rewards/redemptions", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = redemptionGuid,
                    user_id = "274637212",
                    user_name = "torpedo09",
                    user_login = "torpedo09",
                    user_input = "",
                    status = "CANCELED",
                    redeemed_at = redeemedAt,
                    reward = new
                    {
                        id = rewardGuid,
                        title = "game analysis",
                        prompt = "",
                        cost = 50000
                    }
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        ResponseData<ChannelPointsReward>? response = await ChannelPointsRedemption.GetCustomRewardRedemption(
            session,
            "274637212",
            rewardGuid,
            ChannelPointsRedemption.RedemptionStatus.Canceled).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards/redemptions");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"reward_id={rewardGuid:D}");
    }

    #endregion GetCustomRewardRedemption

    #region UpdateRedemptionStatus

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(null!, [Guid.NewGuid()], "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "UpdateRedemptionStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateRedemptionStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, [Guid.NewGuid()], invalidId!, Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentNullException"/> when rewardId is empty.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenRewardIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, [Guid.NewGuid()], "274637212", Guid.Empty, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("rewardId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentNullException"/> when id is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenIdIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, null!, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, [Guid.NewGuid()], "274637212", Guid.NewGuid(), null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentOutOfRangeException"/> when id count exceeds 50.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenIdCountExceeds50_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        List<Guid> ids = Enumerable.Range(1, 51).Select(_ => Guid.NewGuid()).ToList();
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, ids, "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="ArgumentOutOfRangeException"/> when status is not Fulfilled or Canceled.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenStatusIsNotFulfilledOrCanceled_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Unfulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, [Guid.NewGuid()], "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Status");
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        Func<Task> action = async () => await ChannelPointsRedemption.UpdateRedemptionStatus(session, [Guid.NewGuid()], "274637212", Guid.NewGuid(), parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> sends a PATCH request and returns updated redemption data.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateRedemptionStatus")]
    public async Task UpdateRedemptionStatus_WhenValidParameters_PatchesRedemptionAndReturnsUpdatedRedemptions()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");
        Guid redemptionGuid = Guid.Parse("17fa2df1-ad76-4804-bfa5-a40ef63efe63");

        this._fixture.RegisterJsonResponse("PATCH", "/helix/channel_points/custom_rewards/redemptions", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_name = "torpedo09",
                    broadcaster_login = "torpedo09",
                    broadcaster_id = "274637212",
                    id = redemptionGuid,
                    status = "FULFILLED"
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRedemptionUpdateParameters parameters = new() { Status = ChannelPointsRedemption.RedemptionStatus.Fulfilled };
        ResponseData<ChannelPointsReward>? response = await ChannelPointsRedemption.UpdateRedemptionStatus(
            session,
            [redemptionGuid],
            "274637212",
            rewardGuid,
            parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards/redemptions");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"reward_id={rewardGuid:D}");
        _ = request.QueryString.Should().Contain($"id={redemptionGuid:D}");
        _ = request.Body.Should().Contain("\"status\":\"FULFILLED\"");
    }

    #endregion UpdateRedemptionStatus
}

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
                    prompt = "",
                    cost = 50000,
                    background_color = "#00FF00",
                    is_enabled = true
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadRedemptions);
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.GetCustomReward(session, "274637212", [rewardGuid], true).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChannelPointsReward reward = response.Data[0];
        _ = reward.Id.Should().Be(rewardGuid);
        _ = reward.Title.Should().Be("game analysis");
        _ = reward.Cost.Should().Be(50000);
        _ = reward.BackgroundColorString.Should().Be("#00FF00");
        _ = reward.BackgroundColor.Should().Be(Color.FromArgb(0, 255, 0));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"id={rewardGuid:D}");
        _ = request.QueryString.Should().Contain("only_manageable_rewards=true");
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
    /// Tests that <see cref="ChannelPointsReward.CreateCustomReward"/> sends a POST request and returns created reward.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateCustomReward")]
    public async Task CreateCustomReward_WhenValidParameters_CreatesAndReturnsCustomReward()
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
                    title = "New Reward",
                    cost = 100
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardCreationParameters parameters = new() { Title = "New Reward", Cost = 100 };
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.CreateCustomReward(session, "274637212", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.Body.Should().Contain("\"title\":\"New Reward\"");
        _ = request.Body.Should().Contain("\"cost\":100");
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
    /// Tests that <see cref="ChannelPointsReward.UpdateCustomReward"/> sends a PATCH request and returns updated reward.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateCustomReward")]
    public async Task UpdateCustomReward_WhenValidParameters_UpdatesAndReturnsCustomReward()
    {
        Guid rewardGuid = Guid.Parse("92af127c-7326-4483-a52b-b0da0be61c01");

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
                    cost = 200
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRedemptions);
        ChannelPointsRewardUpdateParameters parameters = new() { Title = "Updated Title", Cost = 200 };
        ResponseData<ChannelPointsReward>? response = await ChannelPointsReward.UpdateCustomReward(session, "274637212", rewardGuid, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/channel_points/custom_rewards");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.QueryString.Should().Contain($"id={rewardGuid:D}");
        _ = request.Body.Should().Contain("\"title\":\"Updated Title\"");
        _ = request.Body.Should().Contain("\"cost\":200");
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

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

using StreamActions.Twitch.Api.Goals;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Goals;

/// <summary>
/// Performs unit and integration testing on <see cref="Goal"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class GoalTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GoalTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public GoalTests(TwitchKestrelServerFixture fixture)
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

    #region GetCreatorGoals

    [Fact]
    [Trait("Member", "GetCreatorGoals")]
    public async Task GetCreatorGoals_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Goal.GetCreatorGoals(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetCreatorGoals")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCreatorGoals_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGoals);
        Func<Task> action = async () => await Goal.GetCreatorGoals(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetCreatorGoals")]
    public async Task GetCreatorGoals_WhenSessionTokenIsNotUser_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Goal.GetCreatorGoals(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetCreatorGoals")]
    public async Task GetCreatorGoals_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await Goal.GetCreatorGoals(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetCreatorGoals")]
    public async Task GetCreatorGoals_WhenValidRequest_SendsGetAndReturnsGoals()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "1woowvbkiNv8BRxEWSqmQz6Zk92",
              "broadcaster_id": "141981764",
              "broadcaster_name": "TwitchDev",
              "broadcaster_login": "twitchdev",
              "type": "follower",
              "description": "Follow goal for Helix testing",
              "current_amount": 27062,
              "target_amount": 30000,
              "created_at": "2021-08-16T17:22:23Z"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/goals", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGoals);
        ResponseData<Goal>? response = await Goal.GetCreatorGoals(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Goal goal = response.Data[0];
        _ = goal.Id.Should().Be("1woowvbkiNv8BRxEWSqmQz6Zk92");
        _ = goal.BroadcasterId.Should().Be("141981764");
        _ = goal.BroadcasterName.Should().Be("TwitchDev");
        _ = goal.BroadcasterLogin.Should().Be("twitchdev");
        _ = goal.Type.Should().Be(Goal.GoalType.Follower);
        _ = goal.Description.Should().Be("Follow goal for Helix testing");
        _ = goal.CurrentAmount.Should().Be(27062);
        _ = goal.TargetAmount.Should().Be(30000);
        _ = goal.CreatedAt.Should().Be(DateTime.Parse("2021-08-16T17:22:23Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/goals");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    /// <summary>
    /// Verifies that <see cref="Goal.GetCreatorGoals(TwitchSession, string)"/> correctly deserializes each <see cref="Goal.GoalType"/> value.
    /// </summary>
    /// <param name="typeString">The serialized type string returned by the API.</param>
    /// <param name="expectedType">The expected deserialized <see cref="Goal.GoalType"/> value.</param>
    [Theory]
    [Trait("Member", "GetCreatorGoals")]
    [InlineData("follower", Goal.GoalType.Follower)]
    [InlineData("subscription", Goal.GoalType.Subscription)]
    [InlineData("subscription_count", Goal.GoalType.SubscriptionCount)]
    [InlineData("new_subscription", Goal.GoalType.NewSubscription)]
    [InlineData("new_subscription_count", Goal.GoalType.NewSubscriptionCount)]
    [InlineData("new_bit", Goal.GoalType.NewBit)]
    [InlineData("new_cheerer", Goal.GoalType.NewCheerer)]
    public async Task GetCreatorGoals_WhenDifferentGoalTypesReturned_DeserializesCorrectly(string typeString, Goal.GoalType expectedType)
    {
        string jsonResponse = $$"""
        {
          "data": [
            {
              "id": "1woowvbkiNv8BRxEWSqmQz6Zk92",
              "broadcaster_id": "141981764",
              "broadcaster_name": "TwitchDev",
              "broadcaster_login": "twitchdev",
              "type": "{{typeString}}",
              "description": "Follow goal for Helix testing",
              "current_amount": 27062,
              "target_amount": 30000,
              "created_at": "2021-08-16T17:22:23Z"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/goals", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGoals);
        ResponseData<Goal>? response = await Goal.GetCreatorGoals(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);
        _ = response.Data[0].Type.Should().Be(expectedType);
    }

    #endregion GetCreatorGoals
}

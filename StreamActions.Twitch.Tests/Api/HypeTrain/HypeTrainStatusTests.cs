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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.HypeTrain;
using StreamActions.Twitch.Api.HypeTrain.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using HypeTrainModel = StreamActions.Twitch.Api.HypeTrain.HypeTrain;

namespace StreamActions.Twitch.Tests.Api.HypeTrain;

/// <summary>
/// Performs unit and integration testing on <see cref="HypeTrainStatus"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class HypeTrainStatusTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HypeTrainStatusTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public HypeTrainStatusTests(TwitchKestrelServerFixture fixture)
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

    #region GetHypeTrainStatus

    [Fact]
    [Trait("Member", "GetHypeTrainStatus")]
    public async Task GetHypeTrainStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await HypeTrainStatus.GetHypeTrainStatus(null!, "1337").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetHypeTrainStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetHypeTrainStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadHypeTrain);
        Func<Task> action = async () => await HypeTrainStatus.GetHypeTrainStatus(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetHypeTrainStatus")]
    public async Task GetHypeTrainStatus_WhenTokenIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await HypeTrainStatus.GetHypeTrainStatus(session, "1337").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetHypeTrainStatus")]
    public async Task GetHypeTrainStatus_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await HypeTrainStatus.GetHypeTrainStatus(session, "1337").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetHypeTrainStatus")]
    public async Task GetHypeTrainStatus_WhenValidRequest_SendsGetAndReturnsHypeTrainStatus()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "current": {
                "id": "1b0AsbInCHZW2SQFQkCzqN07Ib2",
                "broadcaster_id": "1337",
                "broadcaster_login": "cool_user",
                "broadcaster_name": "Cool_User",
                "level": 2,
                "total": 700,
                "progress": 200,
                "goal": 1000,
                "top_contributions": [
                  {
                    "user_id": "123",
                    "user_login": "pogchamp",
                    "user_name": "PogChamp",
                    "type": "BITS",
                    "total": 50
                  },
                  {
                    "user_id": "456",
                    "user_login": "kappa",
                    "user_name": "Kappa",
                    "type": "SUBS",
                    "total": 45
                  }
                ],
                "shared_train_participants": [
                  {
                    "broadcaster_id": "456",
                    "broadcaster_login": "pogchamp",
                    "broadcaster_name": "PogChamp"
                  }
                ],
                "started_at": "2020-07-15T17:16:03.1710671Z",
                "expires_at": "2020-07-15T17:16:11.1710671Z",
                "type": "golden_kappa",
                "is_shared_train": true
              },
              "all_time_high": {
                "level": 6,
                "total": 2850,
                "achieved_at": "2020-04-24T20:12:21.0038022Z"
              },
              "shared_all_time_high": {
                "level": 16,
                "total": 23850,
                "achieved_at": "2020-04-27T20:12:21.0038022Z"
              }
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/hypetrain/status", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadHypeTrain);
        ResponseData<HypeTrainStatus>? response = await HypeTrainStatus.GetHypeTrainStatus(session, "1337").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        HypeTrainStatus status = response.Data[0];
        _ = status.Current.Should().NotBeNull();
        _ = status.Current!.Id.Should().Be("1b0AsbInCHZW2SQFQkCzqN07Ib2");
        _ = status.Current.BroadcasterId.Should().Be("1337");
        _ = status.Current.BroadcasterLogin.Should().Be("cool_user");
        _ = status.Current.BroadcasterName.Should().Be("Cool_User");
        _ = status.Current.Level.Should().Be(2);
        _ = status.Current.Total.Should().Be(700);
        _ = status.Current.Progress.Should().Be(200);
        _ = status.Current.Goal.Should().Be(1000);
        _ = status.Current.Type.Should().Be(HypeTrainModel.HypeTrainType.GoldenKappa);
        _ = status.Current.IsSharedTrain.Should().BeTrue();

        _ = status.Current.TopContributions.Should().NotBeNull();
        _ = status.Current.TopContributions!.Count.Should().Be(2);
        _ = status.Current.TopContributions[0].UserId.Should().Be("123");
        _ = status.Current.TopContributions[0].Type.Should().Be(HypeTrainContribution.HypeTrainContributionType.Bits);
        _ = status.Current.TopContributions[0].Total.Should().Be(50);
        _ = status.Current.TopContributions[1].UserId.Should().Be("456");
        _ = status.Current.TopContributions[1].Type.Should().Be(HypeTrainContribution.HypeTrainContributionType.Subscription);
        _ = status.Current.TopContributions[1].Total.Should().Be(45);

        _ = status.Current.SharedTrainParticipants.Should().NotBeNull();
        _ = status.Current.SharedTrainParticipants!.Count.Should().Be(1);
        _ = status.Current.SharedTrainParticipants[0].BroadcasterId.Should().Be("456");

        _ = status.AllTimeHigh.Should().NotBeNull();
        _ = status.AllTimeHigh!.Level.Should().Be(6);
        _ = status.AllTimeHigh.Total.Should().Be(2850);

        _ = status.SharedAllTimeHigh.Should().NotBeNull();
        _ = status.SharedAllTimeHigh!.Level.Should().Be(16);
        _ = status.SharedAllTimeHigh.Total.Should().Be(23850);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/hypetrain/status");
        _ = request.QueryString.Should().Contain("broadcaster_id=1337");
    }

    #endregion GetHypeTrainStatus
}

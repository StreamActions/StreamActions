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
using Microsoft.AspNetCore.Http;
using StreamActions.Common.Exceptions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.GuestStar;
using StreamActions.Twitch.Api.GuestStar.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.GuestStar;

/// <summary>
/// Performs unit and integration testing on <see cref="GuestStarSession"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class GuestStarSessionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GuestStarSessionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public GuestStarSessionTests(TwitchKestrelServerFixture fixture)
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

    #region GetGuestStarSession

    [Fact]
    [Trait("Member", "GetGuestStarSession")]
    public async Task GetGuestStarSession_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSession.GetGuestStarSession(null!, "9321049", "9321049").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetGuestStarSession")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetGuestStarSession_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        Func<Task> action = async () => await GuestStarSession.GetGuestStarSession(session, invalidBroadcasterId!, "9321049").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetGuestStarSession")]
    public async Task GetGuestStarSession_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarSession.GetGuestStarSession(session, "9321049", "9321049").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetGuestStarSession")]
    public async Task GetGuestStarSession_WhenValidRequest_SendsGetAndReturnsSession()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "2KFRQbFtpmfyD3IevNRnCzOPRJI",
              "guests": [
                {
                  "slot_id": "0",
                  "user_id": "9321049",
                  "user_display_name": "Cool_User",
                  "user_login": "cool_user",
                  "is_live": true,
                  "volume": 100,
                  "assigned_at": "2023-01-02T04:16:53.325Z",
                  "audio_settings": {
                    "is_available": true,
                    "is_host_enabled": true,
                    "is_guest_enabled": true
                  },
                  "video_settings": {
                    "is_available": true,
                    "is_host_enabled": true,
                    "is_guest_enabled": true
                  }
                }
              ]
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/guest_star/session", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        ResponseData<GuestStarSession>? response = await GuestStarSession.GetGuestStarSession(session, "9321049", "9321049").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        GuestStarSession starSession = response.Data[0];
        _ = starSession.Id.Should().Be("2KFRQbFtpmfyD3IevNRnCzOPRJI");
        _ = starSession.Guests.Should().NotBeNull();
        _ = starSession.Guests!.Count.Should().Be(1);

        GuestStarGuest guest = starSession.Guests[0];
        _ = guest.SlotId.Should().Be("0");
        _ = guest.UserId.Should().Be("9321049");
        _ = guest.UserDisplayName.Should().Be("Cool_User");
        _ = guest.IsLive.Should().BeTrue();
        _ = guest.Volume.Should().Be(100);
        _ = guest.AudioSettings.Should().NotBeNull();
        _ = guest.AudioSettings!.IsAvailable.Should().BeTrue();
        _ = guest.VideoSettings.Should().NotBeNull();
        _ = guest.VideoSettings!.IsHostEnabled.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/guest_star/session");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
    }

    #endregion GetGuestStarSession

    #region CreateGuestStarSession

    [Fact]
    [Trait("Member", "CreateGuestStarSession")]
    public async Task CreateGuestStarSession_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSession.CreateGuestStarSession(null!, "9321049").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "CreateGuestStarSession")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateGuestStarSession_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSession.CreateGuestStarSession(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "CreateGuestStarSession")]
    public async Task CreateGuestStarSession_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarSession.CreateGuestStarSession(session, "9321049").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CreateGuestStarSession")]
    public async Task CreateGuestStarSession_WhenValidRequest_SendsPostAndReturnsSession()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "2KFRQbFtpmfyD3IevNRnCzOPRJI",
              "guests": [
                {
                  "slot_id": "0",
                  "user_id": "9321049",
                  "user_display_name": "Cool_User",
                  "user_login": "cool_user",
                  "is_live": true,
                  "volume": 100,
                  "assigned_at": "2023-01-02T04:16:53.325Z",
                  "audio_settings": {
                    "is_available": true,
                    "is_host_enabled": true,
                    "is_guest_enabled": true
                  },
                  "video_settings": {
                    "is_available": true,
                    "is_host_enabled": true,
                    "is_guest_enabled": true
                  }
                }
              ]
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("POST", "/helix/guest_star/session", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        ResponseData<GuestStarSession>? response = await GuestStarSession.CreateGuestStarSession(session, "9321049").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        GuestStarSession starSession = response.Data[0];
        _ = starSession.Id.Should().Be("2KFRQbFtpmfyD3IevNRnCzOPRJI");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/guest_star/session");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
    }

    #endregion CreateGuestStarSession

    #region EndGuestStarSession

    [Fact]
    [Trait("Member", "EndGuestStarSession")]
    public async Task EndGuestStarSession_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSession.EndGuestStarSession(null!, "9321049", "session123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "EndGuestStarSession")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EndGuestStarSession_WhenSessionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSessionId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSession.EndGuestStarSession(session, "9321049", invalidSessionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("sessionId");
    }

    [Fact]
    [Trait("Member", "EndGuestStarSession")]
    public async Task EndGuestStarSession_WhenValidRequest_SendsDeleteAndReturnsSession()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "2KFRQbFtpmfyD3IevNRnCzOPRJI",
              "guests": []
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("DELETE", "/helix/guest_star/session", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        ResponseData<GuestStarSession>? response = await GuestStarSession.EndGuestStarSession(session, "9321049", "2KFRQbFtpmfyD3IevNRnCzOPRJI").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        GuestStarSession starSession = response.Data[0];
        _ = starSession.Id.Should().Be("2KFRQbFtpmfyD3IevNRnCzOPRJI");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/guest_star/session");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("session_id=2KFRQbFtpmfyD3IevNRnCzOPRJI");
    }

    #endregion EndGuestStarSession
}

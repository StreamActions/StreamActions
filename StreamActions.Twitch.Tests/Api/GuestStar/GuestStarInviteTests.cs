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
using StreamActions.Twitch.Api.GuestStar;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.GuestStar;

/// <summary>
/// Performs unit and integration testing on <see cref="GuestStarInvite"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class GuestStarInviteTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GuestStarInviteTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public GuestStarInviteTests(TwitchKestrelServerFixture fixture)
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

    #region GetGuestStarInvites

    [Fact]
    [Trait("Member", "GetGuestStarInvites")]
    public async Task GetGuestStarInvites_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarInvite.GetGuestStarInvites(null!, "9321049", "9321049", "session123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetGuestStarInvites")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetGuestStarInvites_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        Func<Task> action = async () => await GuestStarInvite.GetGuestStarInvites(session, invalidBroadcasterId!, "9321049", "session123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetGuestStarInvites")]
    public async Task GetGuestStarInvites_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarInvite.GetGuestStarInvites(session, "9321049", "9321049", "session123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetGuestStarInvites")]
    public async Task GetGuestStarInvites_WhenValidRequest_SendsGetAndReturnsInvites()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "user_id": "144601104",
              "invited_at": "2023-01-02T04:20:59Z",
              "status": "INVITED",
              "is_video_enabled": true,
              "is_audio_enabled": true,
              "is_video_available": true,
              "is_audio_available": true
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/guest_star/invites", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        ResponseData<GuestStarInvite>? response = await GuestStarInvite.GetGuestStarInvites(session, "9321049", "9321049", "session123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        GuestStarInvite invite = response.Data[0];
        _ = invite.UserId.Should().Be("144601104");
        _ = invite.InvitedAt.Should().Be(DateTime.Parse("2023-01-02T04:20:59Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = invite.Status.Should().Be(GuestStarInvite.GuestStarInviteStatus.Invited);
        _ = invite.IsVideoEnabled.Should().BeTrue();
        _ = invite.IsAudioEnabled.Should().BeTrue();
        _ = invite.IsVideoAvailable.Should().BeTrue();
        _ = invite.IsAudioAvailable.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/guest_star/invites");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
        _ = request.QueryString.Should().Contain("session_id=session123");
    }

    #endregion GetGuestStarInvites

    #region SendGuestStarInvite

    [Fact]
    [Trait("Member", "SendGuestStarInvite")]
    public async Task SendGuestStarInvite_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarInvite.SendGuestStarInvite(null!, "9321049", "9321049", "session123", "144601104").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "SendGuestStarInvite")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendGuestStarInvite_WhenGuestIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidGuestId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarInvite.SendGuestStarInvite(session, "9321049", "9321049", "session123", invalidGuestId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("guestId");
    }

    [Fact]
    [Trait("Member", "SendGuestStarInvite")]
    public async Task SendGuestStarInvite_WhenValidRequest_SendsPostAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/guest_star/invites", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        JsonApiResponse? response = await GuestStarInvite.SendGuestStarInvite(session, "9321049", "9321049", "session123", "144601104").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/guest_star/invites");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("guest_id=144601104");
    }

    #endregion SendGuestStarInvite

    #region DeleteGuestStarInvite

    [Fact]
    [Trait("Member", "DeleteGuestStarInvite")]
    public async Task DeleteGuestStarInvite_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarInvite.DeleteGuestStarInvite(null!, "9321049", "9321049", "session123", "144601104").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "DeleteGuestStarInvite")]
    public async Task DeleteGuestStarInvite_WhenValidRequest_SendsDeleteAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/guest_star/invites", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        JsonApiResponse? response = await GuestStarInvite.DeleteGuestStarInvite(session, "9321049", "9321049", "session123", "144601104").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/guest_star/invites");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("guest_id=144601104");
    }

    #endregion DeleteGuestStarInvite
}

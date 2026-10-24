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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.GuestStar;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.GuestStar;

/// <summary>
/// Performs unit and integration testing on <see cref="GuestStarSlot"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class GuestStarSlotTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GuestStarSlotTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public GuestStarSlotTests(TwitchKestrelServerFixture fixture)
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

    #region AssignGuestStarSlot

    [Fact]
    [Trait("Member", "AssignGuestStarSlot")]
    public async Task AssignGuestStarSlot_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(null!, "9321049", "9321049", "session123", "144601104", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "AssignGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignGuestStarSlot_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(session, invalidBroadcasterId!, "9321049", "session123", "144601104", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "AssignGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignGuestStarSlot_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidModeratorId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(session, "9321049", invalidModeratorId!, "session123", "144601104", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Theory]
    [Trait("Member", "AssignGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignGuestStarSlot_WhenSessionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSessionId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(session, "9321049", "9321049", invalidSessionId!, "144601104", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("sessionId");
    }

    [Theory]
    [Trait("Member", "AssignGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignGuestStarSlot_WhenGuestIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidGuestId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(session, "9321049", "9321049", "session123", invalidGuestId!, "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("guestId");
    }

    [Theory]
    [Trait("Member", "AssignGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignGuestStarSlot_WhenSlotIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSlotId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(session, "9321049", "9321049", "session123", "144601104", invalidSlotId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("slotId");
    }

    [Fact]
    [Trait("Member", "AssignGuestStarSlot")]
    public async Task AssignGuestStarSlot_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarSlot.AssignGuestStarSlot(session, "9321049", "9321049", "session123", "144601104", "1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AssignGuestStarSlot")]
    public async Task AssignGuestStarSlot_WhenValidRequest_SendsPostAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/guest_star/slot", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        JsonApiResponse? response = await GuestStarSlot.AssignGuestStarSlot(session, "9321049", "9321049", "session123", "144601104", "1").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/guest_star/slot");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
        _ = request.QueryString.Should().Contain("session_id=session123");
        _ = request.QueryString.Should().Contain("guest_id=144601104");
        _ = request.QueryString.Should().Contain("slot_id=1");
    }

    #endregion AssignGuestStarSlot

    #region UpdateGuestStarSlot

    [Fact]
    [Trait("Member", "UpdateGuestStarSlot")]
    public async Task UpdateGuestStarSlot_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlot(null!, "9321049", "9321049", "session123", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "UpdateGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateGuestStarSlot_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlot(session, invalidBroadcasterId!, "9321049", "session123", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "UpdateGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateGuestStarSlot_WhenSourceSlotIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSourceSlotId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlot(session, "9321049", "9321049", "session123", invalidSourceSlotId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("sourceSlotId");
    }

    [Fact]
    [Trait("Member", "UpdateGuestStarSlot")]
    public async Task UpdateGuestStarSlot_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlot(session, "9321049", "9321049", "session123", "1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "UpdateGuestStarSlot")]
    public async Task UpdateGuestStarSlot_WhenValidRequest_SendsPatchAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/guest_star/slot", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageGuestStar);
        JsonApiResponse? response = await GuestStarSlot.UpdateGuestStarSlot(session, "9321049", "9321049", "session123", "1", "2").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/guest_star/slot");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
        _ = request.QueryString.Should().Contain("session_id=session123");
        _ = request.QueryString.Should().Contain("source_slot_id=1");
        _ = request.QueryString.Should().Contain("destination_slot_id=2");
    }

    #endregion UpdateGuestStarSlot

    #region DeleteGuestStarSlot

    [Fact]
    [Trait("Member", "DeleteGuestStarSlot")]
    public async Task DeleteGuestStarSlot_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSlot.DeleteGuestStarSlot(null!, "9321049", "9321049", "session123", "144601104", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "DeleteGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteGuestStarSlot_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.DeleteGuestStarSlot(session, invalidBroadcasterId!, "9321049", "session123", "144601104", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "DeleteGuestStarSlot")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteGuestStarSlot_WhenGuestIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidGuestId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.DeleteGuestStarSlot(session, "9321049", "9321049", "session123", invalidGuestId!, "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("guestId");
    }

    [Fact]
    [Trait("Member", "DeleteGuestStarSlot")]
    public async Task DeleteGuestStarSlot_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarSlot.DeleteGuestStarSlot(session, "9321049", "9321049", "session123", "144601104", "1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "DeleteGuestStarSlot")]
    public async Task DeleteGuestStarSlot_WhenValidRequest_SendsDeleteAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/guest_star/slot", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        JsonApiResponse? response = await GuestStarSlot.DeleteGuestStarSlot(session, "9321049", "9321049", "session123", "144601104", "1", shouldReinviteGuest: true).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/guest_star/slot");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
        _ = request.QueryString.Should().Contain("session_id=session123");
        _ = request.QueryString.Should().Contain("guest_id=144601104");
        _ = request.QueryString.Should().Contain("slot_id=1");
        _ = request.QueryString.Should().Contain("should_reinvite_guest=true");
    }

    #endregion DeleteGuestStarSlot

    #region UpdateGuestStarSlotSettings

    [Fact]
    [Trait("Member", "UpdateGuestStarSlotSettings")]
    public async Task UpdateGuestStarSlotSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlotSettings(null!, "9321049", "9321049", "session123", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "UpdateGuestStarSlotSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateGuestStarSlotSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlotSettings(session, invalidBroadcasterId!, "9321049", "session123", "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "UpdateGuestStarSlotSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateGuestStarSlotSettings_WhenSlotIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSlotId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlotSettings(session, "9321049", "9321049", "session123", invalidSlotId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("slotId");
    }

    [Fact]
    [Trait("Member", "UpdateGuestStarSlotSettings")]
    public async Task UpdateGuestStarSlotSettings_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await GuestStarSlot.UpdateGuestStarSlotSettings(session, "9321049", "9321049", "session123", "1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "UpdateGuestStarSlotSettings")]
    public async Task UpdateGuestStarSlotSettings_WhenValidRequest_SendsPatchAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/guest_star/slot_settings", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        JsonApiResponse? response = await GuestStarSlot.UpdateGuestStarSlotSettings(
            session,
            "9321049",
            "9321049",
            "session123",
            "1",
            isAudioEnabled: true,
            isVideoEnabled: false,
            isLive: true,
            volume: 80).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/guest_star/slot_settings");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
        _ = request.QueryString.Should().Contain("session_id=session123");
        _ = request.QueryString.Should().Contain("slot_id=1");
        _ = request.QueryString.Should().Contain("is_audio_enabled=true");
        _ = request.QueryString.Should().Contain("is_video_enabled=false");
        _ = request.QueryString.Should().Contain("is_live=true");
        _ = request.QueryString.Should().Contain("volume=80");
    }

    #endregion UpdateGuestStarSlotSettings
}

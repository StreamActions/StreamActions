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
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.GuestStar;

/// <summary>
/// Performs unit and integration testing on <see cref="ChannelGuestStarSettings"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelGuestStarSettingsTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelGuestStarSettingsTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChannelGuestStarSettingsTests(TwitchKestrelServerFixture fixture)
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

    #region GetChannelGuestStarSettings

    [Fact]
    [Trait("Member", "GetChannelGuestStarSettings")]
    public async Task GetChannelGuestStarSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelGuestStarSettings.GetChannelGuestStarSettings(null!, "9321049", "9321049").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetChannelGuestStarSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelGuestStarSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        Func<Task> action = async () => await ChannelGuestStarSettings.GetChannelGuestStarSettings(session, invalidBroadcasterId!, "9321049").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "GetChannelGuestStarSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelGuestStarSettings_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidModeratorId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        Func<Task> action = async () => await ChannelGuestStarSettings.GetChannelGuestStarSettings(session, "9321049", invalidModeratorId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "GetChannelGuestStarSettings")]
    public async Task GetChannelGuestStarSettings_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await ChannelGuestStarSettings.GetChannelGuestStarSettings(session, "9321049", "9321049").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetChannelGuestStarSettings")]
    public async Task GetChannelGuestStarSettings_WhenValidRequest_SendsGetAndReturnsSettings()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "is_moderator_send_live_enabled": true,
              "slot_count": 4,
              "is_browser_source_audio_enabled": true,
              "group_layout": "TILED_LAYOUT",
              "browser_source_token": "eihq8rew7q3hgierufhi3q"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/guest_star/channel_settings", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadGuestStar);
        ResponseData<ChannelGuestStarSettings>? response = await ChannelGuestStarSettings.GetChannelGuestStarSettings(session, "9321049", "9321049").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChannelGuestStarSettings settings = response.Data[0];
        _ = settings.IsModeratorSendLiveEnabled.Should().BeTrue();
        _ = settings.SlotCount.Should().Be(4);
        _ = settings.IsBrowserSourceAudioEnabled.Should().BeTrue();
        _ = settings.GroupLayout.Should().Be(ChannelGuestStarSettings.GuestStarGroupLayout.TiledLayout);
        _ = settings.BrowserSourceToken.Should().Be("eihq8rew7q3hgierufhi3q");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/guest_star/channel_settings");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.QueryString.Should().Contain("moderator_id=9321049");
    }

    #endregion GetChannelGuestStarSettings

    #region UpdateChannelGuestStarSettings

    [Fact]
    [Trait("Member", "UpdateChannelGuestStarSettings")]
    public async Task UpdateChannelGuestStarSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ChannelGuestStarSettingsUpdateParameters parameters = new()
        {
            SlotCount = 4,
            IsModeratorSendLiveEnabled = true
        };

        Func<Task> action = async () => await ChannelGuestStarSettings.UpdateChannelGuestStarSettings(null!, "9321049", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "UpdateChannelGuestStarSettings")]
    public async Task UpdateChannelGuestStarSettings_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        Func<Task> action = async () => await ChannelGuestStarSettings.UpdateChannelGuestStarSettings(session, "9321049", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "UpdateChannelGuestStarSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateChannelGuestStarSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        ChannelGuestStarSettingsUpdateParameters parameters = new()
        {
            SlotCount = 4
        };

        Func<Task> action = async () => await ChannelGuestStarSettings.UpdateChannelGuestStarSettings(session, invalidBroadcasterId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "UpdateChannelGuestStarSettings")]
    public async Task UpdateChannelGuestStarSettings_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        ChannelGuestStarSettingsUpdateParameters parameters = new()
        {
            SlotCount = 4
        };

        Func<Task> action = async () => await ChannelGuestStarSettings.UpdateChannelGuestStarSettings(session, "9321049", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "UpdateChannelGuestStarSettings")]
    public async Task UpdateChannelGuestStarSettings_WhenValidRequest_SendsPutAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PUT", "/helix/guest_star/channel_settings", StatusCodes.Status204NoContent);

        ChannelGuestStarSettingsUpdateParameters parameters = new()
        {
            SlotCount = 4,
            IsModeratorSendLiveEnabled = true,
            IsBrowserSourceAudioEnabled = true,
            GroupLayout = ChannelGuestStarSettings.GuestStarGroupLayout.TiledLayout,
            RegenerateBrowserSources = false
        };

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageGuestStar);
        JsonApiResponse? response = await ChannelGuestStarSettings.UpdateChannelGuestStarSettings(session, "9321049", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/guest_star/channel_settings");
        _ = request.QueryString.Should().Contain("broadcaster_id=9321049");
        _ = request.Body.Should().Contain("\"slot_count\":4");
        _ = request.Body.Should().Contain("\"group_layout\":\"TILED_LAYOUT\"");
    }

    #endregion UpdateChannelGuestStarSettings
}

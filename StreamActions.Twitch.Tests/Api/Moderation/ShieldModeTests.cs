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

using StreamActions.Twitch.Api.Moderation;
using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="ShieldMode"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ShieldModeTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ShieldModeTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ShieldModeTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetShieldModeStatus

    [Fact]
    [Trait("Member", "GetShieldModeStatus")]
    public async Task GetShieldModeStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ShieldMode.GetShieldModeStatus(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetShieldModeStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetShieldModeStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadShieldMode);
        Func<Task> action = async () => await ShieldMode.GetShieldModeStatus(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "GetShieldModeStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetShieldModeStatus_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadShieldMode);
        Func<Task> action = async () => await ShieldMode.GetShieldModeStatus(session, "1234", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "GetShieldModeStatus")]
    public async Task GetShieldModeStatus_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorReadShieldMode]
            }
        };

        Func<Task> action = async () => await ShieldMode.GetShieldModeStatus(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetShieldModeStatus")]
    public async Task GetShieldModeStatus_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await ShieldMode.GetShieldModeStatus(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "GetShieldModeStatus")]
    [InlineData("moderator:read:shield_mode")]
    [InlineData("moderator:manage:shield_mode")]
    public async Task GetShieldModeStatus_WhenValidUserToken_ReturnsStatus(string scopeName)
    {
        const string json = "{\"data\":[{\"is_active\":true,\"moderator_id\":\"98765\",\"moderator_login\":\"simplysimple\",\"moderator_name\":\"SimplySimple\",\"last_activated_at\":\"2022-07-26T17:16:03.123Z\"}]}";
        this._fixture.RegisterResponse("GET", "/moderation/shield_mode", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<ShieldMode>? response = await ShieldMode.GetShieldModeStatus(session, "1234", "5678").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        ShieldMode item = response.Data[0];
        _ = item.IsActive.Should().BeTrue();
        _ = item.ModeratorId.Should().Be("98765");
        _ = item.ModeratorLogin.Should().Be("simplysimple");
        _ = item.ModeratorName.Should().Be("SimplySimple");
        _ = item.LastActivatedAt.Should().Be(DateTime.Parse("2022-07-26T17:16:03.123Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/shield_mode");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
    }

    [Fact]
    [Trait("Member", "GetShieldModeStatus")]
    public async Task GetShieldModeStatus_WhenAppToken_ReturnsStatus()
    {
        const string json = "{\"data\":[{\"is_active\":false,\"moderator_id\":\"\",\"moderator_login\":\"\",\"moderator_name\":\"\",\"last_activated_at\":\"1970-01-01T00:00:00Z\"}]}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/shield_mode", json);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModeratorReadShieldMode);
        ResponseData<ShieldMode>? response = await ShieldMode.GetShieldModeStatus(session, "1234", "5678").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data[0].IsActive.Should().BeFalse();
    }

    #endregion GetShieldModeStatus

    #region UpdateShieldModeStatus

    [Fact]
    [Trait("Member", "UpdateShieldModeStatus")]
    public async Task UpdateShieldModeStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ShieldModeUpdateParameters parameters = new() { IsActive = true };
        Func<Task> action = async () => await ShieldMode.UpdateShieldModeStatus(null!, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "UpdateShieldModeStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateShieldModeStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShieldMode);
        ShieldModeUpdateParameters parameters = new() { IsActive = true };
        Func<Task> action = async () => await ShieldMode.UpdateShieldModeStatus(session, invalidId!, "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "UpdateShieldModeStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateShieldModeStatus_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShieldMode);
        ShieldModeUpdateParameters parameters = new() { IsActive = true };
        Func<Task> action = async () => await ShieldMode.UpdateShieldModeStatus(session, "1234", invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "UpdateShieldModeStatus")]
    public async Task UpdateShieldModeStatus_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShieldMode);
        Func<Task> action = async () => await ShieldMode.UpdateShieldModeStatus(session, "1234", "5678", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "UpdateShieldModeStatus")]
    public async Task UpdateShieldModeStatus_WhenIsActiveIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShieldMode);
        ShieldModeUpdateParameters parameters = new() { IsActive = null };
        Func<Task> action = async () => await ShieldMode.UpdateShieldModeStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "UpdateShieldModeStatus")]
    public async Task UpdateShieldModeStatus_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadShieldMode);
        ShieldModeUpdateParameters parameters = new() { IsActive = true };
        Func<Task> action = async () => await ShieldMode.UpdateShieldModeStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "UpdateShieldModeStatus")]
    public async Task UpdateShieldModeStatus_WhenValidRequest_PutsStatusAndReturnsUpdated()
    {
        const string json = "{\"data\":[{\"is_active\":false,\"moderator_id\":\"98765\",\"moderator_login\":\"simplysimple\",\"moderator_name\":\"SimplySimple\",\"last_activated_at\":\"2022-07-26T17:16:03.123Z\"}]}";
        this._fixture.RegisterResponse("PUT", "/helix/moderation/shield_mode", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShieldMode);
        ShieldModeUpdateParameters parameters = new() { IsActive = false };
        ResponseData<ShieldMode>? response = await ShieldMode.UpdateShieldModeStatus(session, "1234", "5678", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        ShieldMode item = response.Data[0];
        _ = item.IsActive.Should().BeFalse();
        _ = item.ModeratorId.Should().Be("98765");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/moderation/shield_mode");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.Body.Should().Contain("\"is_active\":false");
    }

    #endregion UpdateShieldModeStatus
}

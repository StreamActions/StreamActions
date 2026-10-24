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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Raids.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Raids;

/// <summary>
/// Performs unit tests on <see cref="Raid"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class RaidTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RaidTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public RaidTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region StartARaid

    [Fact]
    [Trait("Member", "StartARaid")]
    public async Task StartARaid_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Raid.StartARaid(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "StartARaid")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartARaid_WhenFromBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRaids);
        Func<Task> action = async () => await Raid.StartARaid(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("fromBroadcasterId");
    }

    [Theory]
    [Trait("Member", "StartARaid")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartARaid_WhenToBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRaids);
        Func<Task> action = async () => await Raid.StartARaid(session, "1234", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("toBroadcasterId");
    }

    [Fact]
    [Trait("Member", "StartARaid")]
    public async Task StartARaid_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManageRaids);
        Func<Task> action = async () => await Raid.StartARaid(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "StartARaid")]
    public async Task StartARaid_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ChannelManageRaids]
            }
        };

        Func<Task> action = async () => await Raid.StartARaid(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "StartARaid")]
    public async Task StartARaid_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Raid.StartARaid(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "StartARaid")]
    public async Task StartARaid_WhenValidRequest_StartsRaidAndReturnsData()
    {
        const string json = "{\"data\":[{\"created_at\":\"2023-01-01T00:00:00Z\",\"is_mature\":false}]}";
        this._fixture.RegisterResponse("POST", "/helix/raids", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRaids);
        ResponseData<Raid>? response = await Raid.StartARaid(session, "1234", "5678").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Raid raid = response.Data![0];
        _ = raid.CreatedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = raid.IsMature.Should().BeFalse();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/raids");
        _ = request.QueryString.Should().Contain("from_broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("to_broadcaster_id=5678");
    }

    #endregion StartARaid

    #region CancelARaid

    [Fact]
    [Trait("Member", "CancelARaid")]
    public async Task CancelARaid_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Raid.CancelARaid(null!, "1234").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "CancelARaid")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelARaid_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRaids);
        Func<Task> action = async () => await Raid.CancelARaid(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "CancelARaid")]
    public async Task CancelARaid_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManageRaids);
        Func<Task> action = async () => await Raid.CancelARaid(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CancelARaid")]
    public async Task CancelARaid_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Raid.CancelARaid(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CancelARaid")]
    public async Task CancelARaid_WhenValidRequest_DeletesRaidAndReturnsNoContent()
    {
        this._fixture.RegisterResponse("DELETE", "/helix/raids", "", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageRaids);
        JsonApiResponse? response = await Raid.CancelARaid(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/raids");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
    }

    #endregion CancelARaid
}

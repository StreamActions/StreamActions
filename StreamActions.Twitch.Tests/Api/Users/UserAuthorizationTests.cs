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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Users;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Users;

/// <summary>
/// Provides unit tests for the <see cref="UserAuthorization"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class UserAuthorizationTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAuthorizationTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public UserAuthorizationTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetAuthorizationByUser

    /// <summary>
    /// Tests that <see cref="UserAuthorization.GetAuthorizationByUser"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAuthorizationByUser")]
    public async Task GetAuthorizationByUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await UserAuthorization.GetAuthorizationByUser(null!, ["141981764"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="UserAuthorization.GetAuthorizationByUser"/> throws <see cref="ArgumentNullException"/> when userIds is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAuthorizationByUser")]
    public async Task GetAuthorizationByUser_WhenUserIdsIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await UserAuthorization.GetAuthorizationByUser(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userIds");

        Func<Task> actionEmpty = async () => await UserAuthorization.GetAuthorizationByUser(session, []).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userIds");
    }

    /// <summary>
    /// Tests that <see cref="UserAuthorization.GetAuthorizationByUser"/> throws <see cref="ArgumentOutOfRangeException"/> when userIds exceeds 10 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAuthorizationByUser")]
    public async Task GetAuthorizationByUser_WhenUserIdsExceeds10_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> userIds = Enumerable.Range(1, 11).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

        Func<Task> action = async () => await UserAuthorization.GetAuthorizationByUser(session, userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userIds");
    }

    /// <summary>
    /// Tests that <see cref="UserAuthorization.GetAuthorizationByUser"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAuthorizationByUser")]
    public async Task GetAuthorizationByUser_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await UserAuthorization.GetAuthorizationByUser(session, ["141981764"]).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthorization.GetAuthorizationByUser"/> returns authorization details on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetAuthorizationByUser")]
    public async Task GetAuthorizationByUser_WhenRequestSucceeds_ReturnsAuthorization()
    {
        this._fixture.RegisterEndpoint("/helix/authorization/users", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""user_id"": ""141981764"",
      ""user_name"": ""TwitchDev"",
      ""user_login"": ""twitchdev"",
      ""scopes"": [
        ""user:read:email""
      ],
      ""has_authorized"": true
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<UserAuthorization>? response = await UserAuthorization.GetAuthorizationByUser(session, ["141981764"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        UserAuthorization auth = response.Data[0];
        _ = auth.UserId.Should().Be("141981764");
        _ = auth.UserName.Should().Be("TwitchDev");
        _ = auth.UserLogin.Should().Be("twitchdev");
        _ = auth.Scopes.Should().NotBeNull();
        _ = auth.Scopes.Should().Contain(Scope.UserReadEmail);
        _ = auth.HasAuthorized.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/authorization/users");
        _ = request.QueryString.Should().Contain("user_id=141981764");
    }

    #endregion GetAuthorizationByUser
}

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

using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.OAuth;

/// <summary>
/// Performs unit and integration testing on <see cref="UserInfo"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class UserInfoTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public UserInfoTests(TwitchKestrelServerFixture fixture)
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

    #region GetUserInfo

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> throws an <see cref="ArgumentNullException"/> when the session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await UserInfo.GetUserInfo(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> throws a <see cref="TokenTypeException"/> when the session token is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await UserInfo.GetUserInfo(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> throws a <see cref="TwitchScopeMissingException"/> when the session lacks the openid scope.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenSessionMissingOpenIdScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await UserInfo.GetUserInfo(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> throws an <see cref="InvalidOperationException"/> when the session OAuth token is null or whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenOAuthIsNullOrWhitespace_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.OpenID]
            }
        };

        Func<Task> action = async () => await UserInfo.GetUserInfo(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> successfully queries user info and maps all properties when the openid scope is present.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenSessionHasOpenIdScope_GetsUserInfoAndMapsAllProperties()
    {
        const string expectedPicture = "https://static-cdn.jtvnw.net/user-default-pictures-uv/998f01ae-def8-11e9-b95c-784f43822e80-profile_image-150x150.png";
        DateTime expectedUpdated = DateTime.Parse("2022-02-03T16:16:16.968509Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);

        this._fixture.RegisterJsonResponse("GET", "/oauth2/userinfo", new
        {
            aud = this._fixture.ClientId,
            azp = this._fixture.ClientId,
            exp = 1644342250,
            iat = 1644341350,
            iss = this._fixture.BaseUrl,
            sub = "141981764",
            email = "foo@justin.tv",
            email_verified = true,
            picture = expectedPicture,
            preferred_username = "twitchdev",
            updated_at = "2022-02-03T16:16:16.968509Z"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.OpenID);

        UserInfo? userInfo = await UserInfo.GetUserInfo(session).ConfigureAwait(true);

        _ = userInfo.Should().NotBeNull();
        _ = userInfo!.Audience.Should().Be(this._fixture.ClientId);
        _ = userInfo.AuthorizedParty.Should().Be(this._fixture.ClientId);
        _ = userInfo.ExpiresUnix.Should().Be(1644342250);
        _ = userInfo.Expires.Should().Be(DateTime.UnixEpoch.AddSeconds(1644342250));
        _ = userInfo.IssuedUnix.Should().Be(1644341350);
        _ = userInfo.Issued.Should().Be(DateTime.UnixEpoch.AddSeconds(1644341350));
        _ = userInfo.Issuer.Should().Be(this._fixture.BaseUrl);
        _ = userInfo.Subject.Should().Be("141981764");
        _ = userInfo.Email.Should().Be("foo@justin.tv");
        _ = userInfo.EmailVerified.Should().BeTrue();
        _ = userInfo.Picture.Should().Be(expectedPicture);
        _ = userInfo.PictureUri.Should().Be(new Uri(expectedPicture));
        _ = userInfo.PreferredUsername.Should().Be("twitchdev");
        _ = userInfo.UpdatedAt.Should().Be(expectedUpdated);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/oauth2/userinfo");
        _ = this._fixture.LastRequest!.Headers.Authorization.ToString().Should().StartWith("Bearer ");
    }

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> requests user info from an explicit base address URI.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenExplicitBaseAddressProvided_RequestsConfiguredUrlAndReturnsUserInfo()
    {
        this._fixture.RegisterJsonResponse("GET", "/oauth2/userinfo", new
        {
            sub = "141981764"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.OpenID);
        Uri explicitBaseAddress = new($"{this._fixture.BaseUrl}/oauth2/userinfo");

        UserInfo? userInfo = await UserInfo.GetUserInfo(session, explicitBaseAddress.ToString()).ConfigureAwait(true);

        _ = userInfo.Should().NotBeNull();
        _ = userInfo!.Subject.Should().Be("141981764");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/oauth2/userinfo");
    }

    /// <summary>
    /// Tests that <see cref="UserInfo.GetUserInfo(TwitchSession, string?)"/> returns null for calculated and optional properties when omitted from the response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfo")]
    public async Task GetUserInfo_WhenUserInfoPropertiesNull_CalculatedPropertiesReturnNull()
    {
        this._fixture.RegisterJsonResponse("GET", "/oauth2/userinfo", new
        {
            sub = "141981764"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.OpenID);

        UserInfo? userInfo = await UserInfo.GetUserInfo(session).ConfigureAwait(true);

        _ = userInfo.Should().NotBeNull();
        _ = userInfo!.Audience.Should().BeNull();
        _ = userInfo.AuthorizedParty.Should().BeNull();
        _ = userInfo.ExpiresUnix.Should().BeNull();
        _ = userInfo.Expires.Should().BeNull();
        _ = userInfo.IssuedUnix.Should().BeNull();
        _ = userInfo.Issued.Should().BeNull();
        _ = userInfo.Email.Should().BeNull();
        _ = userInfo.EmailVerified.Should().BeNull();
        _ = userInfo.Picture.Should().BeNull();
        _ = userInfo.PictureUri.Should().BeNull();
        _ = userInfo.PreferredUsername.Should().BeNull();
        _ = userInfo.UpdatedAt.Should().BeNull();
    }

    #endregion GetUserInfo
}


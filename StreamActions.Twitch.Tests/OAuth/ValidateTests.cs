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

using Microsoft.AspNetCore.Http;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.OAuth;

/// <summary>
/// Performs unit and integration testing on <see cref="Validate"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ValidateTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// Sample scopes array for OAuth validation response.
    /// </summary>
    private static readonly string[] _validationScopes = ["channel:read:subscriptions"];

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ValidateTests(TwitchKestrelServerFixture fixture)
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

    #region ValidateOAuth

    [Fact]
    [Trait("Member", "ValidateOAuth")]
    public async Task ValidateOAuth_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Validate.ValidateOAuth(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "ValidateOAuth")]
    public async Task ValidateOAuth_WhenTokenIsValid_SendsAuthorizationHeaderAndReturnsValidateObject()
    {
        this._fixture.RegisterJsonResponse("GET", "/oauth2/validate", new
        {
            client_id = "wbmytr93xzw8zbg0p1izqyzzc5mbiz",
            login = "twitchdev",
            scopes = _validationScopes,
            user_id = "141981764",
            expires_in = 5520838
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        string tokenString = session.Token!.OAuth!;

        Validate? result = await Validate.ValidateOAuth(session, $"{this._fixture.BaseUrl}/oauth2/validate").ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result!.ClientId.Should().Be("wbmytr93xzw8zbg0p1izqyzzc5mbiz");
        _ = result.Login.Should().Be("twitchdev");
        _ = result.UserId.Should().Be("141981764");
        _ = result.ExpiresIn.Should().Be(5520838);
        _ = result.Scopes.Should().Equal(["channel:read:subscriptions"]);
        _ = result.Expires.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(5520838), TimeSpan.FromSeconds(5));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Headers["Authorization"].ToString().Should().Be($"Bearer {tokenString}");
    }

    [Fact]
    [Trait("Member", "ValidateOAuth")]
    public async Task ValidateOAuth_WhenTokenIsInvalid_ReturnsErrorDetails()
    {
        this._fixture.RegisterJsonResponse("GET", "/oauth2/validate", new
        {
            status = 401,
            message = "invalid access token"
        }, StatusCodes.Status401Unauthorized);

        using TwitchSession session = TestTokenFactory.CreateUserSession();

        Validate? result = await Validate.ValidateOAuth(session, $"{this._fixture.BaseUrl}/oauth2/validate").ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.Status.Should().Be(HttpStatusCode.Unauthorized);
        _ = result.Message.Should().Be("invalid access token");
        _ = result.ClientId.Should().BeNull();
    }

    #endregion ValidateOAuth
}

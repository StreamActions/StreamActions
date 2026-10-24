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
using StreamActions.Twitch.Api.Extensions;
using StreamActions.Twitch.Api.Extensions.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="ExtensionSecret"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionSecretTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionSecretTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionSecretTests(TwitchKestrelServerFixture fixture)
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

    #region GetExtensionSecrets

    [Fact]
    [Trait("Member", "GetExtensionSecrets")]
    public async Task GetExtensionSecrets_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionSecret.GetExtensionSecrets(null!, "ext123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetExtensionSecrets")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetExtensionSecrets_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionSecret.GetExtensionSecrets(session, invalidExtensionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "GetExtensionSecrets")]
    public async Task GetExtensionSecrets_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionSecret.GetExtensionSecrets(session, "ext123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetExtensionSecrets")]
    public async Task GetExtensionSecrets_WhenValidRequest_SendsGetAndReturnsSecrets()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "format_version": 1,
              "secrets": [
                {
                  "content": "secret_content",
                  "active_at": "2021-03-29T06:58:40.858343Z",
                  "expires_at": "2121-03-05T06:58:40.858343Z"
                }
              ]
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions/jwt/secrets", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        ResponseData<ExtensionSecret>? response = await ExtensionSecret.GetExtensionSecrets(session, "uo6dggojyb8d6soh92zknwmi5ej1q2").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ExtensionSecret secret = response.Data[0];
        _ = secret.FormatVersion.Should().Be(1);
        _ = secret.Secrets.Should().NotBeNull();
        _ = secret.Secrets.Count.Should().Be(1);

        ExtensionSecretData secretData = secret.Secrets[0];
        _ = secretData.Content.Should().Be("secret_content");
        _ = secretData.ActiveAt.Should().Be(DateTime.Parse("2021-03-29T06:58:40.858343Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = secretData.ExpiresAt.Should().Be(DateTime.Parse("2121-03-05T06:58:40.858343Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/extensions/jwt/secrets");
        _ = request.QueryString.Should().Contain("extension_id=uo6dggojyb8d6soh92zknwmi5ej1q2");
    }

    #endregion GetExtensionSecrets

    #region CreateExtensionSecret

    [Fact]
    [Trait("Member", "CreateExtensionSecret")]
    public async Task CreateExtensionSecret_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionSecret.CreateExtensionSecret(null!, "ext123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "CreateExtensionSecret")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateExtensionSecret_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionSecret.CreateExtensionSecret(session, invalidExtensionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "CreateExtensionSecret")]
    public async Task CreateExtensionSecret_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionSecret.CreateExtensionSecret(session, "ext123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CreateExtensionSecret")]
    public async Task CreateExtensionSecret_WhenValidRequest_SendsPostAndReturnsNewSecret()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "format_version": 1,
              "secrets": [
                {
                  "content": "old-secret",
                  "active_at": "2021-03-29T06:58:40.858343Z",
                  "expires_at": "2021-04-22T05:21:54.992616Z"
                },
                {
                  "content": "new-secret",
                  "active_at": "2021-04-22T04:16:54.996365Z",
                  "expires_at": "2121-03-29T04:16:54.996365Z"
                }
              ]
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("POST", "/helix/extensions/jwt/secrets", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        ResponseData<ExtensionSecret>? response = await ExtensionSecret.CreateExtensionSecret(session, "uo6dggojyb8d6soh92zknwmi5ej1q2", 600).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ExtensionSecret secret = response.Data[0];
        _ = secret.FormatVersion.Should().Be(1);
        _ = secret.Secrets.Should().NotBeNull();
        _ = secret.Secrets.Count.Should().Be(2);
        _ = secret.Secrets[1].Content.Should().Be("new-secret");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/extensions/jwt/secrets");
        _ = request.QueryString.Should().Contain("extension_id=uo6dggojyb8d6soh92zknwmi5ej1q2");
        _ = request.QueryString.Should().Contain("delay=600");
    }

    #endregion CreateExtensionSecret
}

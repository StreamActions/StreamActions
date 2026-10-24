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

using Microsoft.Extensions.Logging;
using StreamActions.Common.Extensions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using System.Text.Json;

namespace StreamActions.Twitch.Tests.Api;

/// <summary>
/// Performs unit and integration testing on <see cref="TwitchApi"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class TwitchApiTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchApiTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public TwitchApiTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Disposes test resources and resets server state.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region Author

    [Fact]
    [Trait("Member", "Author")]
    public void Author_ReturnsExpectedAuthor()
    {
        string? author = TwitchApi.Author;
        _ = author.Should().Be("StreamActions Team");
    }

    #endregion Author

    #region ClientId

    [Fact]
    [Trait("Member", "ClientId")]
    public void ClientId_WhenInitialized_ReturnsInitializedClientId()
    {
        string expectedClientId = TestTokenFactory.CreateToken("CLIENT");
        TwitchApi.Init(expectedClientId);
        _ = TwitchApi.ClientId.Should().Be(expectedClientId);
    }

    #endregion ClientId

    #region Description

    [Fact]
    [Trait("Member", "Description")]
    public void Description_ReturnsExpectedDescription()
    {
        string? description = TwitchApi.Description;
        _ = description.Should().Be("Interacts with the Twitch Helix API");
    }

    #endregion Description

    #region Id

    [Fact]
    [Trait("Member", "Id")]
    public void Id_ReturnsExpectedGuid()
    {
        Guid expected = new("996E35EC-638A-4E5B-AEFC-84C800E16520");
        _ = TwitchApi.Id.Should().Be(expected);
    }

    #endregion Id

    #region Name

    [Fact]
    [Trait("Member", "Name")]
    public void Name_ReturnsTwitchApi()
    {
        string? name = TwitchApi.Name;
        _ = name.Should().Be(nameof(TwitchApi));
    }

    #endregion Name

    #region SerializerOptions

    [Fact]
    [Trait("Member", "SerializerOptions")]
    public void SerializerOptions_ReturnsConfiguredOptions()
    {
        JsonSerializerOptions options = TwitchApi.SerializerOptions;
        _ = options.Should().NotBeNull();
        _ = options.Converters.Should().NotBeEmpty();
    }

    #endregion SerializerOptions

    #region Uri

    [Fact]
    [Trait("Member", "Uri")]
    public void Uri_ReturnsExpectedUri()
    {
        Uri expected = new("https://github.com/StreamActions/StreamActions");
        _ = TwitchApi.Uri.Should().Be(expected);
    }

    #endregion Uri

    #region Version

    [Fact]
    [Trait("Member", "Version")]
    public void Version_ReturnsExpectedVersion()
    {
        Version expected = new(1, 0, 0, 0);
        _ = TwitchApi.Version.Should().Be(expected);
    }

    #endregion Version

    #region GetLogger

    [Fact]
    [Trait("Member", "GetLogger")]
    public void GetLogger_ReturnsNonNullLogger()
    {
        ILogger logger = TwitchApi.GetLogger();
        _ = logger.Should().NotBeNull();
    }

    #endregion GetLogger

    #region Init

    [Theory]
    [Trait("Member", "Init")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Init_WhenClientIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidClientId)
    {
        Action action = () => TwitchApi.Init(invalidClientId!);
        _ = action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("clientId");
    }

    [Fact]
    [Trait("Member", "Init")]
    public void Init_WhenCalledWithValidParameters_SetsClientIdAndClientSecret()
    {
        string testClientId = TestTokenFactory.CreateToken("CLIENT");
        string testSecret = TestTokenFactory.CreateToken("SECRET");

        TwitchApi.Init(testClientId, testSecret, $"{this._fixture.BaseUrl}/helix/");

        _ = TwitchApi.ClientId.Should().Be(testClientId);
        _ = TwitchApi.ClientSecret.Should().Be(testSecret);
    }

    #endregion Init

    #region PerformHttpRequest

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenSessionTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken { Type = TwitchToken.TokenType.User }
        };

        Func<Task> action = async () => await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri("users", UriKind.Relative), session).ConfigureAwait(true);

        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("Invalid OAuth token in session.");
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenUserTokenReturns401_RefreshesTokenFiresEventAndRetries()
    {
        int attempt = 0;
        this._fixture.RegisterHandler("GET", "/helix/users", async context =>
        {
            attempt++;
            if (attempt == 1)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"status\":401,\"message\":\"Invalid OAuth token\"}").ConfigureAwait(true);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"data\":[{\"id\":\"141981764\",\"login\":\"twitchdev\"}]}").ConfigureAwait(true);
            }
        });

        string newBearer = TestTokenFactory.CreateToken("BEARER");
        string newRefresh = TestTokenFactory.CreateToken("REFRESH");
        this._fixture.RegisterHandler("POST", "/oauth2/token", async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"access_token\":\"{newBearer}\",\"expires_in\":14124,\"refresh_token\":\"{newRefresh}\",\"token_type\":\"bearer\"}}").ConfigureAwait(true);
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        string initialOAuth = session.Token!.OAuth!;
        string initialRefresh = session.Token.Refresh!;

        TokenRefreshedEventArgs? eventArgs = null;
        TwitchApi.OnTokenRefreshed += (sender, args) => eventArgs = args;

        try
        {
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri("users", UriKind.Relative), session).ConfigureAwait(true);

            _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
            _ = attempt.Should().Be(2);
            _ = session.Token.OAuth.Should().Be(newBearer);
            _ = session.Token.Refresh.Should().Be(newRefresh);
            _ = session.Token.OAuth.Should().NotBe(initialOAuth);
            _ = session.Token.Refresh.Should().NotBe(initialRefresh);
            _ = eventArgs.Should().NotBeNull();
            _ = eventArgs!.Session.Should().BeSameAs(session);
        }
        finally
        {
            TwitchApi.OnTokenRefreshed -= (sender, args) => eventArgs = args;
        }
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenAppTokenReturns401_RefreshesTokenFiresEventAndRetries()
    {
        int attempt = 0;
        this._fixture.RegisterHandler("GET", "/helix/app-resource", async context =>
        {
            attempt++;
            if (attempt == 1)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"status\":401,\"message\":\"Invalid OAuth token\"}").ConfigureAwait(true);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"data\":[{\"id\":\"app_resource\"}]}").ConfigureAwait(true);
            }
        });

        string newAppBearer = TestTokenFactory.CreateToken("APP");
        this._fixture.RegisterHandler("POST", "/oauth2/token", async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"access_token\":\"{newAppBearer}\",\"expires_in\":5011271,\"token_type\":\"bearer\"}}").ConfigureAwait(true);
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        string initialOAuth = session.Token!.OAuth!;

        TokenRefreshedEventArgs? eventArgs = null;
        TwitchApi.OnTokenRefreshed += (sender, args) => eventArgs = args;

        try
        {
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri("app-resource", UriKind.Relative), session).ConfigureAwait(true);

            _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
            _ = attempt.Should().Be(2);
            _ = session.Token.OAuth.Should().Be(newAppBearer);
            _ = session.Token.OAuth.Should().NotBe(initialOAuth);
            _ = eventArgs.Should().NotBeNull();
            _ = eventArgs!.Session.Should().BeSameAs(session);
        }
        finally
        {
            TwitchApi.OnTokenRefreshed -= (sender, args) => eventArgs = args;
        }
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenResponseIsNotJson_WrapsInJsonApiResponseFormat()
    {
        const string plainTextContent = "Plain text error message from upstream";
        this._fixture.RegisterHandler("GET", "/helix/plain-text-endpoint", async context =>
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(plainTextContent).ConfigureAwait(true);
        });

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri("plain-text-endpoint", UriKind.Relative), TwitchSession.Empty).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        JsonApiResponse? parsed = await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = parsed.Should().NotBeNull();
        _ = parsed.Message.Should().Be(plainTextContent);
        _ = parsed.Status.Should().Be(HttpStatusCode.BadGateway);
    }

    #endregion PerformHttpRequest
}

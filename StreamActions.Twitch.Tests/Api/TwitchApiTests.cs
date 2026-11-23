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

using StreamActions.Common.Extensions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    public async Task PerformHttpRequest_WhenUserTokenReturns401_RefreshesTokenAndRetries()
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

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri("users", UriKind.Relative), session).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = attempt.Should().Be(2);
        _ = session.Token.OAuth.Should().Be(newBearer);
        _ = session.Token.Refresh.Should().Be(newRefresh);
        _ = session.Token.OAuth.Should().NotBe(initialOAuth);
        _ = session.Token.Refresh.Should().NotBe(initialRefresh);
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenAppTokenReturns401_RefreshesTokenAndRetries()
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

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri("app-resource", UriKind.Relative), session).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = attempt.Should().Be(2);
        _ = session.Token.OAuth.Should().Be(newAppBearer);
        _ = session.Token.OAuth.Should().NotBe(initialOAuth);
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenRefreshTokenCallbackProvidedAndReturns401_InvokesCallbackAndRetries()
    {
        int attempt = 0;
        string? secondAuthHeader = null;
        int callbackCount = 0;

        using StubHttpMessageHandler handler = new(request =>
        {
            attempt++;
            if (attempt == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            secondAuthHeader = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json")
            };
        });
        using HttpMessageInvoker customInvoker = new(handler);
        using TwitchSession session = new()
        {
            CustomHttpClient = customInvoker,
            CustomBaseUri = new("https://custom.twitch.test/helix/"),
            Token = new TwitchToken
            {
                OAuth = "expired-token",
                Refresh = "refresh-token",
                Type = TwitchToken.TokenType.User
            },
            RefreshTokenCallback = (s, ct) =>
            {
                callbackCount++;
                return ValueTask.FromResult<TwitchToken?>(s.Token! with { OAuth = "callback-refreshed-token" });
            }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new("users", UriKind.Relative), session).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = attempt.Should().Be(2);
        _ = callbackCount.Should().Be(1);
        _ = secondAuthHeader.Should().Be("Bearer callback-refreshed-token");
        _ = session.Token.OAuth.Should().Be("callback-refreshed-token");
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenRefreshTokenCallbackReturnsNull_DoesNotRetryAndReturnsUnauthorized()
    {
        int attempt = 0;
        using StubHttpMessageHandler handler = new(_ =>
        {
            attempt++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"status":401,"message":"Invalid token"}""", Encoding.UTF8, "application/json")
            };
        });
        using HttpMessageInvoker customInvoker = new(handler);
        using TwitchSession session = new()
        {
            CustomHttpClient = customInvoker,
            CustomBaseUri = new("https://custom.twitch.test/helix/"),
            Token = new TwitchToken
            {
                OAuth = "expired-token",
                Type = TwitchToken.TokenType.User
            },
            RefreshTokenCallback = (_, _) => ValueTask.FromResult<TwitchToken?>(null)
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new("users", UriKind.Relative), session).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _ = attempt.Should().Be(1);
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenPostWithJsonContentReturns401_RetriesWithoutObjectDisposedException()
    {
        int attempt = 0;
        List<string> capturedBodies = [];
        List<string?> capturedContentTypes = [];

        using StubHttpMessageHandler handler = new(async request =>
        {
            attempt++;
            string body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            capturedBodies.Add(body);
            capturedContentTypes.Add(request.Content.Headers.ContentType?.MediaType);

            return attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json")
                };
        });
        using HttpMessageInvoker customInvoker = new(handler);
        using TwitchSession session = new()
        {
            CustomHttpClient = customInvoker,
            CustomBaseUri = new("https://custom.twitch.test/helix/"),
            Token = new TwitchToken
            {
                OAuth = "expired-token",
                Type = TwitchToken.TokenType.User
            },
            RefreshTokenCallback = (s, _) => ValueTask.FromResult<TwitchToken?>(s.Token! with { OAuth = "fresh-token" })
        };

        using JsonContent jsonContent = JsonContent.Create(new { broadcaster_id = "1234", message = "hello" });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("chat/messages", UriKind.Relative), session, jsonContent).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = attempt.Should().Be(2);
        _ = capturedBodies.Should().HaveCount(2);
        _ = capturedBodies[1].Should().Be(capturedBodies[0]);
        _ = capturedBodies[1].Should().Contain("\"broadcaster_id\":\"1234\"");
        _ = capturedContentTypes.Should().AllBe("application/json");
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WhenSessionCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync().ConfigureAwait(true);

        using StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using HttpMessageInvoker customInvoker = new(handler);
        using TwitchSession session = new()
        {
            CancellationToken = cts.Token,
            CustomHttpClient = customInvoker,
            CustomBaseUri = new("https://custom.twitch.test/helix/"),
            Token = TwitchToken.Empty
        };

        Func<Task> action = async () => await TwitchApi.PerformHttpRequest(HttpMethod.Get, new("users", UriKind.Relative), session).ConfigureAwait(true);

        _ = await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
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

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WithCustomClientIdAndCustomHttpClient_UsesSessionOverrides()
    {
        string? capturedClientId = null;
        Uri? capturedUri = null;
        using StubHttpMessageHandler handler = new(request =>
        {
            capturedClientId = request.Headers.GetValues("Client-Id").Single();
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });
        using HttpMessageInvoker customInvoker = new(handler);
        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "custom-session-client-id",
            CustomHttpClient = customInvoker,
            CustomBaseUri = new("https://custom.twitch.test/helix/")
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new("users?login=test", UriKind.Relative), session).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = capturedClientId.Should().Be("custom-session-client-id");
        _ = capturedUri.Should().Be(new Uri("https://custom.twitch.test/helix/users?login=test"));
    }

    [Fact]
    [Trait("Member", "PerformHttpRequest")]
    public async Task PerformHttpRequest_WithCustomClientSecretOn401Refresh_SendsCustomClientIdAndSecret()
    {
        string? refreshBody = null;
        string? refreshClientIdHeader = null;
        int helixCalls = 0;

        using StubHttpMessageHandler handler = new(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal))
            {
                refreshClientIdHeader = request.Headers.GetValues("Client-Id").Single();
                refreshBody = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"token_type":"bearer"}""", System.Text.Encoding.UTF8, "application/json")
                };
            }

            helixCalls++;
            return helixCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[]}""", System.Text.Encoding.UTF8, "application/json")
                };
        });
        using HttpMessageInvoker customInvoker = new(handler);
        using TwitchSession session = new()
        {
            Token = new()
            {
                OAuth = "expired-access",
                Refresh = "old-refresh",
                Type = TwitchToken.TokenType.User
            },
            CustomClientId = "override-client-id",
            CustomClientSecret = "override-client-secret",
            CustomHttpClient = customInvoker,
            CustomBaseUri = new("https://custom.twitch.test/helix/")
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new("users", UriKind.Relative), session).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = helixCalls.Should().Be(2);
        _ = refreshClientIdHeader.Should().Be("override-client-id");
        _ = refreshBody.Should().Contain("client_id=override-client-id");
        _ = refreshBody.Should().Contain("client_secret=override-client-secret");
        _ = session.Token.OAuth.Should().Be("new-access");
    }

    #endregion PerformHttpRequest

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> syncHandler)
            : this(request => Task.FromResult(syncHandler(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}

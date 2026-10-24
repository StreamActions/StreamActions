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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.EventSub;
using StreamActions.Twitch.EventSub.Common;
using StreamActions.Twitch.OAuth;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EventSubService = StreamActions.Twitch.EventSub.EventSub;

namespace StreamActions.Twitch.Tests.Infrastructure;

/// <summary>
/// Represents a recorded HTTP request received by the test server fixture.
/// </summary>
public sealed record RecordedRequest
{
    /// <summary>
    /// The HTTP method of the request.
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// The path component of the request URI.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The query string component of the request URI.
    /// </summary>
    public string QueryString { get; init; } = string.Empty;

    /// <summary>
    /// The parsed query collection of the request.
    /// </summary>
    public IQueryCollection Query { get; init; } = default!;

    /// <summary>
    /// The headers collection of the request.
    /// </summary>
    public IHeaderDictionary Headers { get; init; } = default!;

    /// <summary>
    /// The body content of the request.
    /// </summary>
    public string Body { get; init; } = string.Empty;
}

/// <summary>
/// Test fixture hosting an in-memory ASP.NET Core Kestrel server for mocking Twitch API and OAuth endpoints.
/// </summary>
public sealed class TwitchKestrelServerFixture : IAsyncLifetime, IDisposable, IAsyncDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel web application instance.
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// The RSA keypair used for signing test OIDC JWTs.
    /// </summary>
    private RSA _rsa = RSA.Create(2048);

    /// <summary>
    /// The key ID of the RSA security key.
    /// </summary>
    private string _keyId = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The RSA security key for signing tokens.
    /// </summary>
    private RsaSecurityKey _rsaSecurityKey = default!;

    /// <summary>
    /// Map of registered route handlers keyed by method and normalized path.
    /// </summary>
    private readonly ConcurrentDictionary<(string Method, string Path), Func<HttpContext, Task>> _handlers = new();

    /// <summary>
    /// Thread-safe queue of scripted WebSocket server actions.
    /// </summary>
    private readonly ConcurrentQueue<Func<WebSocket, HttpContext, Task>> _webSocketHandlers = new();

    /// <summary>
    /// Thread-safe list of recorded requests.
    /// </summary>
    private readonly ConcurrentQueue<RecordedRequest> _recordedRequests = new();

    #endregion Private Fields

    #region Public Properties

    /// <summary>
    /// The base URL of the running Kestrel test server without trailing slash.
    /// </summary>
    [SuppressMessage("Design", "CA1056:Uri properties should not be strings", Justification = "BaseUrl string format required by API initialization.")]
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// The test Client ID generated for the server fixture.
    /// </summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>
    /// The test Client Secret generated for the server fixture.
    /// </summary>
    public string ClientSecret { get; private set; } = string.Empty;

    /// <summary>
    /// The secret string used for verifying EventSub webhook messages.
    /// </summary>
    public string WebhookSecret { get; set; } = "test_webhook_secret";

    /// <summary>
    /// The most recently received request, or <see langword="null"/> if none has been received.
    /// </summary>
    public RecordedRequest? LastRequest => this._recordedRequests.LastOrDefault();

    /// <summary>
    /// All recorded requests received since the last clear.
    /// </summary>
    public IReadOnlyList<RecordedRequest> RecordedRequests => [.. this._recordedRequests];

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Initializes and starts the Kestrel server, generating RSA keys and registering default routes.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask InitializeAsync()
    {
        this._rsa = RSA.Create(2048);
        this._keyId = Guid.NewGuid().ToString("N");
        this._rsaSecurityKey = new(this._rsa) { KeyId = this._keyId };

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls("http://127.0.0.1:0");

        this._app = builder.Build();
        _ = this._app.UseWebSockets();
        this._app.Run(this.ProcessRequestAsync);

        await this._app.StartAsync().ConfigureAwait(false);

        IServerAddressesFeature? addressesFeature = this._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        string address = addressesFeature?.Addresses.FirstOrDefault() ?? this._app.Urls.FirstOrDefault() ?? "http://127.0.0.1:5000";
        this.BaseUrl = address.TrimEnd('/');

        this.ClientId = TestTokenFactory.CreateToken("CLIENT");
        this.ClientSecret = TestTokenFactory.CreateToken("SECRET");

        TwitchApi.Init(this.ClientId, this.ClientSecret, $"{this.BaseUrl}/helix/");
        Token.SetOpenIdConnectConfigurationUrl($"{this.BaseUrl}/oauth2/.well-known/openid-configuration");
    }

    /// <summary>
    /// Stops and disposes the Kestrel server and cryptographic resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this._app is not null)
        {
            await this._app.StopAsync().ConfigureAwait(false);
            await this._app.DisposeAsync().ConfigureAwait(false);
            this._app = null;
        }

        this._rsa.Dispose();
    }

    /// <summary>
    /// Synchronously disposes the test server fixture.
    /// </summary>
    public void Dispose() => this.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Resets all registered handlers, queued WebSocket actions, and recorded requests to the default initial state.
    /// </summary>
    public void Reset()
    {
        this._handlers.Clear();
        this._webSocketHandlers.Clear();
        this._recordedRequests.Clear();

        TwitchApi.Init(this.ClientId, this.ClientSecret, $"{this.BaseUrl}/helix/");
        Token.SetOpenIdConnectConfigurationUrl($"{this.BaseUrl}/oauth2/.well-known/openid-configuration");
    }

    /// <summary>
    /// Clears all recorded HTTP requests.
    /// </summary>
    public void ClearRequests() => this._recordedRequests.Clear();

    /// <summary>
    /// Clears all registered custom route handlers.
    /// </summary>
    public void ClearHandlers() => this._handlers.Clear();

    /// <summary>
    /// Registers a custom asynchronous request handler for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method (e.g. GET, POST).</param>
    /// <param name="path">The request path (with or without leading slash).</param>
    /// <param name="handler">The delegate to execute when handling the request.</param>
    public void RegisterHandler(string method, string path, Func<HttpContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(handler);

        (string Method, string Path) key = (method.ToUpperInvariant(), NormalizePath(path));
        this._handlers[key] = handler;
    }

    /// <summary>
    /// Registers a JSON response for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="responseBody">The object to serialize as JSON.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to 200.</param>
    public void RegisterJsonResponse(string method, string path, object responseBody, int statusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        string json = JsonSerializer.Serialize(responseBody, TwitchApi.SerializerOptions);
        this.RegisterResponse(method, path, json, "application/json", statusCode);
    }

    /// <summary>
    /// Registers a JSON response for a specific HTTP method and path with a <see cref="HttpStatusCode"/>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="responseBody">The object to serialize as JSON.</param>
    /// <param name="statusCode">The HTTP status code as a <see cref="HttpStatusCode"/>.</param>
    public void RegisterJsonResponse(string method, string path, object responseBody, HttpStatusCode statusCode) => this.RegisterJsonResponse(method, path, responseBody, (int)statusCode);

    /// <summary>
    /// Registers a string response with a custom content type for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="content">The response body content string.</param>
    /// <param name="contentType">The Content-Type header value. Defaults to <c>application/json</c>.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to 200.</param>
    public void RegisterResponse(string method, string path, string content, string contentType = "application/json", int statusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentType);
        this.RegisterHandler(method, path, async context =>
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            await context.Response.WriteAsync(content, Encoding.UTF8).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Registers an empty response returning only a specific HTTP status code for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    public void RegisterStatusCode(string method, string path, int statusCode)
    {
        this.RegisterHandler(method, path, context =>
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Registers a string response with an <see cref="HttpStatusCode"/> for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="content">The response body content string.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="contentType">The Content-Type header value. Defaults to <c>application/json</c>.</param>
    public void RegisterResponse(string method, string path, string content, HttpStatusCode statusCode, string contentType = "application/json") =>
        this.RegisterResponse(method, path, content, contentType, (int)statusCode);

    /// <summary>
    /// Registers an empty response returning only a specific <see cref="HttpStatusCode"/> for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    public void RegisterStatusCode(string method, string path, HttpStatusCode statusCode) =>
        this.RegisterStatusCode(method, path, (int)statusCode);

    /// <summary>
    /// Registers an empty response returning only a specific <see cref="HttpStatusCode"/> for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The HTTP status code to return as a <see cref="HttpStatusCode"/>.</param>
    public void RegisterResponse(string method, string path, HttpStatusCode statusCode) => this.RegisterStatusCode(method, path, (int)statusCode);

    /// <summary>
    /// Registers an endpoint response for any HTTP method at the specified path.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="content">The response body content string.</param>
    /// <param name="contentType">The Content-Type header value. Defaults to <c>application/json</c>.</param>
    public void RegisterEndpoint(string path, HttpStatusCode statusCode, string content = "", string contentType = "application/json") =>
        this.RegisterResponse("*", path, content, contentType, (int)statusCode);

    /// <summary>
    /// Registers an endpoint response for a specific HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="content">The response body content string.</param>
    /// <param name="contentType">The Content-Type header value. Defaults to <c>application/json</c>.</param>
    public void RegisterEndpoint(string method, string path, HttpStatusCode statusCode, string content = "", string contentType = "application/json") =>
        this.RegisterResponse(method, path, content, contentType, (int)statusCode);

    /// <summary>
    /// Queues a scripted WebSocket server action to execute when a client connects to <c>/ws</c>.
    /// </summary>
    /// <param name="action">The delegate executed with the accepted <see cref="WebSocket"/> and <see cref="HttpContext"/>.</param>
    public void QueueWebSocketAction(Func<WebSocket, HttpContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        this._webSocketHandlers.Enqueue(action);
    }

    /// <summary>
    /// Creates a signed OIDC JWT <c>id_token</c> signed with the server fixture's RSA key.
    /// </summary>
    /// <param name="clientId">The client ID audience.</param>
    /// <param name="subject">The user ID subject claim.</param>
    /// <param name="preferredUsername">The preferred username claim.</param>
    /// <returns>A serialized, signed JWT string.</returns>
    public string CreateSignedJwtIdToken(string clientId, string subject = "141981764", string preferredUsername = "twitchdev")
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(preferredUsername);

        JsonWebTokenHandler handler = new();
        SigningCredentials credentials = new(this._rsaSecurityKey, SecurityAlgorithms.RsaSha256);
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = this.BaseUrl,
            Audience = clientId,
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                { "sub", subject },
                { "preferred_username", preferredUsername }
            }
        };

        return handler.CreateToken(descriptor);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Normalizes a URL path to ensure a leading slash and consistent casing.
    /// </summary>
    /// <param name="path">The input path.</param>
    /// <returns>The normalized path starting with a slash.</returns>
    private static string NormalizePath(string path)
    {
        string trimmed = path.Trim();
        string withLeadingSlash = trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
        if (withLeadingSlash.StartsWith("/helix/", StringComparison.OrdinalIgnoreCase))
        {
            return withLeadingSlash["/helix".Length..];
        }
        return withLeadingSlash;
    }

    /// <summary>
    /// Handles an incoming HTTP or WebSocket request to the Kestrel test server.
    /// </summary>
    /// <param name="context">The incoming <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task ProcessRequestAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        string body = string.Empty;
        using (StreamReader reader = new(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync().ConfigureAwait(false);
            context.Request.Body.Position = 0;
        }

        HeaderDictionary headersCopy = new(context.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
        QueryCollection queryCopy = new(context.Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
        RecordedRequest recorded = new()
        {
            Method = context.Request.Method,
            Path = context.Request.Path.Value ?? string.Empty,
            QueryString = context.Request.QueryString.Value ?? string.Empty,
            Query = queryCopy,
            Headers = headersCopy,
            Body = body
        };
        this._recordedRequests.Enqueue(recorded);

        if (context.Request.Path.StartsWithSegments("/ws", StringComparison.OrdinalIgnoreCase) && context.WebSockets.IsWebSocketRequest)
        {
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            if (this._webSocketHandlers.TryDequeue(out Func<WebSocket, HttpContext, Task>? wsHandler))
            {
                await wsHandler(webSocket, context).ConfigureAwait(false);
            }

            try
            {
                byte[] buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted).ConfigureAwait(false);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        {
                            await webSocket.CloseOutputAsync(receiveResult.CloseStatus ?? WebSocketCloseStatus.NormalClosure, receiveResult.CloseStatusDescription, CancellationToken.None).ConfigureAwait(false);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
            {
            }

            return;
        }

        string normalizedPath = NormalizePath(context.Request.Path.Value ?? string.Empty);
        (string Method, string Path) key = (context.Request.Method.ToUpperInvariant(), normalizedPath);

        if (this._handlers.TryGetValue(key, out Func<HttpContext, Task>? customHandler) ||
            this._handlers.TryGetValue(("*", normalizedPath), out customHandler))
        {
            await customHandler(context).ConfigureAwait(false);
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method) && string.Equals(normalizedPath, "/oauth2/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase))
        {
            await this.HandleOpenIdConfigurationAsync(context).ConfigureAwait(false);
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method) && string.Equals(normalizedPath, "/oauth2/keys", StringComparison.OrdinalIgnoreCase))
        {
            await this.HandleJwksKeysAsync(context).ConfigureAwait(false);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method) && string.Equals(normalizedPath, "/webhook", StringComparison.OrdinalIgnoreCase))
        {
            EventSubWebhookResult webhookResult = await EventSubService.ProcessWebhook(context.Request, this.WebhookSecret).ConfigureAwait(false);
            context.Response.StatusCode = webhookResult.StatusCode ?? StatusCodes.Status200OK;
            if (!string.IsNullOrWhiteSpace(webhookResult.ContentType))
            {
                context.Response.ContentType = webhookResult.ContentType;
            }
            if (!string.IsNullOrWhiteSpace(webhookResult.Body))
            {
                await context.Response.WriteAsync(webhookResult.Body).ConfigureAwait(false);
            }
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"Not Found\"}").ConfigureAwait(false);
    }

    /// <summary>
    /// Serves the OpenID Connect discovery document.
    /// </summary>
    /// <param name="context">The incoming <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task HandleOpenIdConfigurationAsync(HttpContext context)
    {
        object config = new
        {
            issuer = this.BaseUrl,
            authorization_endpoint = $"{this.BaseUrl}/oauth2/authorize",
            token_endpoint = $"{this.BaseUrl}/oauth2/token",
            userinfo_endpoint = $"{this.BaseUrl}/oauth2/userinfo",
            jwks_uri = $"{this.BaseUrl}/oauth2/keys",
            response_types_supported = new[] { "code", "token", "id_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            scopes_supported = new[] { "openid", "user:read:email" }
        };

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(config, TwitchApi.SerializerOptions)).ConfigureAwait(false);
    }

    /// <summary>
    /// Serves the JSON Web Key Set containing the public RSA key.
    /// </summary>
    /// <param name="context">The incoming <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task HandleJwksKeysAsync(HttpContext context)
    {
        RSAParameters parameters = this._rsa.ExportParameters(false);
        object jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = this._keyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent)
                }
            }
        };

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(jwks, TwitchApi.SerializerOptions)).ConfigureAwait(false);
    }

    #endregion Private Methods
}

/// <summary>
/// xUnit collection definition for tests requiring the <see cref="TwitchKestrelServerFixture"/> without parallelization.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "xUnit CollectionDefinition convention specified by architecture.")]
[CollectionDefinition("TwitchKestrelCollection", DisableParallelization = true)]
public class TwitchKestrelCollection : ICollectionFixture<TwitchKestrelServerFixture>
{
}

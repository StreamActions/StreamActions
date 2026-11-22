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
using Microsoft.Extensions.Logging;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.Common;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace StreamActions.Twitch.UI.Tests.Infrastructure;

/// <summary>
/// Represents an HTTP request captured by <see cref="TwitchHelixMockServer"/>.
/// </summary>
public sealed record RecordedHelixRequest(
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string[]> Query,
    IReadOnlyDictionary<string, string[]> Headers,
    string Body);

/// <summary>
/// Ephemeral loopback Kestrel minimal HTTP server mocking Twitch Helix endpoints without touching static <see cref="TwitchApi"/> state.
/// </summary>
public sealed class TwitchHelixMockServer : IAsyncDisposable
{
    #region Private Fields

    /// <summary>
    /// Running Kestrel web application instance.
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// Registered route handlers keyed by normalized HTTP method and Helix path.
    /// </summary>
    private readonly ConcurrentDictionary<(string Method, string Path), Func<HttpContext, Task>> _handlers = new();

    /// <summary>
    /// Thread-safe queue of captured inbound HTTP requests.
    /// </summary>
    private readonly ConcurrentQueue<RecordedHelixRequest> _requests = new();

    #endregion Private Fields

    #region Private Constructors

    /// <summary>
    /// Prevents external instantiation outside <see cref="StartAsync"/>.
    /// </summary>
    private TwitchHelixMockServer()
    {
    }

    #endregion Private Constructors

    #region Public Properties

    /// <summary>
    /// Gets the base server URL without a trailing slash (for example, <c>http://127.0.0.1:54321</c>).
    /// </summary>
    public Uri BaseUri { get; private set; } = new("http://127.0.0.1");

    /// <summary>
    /// Gets the Helix base URI ending with <c>/helix/</c> suitable for <see cref="TwitchSession.CustomBaseUri"/>.
    /// </summary>
    public Uri HelixBaseUri { get; private set; } = new("http://127.0.0.1/helix/");

    /// <summary>
    /// Gets the snapshot of all HTTP requests received since startup or the last <see cref="ClearRequests"/> call.
    /// </summary>
    public IReadOnlyList<RecordedHelixRequest> RecordedRequests => [.. this._requests];

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Starts a new isolated <see cref="TwitchHelixMockServer"/> bound to an ephemeral loopback port (<c>127.0.0.1:0</c>).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The started <see cref="TwitchHelixMockServer"/> instance.</returns>
    public static async Task<TwitchHelixMockServer> StartAsync(CancellationToken cancellationToken = default)
    {
        TwitchHelixMockServer server = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.Logging.ClearProviders();
        _ = builder.WebHost.UseUrls("http://127.0.0.1:0");

        WebApplication app = builder.Build();
        app.Run(server.HandleRequestAsync);

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        IServerAddressesFeature? addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        string boundAddress = addresses?.Addresses.FirstOrDefault() ?? app.Urls.First();
        string trimmedBase = boundAddress.TrimEnd('/');

        server._app = app;
        server.BaseUri = new Uri(trimmedBase, UriKind.Absolute);
        server.HelixBaseUri = new Uri($"{trimmedBase}/helix/", UriKind.Absolute);
        return server;
    }

    /// <summary>
    /// Registers a custom asynchronous route handler for <paramref name="method"/> and <paramref name="path"/>.
    /// </summary>
    /// <param name="method">The HTTP method (for example, <c>GET</c> or <c>PATCH</c>).</param>
    /// <param name="path">The endpoint path (with or without <c>/helix</c> prefix).</param>
    /// <param name="handler">The delegate handling matching requests.</param>
    public void RegisterHandler(string method, string path, Func<HttpContext, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(handler);

        this._handlers[(method.Trim().ToUpperInvariant(), NormalizePath(path))] = handler;
    }

    /// <summary>
    /// Registers a static JSON response serialized with <see cref="TwitchApi.SerializerOptions"/>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The endpoint path (with or without <c>/helix</c> prefix).</param>
    /// <param name="payload">The object to serialize as JSON.</param>
    /// <param name="statusCode">The HTTP status code (default <c>200</c>).</param>
    public void RegisterJsonResponse(string method, string path, object payload, int statusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(payload);
        string json = JsonSerializer.Serialize(payload, TwitchApi.SerializerOptions);
        this.RegisterHandler(method, path, async context =>
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, Encoding.UTF8).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Registers an HTTP status-only response for <paramref name="method"/> and <paramref name="path"/>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The endpoint path.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    public void RegisterStatusCode(string method, string path, int statusCode) =>
        this.RegisterHandler(method, path, context =>
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });

    /// <summary>
    /// Counts recorded requests matching the specified Helix path (ignoring any <c>/helix</c> prefix).
    /// </summary>
    /// <param name="path">The endpoint path to count.</param>
    /// <returns>The number of matching recorded requests.</returns>
    public int CountRequests(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalized = NormalizePath(path);
        return this._requests.Count(r => string.Equals(NormalizePath(r.Path), normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears all recorded requests.
    /// </summary>
    public void ClearRequests() => this._requests.Clear();

    /// <summary>
    /// Clears all registered handlers and recorded requests.
    /// </summary>
    public void Reset()
    {
        this._handlers.Clear();
        this._requests.Clear();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._app is not null)
        {
            await this._app.StopAsync().ConfigureAwait(false);
            await this._app.DisposeAsync().ConfigureAwait(false);
            this._app = null;
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Normalizes a path by ensuring a leading slash and stripping any leading <c>/helix</c> prefix.
    /// </summary>
    /// <param name="path">The raw path string.</param>
    /// <returns>The normalized Helix-relative path.</returns>
    private static string NormalizePath(string path)
    {
        string trimmed = path.Trim();
        string withSlash = trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
        return withSlash.StartsWith("/helix/", StringComparison.OrdinalIgnoreCase)
            ? withSlash["/helix".Length..]
            : withSlash;
    }

    /// <summary>
    /// Records an inbound HTTP request and dispatches to the matching registered route handler.
    /// </summary>
    /// <param name="context">The active <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous request handling.</returns>
    private async Task HandleRequestAsync(HttpContext context)
    {
        string body;
        using (StreamReader reader = new(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        Dictionary<string, string[]> queryCopy = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in context.Request.Query)
        {
            queryCopy[kv.Key] = [.. kv.Value.Where(static value => value is not null).Select(static value => value!)];
        }

        Dictionary<string, string[]> headersCopy = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in context.Request.Headers)
        {
            headersCopy[kv.Key] = [.. kv.Value.Where(static value => value is not null).Select(static value => value!)];
        }

        this._requests.Enqueue(new RecordedHelixRequest(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            context.Request.QueryString.Value ?? string.Empty,
            queryCopy,
            headersCopy,
            body));

        string normalizedPath = NormalizePath(context.Request.Path.Value ?? string.Empty);
        (string Method, string Path) key = (context.Request.Method.ToUpperInvariant(), normalizedPath);

        if (this._handlers.TryGetValue(key, out Func<HttpContext, Task>? handler))
        {
            await handler(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"Not Found\"}", Encoding.UTF8).ConfigureAwait(false);
    }

    #endregion Private Methods
}

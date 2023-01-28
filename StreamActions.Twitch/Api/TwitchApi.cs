/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Attributes;
using StreamActions.Common.Extensions;
using StreamActions.Common.Interfaces;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StreamActions.Twitch.Api;

/// <summary>
/// Handles validation of OAuth tokens and performs HTTP calls for the API.
/// </summary>
[Guid("996E35EC-638A-4E5B-AEFC-84C800E16520")]
[ETag("[Twitch] API", new string[] { "twitch", "api" },
    "https://dev.twitch.tv/docs/api/reference", "617ae798756077c5bdd9a4168c23b045422700b804a91156bb47018b4688aae8", "2023-01-21T06:21Z",
    new string[] { "-stripblank", "-strip", "-findfirst", "'<div class=\"main\">'", "-findlast", "'<div class=\"subscribe-footer\">'",
        "-remre", "'cloudcannon[^\"]*'", "-rem",
        "'<a href=\"/docs/product-lifecycle\"><span class=\"pill pill-new\">NEW</span></a>'",
        "'<a href=\"/docs/product-lifecycle\"><span class=\"pill pill-beta\">BETA</span></a>'" })]
public sealed partial class TwitchApi : IApi
{

    #region Public Events

    /// <summary>
    /// Fires when a <see cref="TwitchSession"/> has its OAuth token automatically refreshed as a result of a 401 response.
    /// </summary>
    public static event EventHandler<TokenRefreshedEventArgs>? OnTokenRefreshed;

    #endregion Public Events

    #region Public Properties

    /// <summary>
    /// <inheritdoc cref="IApi.ApiDate"/>
    /// </summary>
    public static DateTime? ApiDate => new(2022, 05, 09);

    /// <summary>
    /// <inheritdoc cref="IComponent.Author"/>
    /// </summary>
    public static string? Author => "StreamActions Team";

    /// <summary>
    /// The currently initialized Client Id.
    /// </summary>
    public static string? ClientId { get; private set; }

    /// <summary>
    /// <inheritdoc cref="IComponent.Description"/>
    /// </summary>
    public static string? Description => "Interacts with the Twitch Helix API";

    /// <summary>
    /// <inheritdoc cref="IComponent.Id"/>
    /// </summary>
    public static Guid? Id => typeof(TwitchApi).GUID;

    /// <summary>
    /// <inheritdoc cref="IComponent.Name"/>
    /// </summary>
    public static string? Name => nameof(TwitchApi);

    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> that should be used for all serialization and deserialization.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions => new()
    {
        Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// <inheritdoc cref="IComponent.Uri"/>
    /// </summary>
    public static Uri? Uri => new("https://github.com/StreamActions/StreamActions");

    /// <summary>
    /// <inheritdoc cref="IComponent.Version"/>
    /// </summary>
    public static Version? Version => new(1, 0, 0, 0);

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Initializes the HTTP client.
    /// </summary>
    /// <param name="clientId">A valid Twitch App Client Id.</param>
    /// <param name="clientSecret">A valid Twitch App Client Secret.</param>
    /// <param name="baseAddress">The base uri to Helix.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clientId"/> is <see langword="null"/> or whitespace.</exception>
    public static void Init(string clientId, string? clientSecret = null, string baseAddress = "https://api.twitch.tv/helix/")
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentNullException(nameof(clientId)).Log(_logger, atLocation: 3);
        }

        ClientId = clientId;
        ClientSecret = clientSecret;

        _ = _httpClient.DefaultRequestHeaders.Remove("Client-Id");
        _ = _httpClient.DefaultRequestHeaders.Remove("User-Agent");

        _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "StreamActions/TwitchAPI/" + typeof(TwitchApi).Assembly.GetName()?.Version?.ToString() ?? "0.0.0.0");
        _httpClient.BaseAddress = new Uri(baseAddress);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    #endregion Public Methods

    #region Internal Properties

    /// <summary>
    /// The currently initialized Client Secret.
    /// </summary>
    internal static string? ClientSecret { get; private set; }

    #endregion Internal Properties

    #region Internal Methods

    /// <summary>
    /// Gets the logger for TwitchApi.
    /// </summary>
    /// <returns>An <see cref="ILogger"/>.</returns>
    internal static ILogger GetLogger() => _logger;

    /// <summary>
    /// Submits a HTTP request to the specified Uri and returns the response.
    /// </summary>
    /// <param name="method">The <see cref="HttpMethod"/> of the request.</param>
    /// <param name="uri">The uri to request. Relative uris resolve against the Helix base.</param>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="content">The body of the request, for methods that require it.</param>
    /// <param name="dontRefresh">Used by <see cref="Token.RefreshOAuth(TwitchSession, string?)"/> to prevent cyclic refresh loops.</param>
    /// <returns>A <see cref="HttpResponseMessage"/> containing the response data.</returns>
    /// <exception cref="InvalidOperationException">Did not call <see cref="Init(string)"/> with a valid Client Id or <paramref name="session"/> does not have an OAuth token.</exception>
    /// <remarks>
    /// <para>
    /// Non-JSON responses are converted to the standard <c>{status,error,message}</c> format.
    /// </para>
    /// <para>
    /// If a 401 is returned, one attempt will be made to refresh the OAuth token and try again.
    /// The <paramref name="session"/> object is updated and <see cref="OnTokenRefreshed"/> is called on refresh success.
    /// </para>
    /// </remarks>
    internal static async Task<HttpResponseMessage> PerformHttpRequest(HttpMethod method, Uri uri, TwitchSession session, HttpContent? content = null, bool dontRefresh = false)
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("Client-Id"))
        {
            throw new InvalidOperationException("Must call TwitchAPI.Init.").Log(_logger, atLocation: 3);
        }

        if (session.Token != TwitchToken.Empty && string.IsNullOrWhiteSpace(session.Token?.OAuth))
        {
            throw new InvalidOperationException("Invalid OAuth token in session.").Log(_logger, atLocation: 3);
        }

        bool retry = false;
    performhttprequest_start:
        try
        {
            await session.RateLimiter.WaitForRateLimit(TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            using HttpRequestMessage request = new(method, uri) { Version = _httpClient.DefaultRequestVersion, VersionPolicy = _httpClient.DefaultVersionPolicy };
            request.Content = content;

            if (session.Token != TwitchToken.Empty)
            {
                request.Headers.Add("Authorization", "Bearer " + session.Token.OAuth);
            }

            HttpResponseMessage response = await _httpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

            if (!retry && !dontRefresh && response.StatusCode == System.Net.HttpStatusCode.Unauthorized && session.Token != TwitchToken.Empty && !string.IsNullOrWhiteSpace(session.Token.Refresh))
            {
                string oldRefresh = session.Token.Refresh;
                if (session._refreshLock.WaitOne(TimeSpan.FromSeconds(30)))
                {
                    try
                    {
                        if (session.Token.Refresh == oldRefresh)
                        {
                            Token? refresh = await Token.RefreshOAuth(session).ConfigureAwait(false);

                            if (refresh is not null && refresh.IsSuccessStatusCode)
                            {
                                TwitchToken oldToken = session.Token;
                                session.Token = new() { OAuth = refresh.AccessToken, Refresh = refresh.RefreshToken, Expires = refresh.Expires, Scopes = refresh.Scopes, Login = oldToken.Login, UserId = oldToken.UserId };
                                _ = OnTokenRefreshed?.InvokeAsync(null, new(session));
                                retry = true;
                            }
                        }
                        else
                        {
                            retry = true;
                        }
                    }
                    finally
                    {
                        session._refreshLock.ReleaseMutex();
                    }
                }

                if (retry)
                {

                    goto performhttprequest_start;
                }
            }

            if (response.Content.Headers?.ContentType?.MediaType != "application/json")
            {
                string rcontent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!JSONRegex().IsMatch(rcontent))
                {
                    response.Content = new StringContent(JsonSerializer.Serialize(new JsonApiResponse { Status = response.StatusCode, Message = rcontent }, SerializerOptions));
                }
            }

            session.RateLimiter.ParseHeaders(response.Headers);

            return response;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            return new(0) { ReasonPhrase = ex.GetType().Name, Content = new StringContent(JsonSerializer.Serialize(new JsonApiResponse { Message = ex.Message }, SerializerOptions)) };
        }
    }

    #endregion Internal Methods

    #region Private Fields

    /// <summary>
    /// The <see cref="HttpClient"/> that is used for all requests.
    /// </summary>
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// The <see cref="ILogger"/> for logging.
    /// </summary>
    private static readonly ILogger _logger = Logger.GetLogger(typeof(TwitchApi));

    #endregion Private Fields

    #region Private Constructors

    /// <summary>
    /// Disabled constructor.
    /// </summary>
    private TwitchApi()
    { }
    #endregion Private Constructors

    #region Private Methods

    [GeneratedRegex("\\s*{.*}\\s*")]
    private static partial Regex JSONRegex();

    #endregion Private Methods

}

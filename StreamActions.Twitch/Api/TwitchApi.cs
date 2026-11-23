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

using StreamActions.Common.Attributes;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.EventSub.Conditions;
using StreamActions.Twitch.OAuth;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StreamActions.Twitch.Api;

/// <summary>
/// Handles validation of OAuth tokens and performs HTTP calls for the API.
/// </summary>
[DocParser("Twitch API", 72, "https://dev.twitch.tv/docs/api/reference/", "twitch/TwitchReferenceParser")]
public static partial class TwitchApi
{
    #region Public Properties

    /// <summary>
    /// The currently initialized Client Id.
    /// </summary>
    public static string? ClientId { get; private set; }

    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> that should be used for all serialization and deserialization.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        Converters = {
            new CustomEnumConverterFactory(),
            new EventSubConditionConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

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
            throw new ArgumentNullException(nameof(clientId));
        }

        ClientId = clientId;
        ClientSecret = clientSecret;

        HttpClient newClient = CreateHttpClient();
        _ = newClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "StreamActions/TwitchAPI/" + (typeof(TwitchApi).Assembly.GetName()?.Version?.ToString() ?? "0.0.0.0"));
        // Intentionally not validating the baseAddress here, as it is possible to use a custom Helix endpoint for testing.
        newClient.BaseAddress = new Uri(baseAddress);
        newClient.Timeout = TimeSpan.FromSeconds(30);

        HttpClient oldClient = Interlocked.Exchange(ref _httpClient, newClient);
        oldClient.Dispose();
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
    /// Submits a HTTP request to the specified Uri and returns the response.
    /// </summary>
    /// <param name="method">The <see cref="HttpMethod"/> of the request.</param>
    /// <param name="uri">The uri to request. Relative uris resolve against the Helix base.</param>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="content">The body of the request, for methods that require it.</param>
    /// <param name="dontRefresh">Used to prevent cyclic refresh loops.</param>
    /// <returns>A <see cref="HttpResponseMessage"/> containing the response data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Did not call <see cref="Init(string, string?, string)"/> with a valid Client Id or <paramref name="session"/> does not have an OAuth token.</exception>
    /// <remarks>
    /// <para>
    /// Non-JSON responses are converted to the standard <c>{status,error,message}</c> format.
    /// </para>
    /// <para>
    /// If a 401 is returned, one attempt will be made to refresh the OAuth token via <see cref="TwitchSession.RefreshTokenCallback"/> (when provided) or the standalone OAuth/App refresh flow, and retry the request.
    /// </para>
    /// </remarks>
    internal static async Task<HttpResponseMessage> PerformHttpRequest(HttpMethod method, Uri uri, TwitchSession session, HttpContent? content = null, bool dontRefresh = false)
    {
        ArgumentNullException.ThrowIfNull(session);

        string? clientId = session.CustomClientId ?? ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Must call TwitchAPI.Init.");
        }

        if (session.Token != TwitchToken.Empty && string.IsNullOrWhiteSpace(session.Token?.OAuth))
        {
            throw new InvalidOperationException("Invalid OAuth token in session.");
        }

        byte[]? bufferedContentBytes = null;
        KeyValuePair<string, IEnumerable<string>>[]? bufferedContentHeaders = null;
        if (content is not null)
        {
            bufferedContentBytes = await content.ReadAsByteArrayAsync(session.CancellationToken).ConfigureAwait(false);
            bufferedContentHeaders = [.. content.Headers];
        }

        bool retry = false;
    performhttprequest_start:
        try
        {
            await session.RateLimiter.WaitForRateLimit(TimeSpan.FromSeconds(30), session.CancellationToken).ConfigureAwait(false);

            Uri targetUri = !uri.IsAbsoluteUri && uri.OriginalString.StartsWith('/')
                ? new(uri.OriginalString.TrimStart('/'), UriKind.Relative)
                : uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeFile
                    ? new((uri.PathAndQuery + uri.Fragment).TrimStart('/'), UriKind.Relative)
                    : uri;

            if (session.CustomBaseUri is not null && !targetUri.IsAbsoluteUri)
            {
                targetUri = new Uri(session.CustomBaseUri, targetUri);
            }

            using HttpRequestMessage request = new(method, targetUri);
            if (session.CustomHttpClient is null)
            {
                request.Version = _httpClient.DefaultRequestVersion;
                request.VersionPolicy = _httpClient.DefaultVersionPolicy;
            }

            request.Headers.Add("Client-Id", clientId);
            if (bufferedContentBytes is not null)
            {
                ByteArrayContent attemptContent = new(bufferedContentBytes);
                if (bufferedContentHeaders is not null)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> header in bufferedContentHeaders)
                    {
                        if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                        {
                            _ = attemptContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }

                request.Content = attemptContent;
            }

            if (session.Token != TwitchToken.Empty)
            {
                request.Headers.Add("Authorization", "Bearer " + session.Token.OAuth);
            }

            HttpMessageInvoker httpClient = session.CustomHttpClient ?? _httpClient;
            HttpResponseMessage response = await httpClient.SendAsync(request, session.CancellationToken).ConfigureAwait(false);

            if (!retry && !dontRefresh && response.StatusCode == HttpStatusCode.Unauthorized && session.Token != TwitchToken.Empty)
            {
                if (session.RefreshTokenCallback is not null)
                {
                    string? oldOAuth = session.Token.OAuth;
                    if (await session._refreshLock.WaitAsync(TimeSpan.FromSeconds(30), session.CancellationToken).ConfigureAwait(false))
                    {
                        try
                        {
                            if (session.Token.OAuth == oldOAuth)
                            {
                                TwitchToken? refreshed = await session.RefreshTokenCallback(session, session.CancellationToken).ConfigureAwait(false);
                                if (refreshed is not null)
                                {
                                    session.Token = refreshed;
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
                            _ = session._refreshLock.Release();
                        }
                    }

                    if (retry)
                    {
                        response.Dispose();
                        goto performhttprequest_start;
                    }
                }
                else
                {
                    switch (session.Token.Type.GetValueOrDefault())
                    {
                        case TwitchToken.TokenType.User:
                            if (!string.IsNullOrWhiteSpace(session.Token.Refresh))
                            {
                                string oldRefresh = session.Token.Refresh;
                                if (await session._refreshLock.WaitAsync(TimeSpan.FromSeconds(30), session.CancellationToken).ConfigureAwait(false))
                                {
                                    try
                                    {
                                        if (session.Token.Refresh == oldRefresh)
                                        {
                                            Token? refresh = await Token.RefreshOAuth(session).ConfigureAwait(false);

                                            if (refresh is not null && refresh.IsSuccessStatusCode)
                                            {
                                                session.Token = TwitchToken.FromApiResponse(refresh, session.Token);
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
                                        _ = session._refreshLock.Release();
                                    }
                                }

                                if (retry)
                                {
                                    response.Dispose();
                                    goto performhttprequest_start;
                                }
                            }
                            break;
                        case TwitchToken.TokenType.App:
                            if (!string.IsNullOrWhiteSpace(session.Token.OAuth))
                            {
                                string oldOAuth = session.Token.OAuth;
                                if (await session._refreshLock.WaitAsync(TimeSpan.FromSeconds(30), session.CancellationToken).ConfigureAwait(false))
                                {
                                    try
                                    {
                                        if (session.Token.OAuth == oldOAuth)
                                        {
                                            Token? refresh = await Token.AuthorizeApp(session: session).ConfigureAwait(false);

                                            if (refresh is not null && refresh.IsSuccessStatusCode)
                                            {
                                                session.Token = TwitchToken.FromApiResponse(refresh, session.Token);
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
                                        _ = session._refreshLock.Release();
                                    }
                                }

                                if (retry)
                                {
                                    response.Dispose();
                                    goto performhttprequest_start;
                                }
                            }
                            break;
                        case TwitchToken.TokenType.Jwt:
                        case TwitchToken.TokenType.Unknown:
                        default:
                            break;
                    }
                }
            }

            if (response.Content.Headers?.ContentType?.MediaType != "application/json")
            {
                string rcontent = await response.Content.ReadAsStringAsync(session.CancellationToken).ConfigureAwait(false);
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
    private static HttpClient _httpClient = CreateHttpClient();

    #endregion Private Fields

    #region Private Methods

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> configured with socket connection pooling.
    /// </summary>
    /// <returns>A new <see cref="HttpClient"/> instance.</returns>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClient takes ownership of and disposes the SocketsHttpHandler.")]
    private static HttpClient CreateHttpClient() => new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    }, disposeHandler: true);

    /// <summary>
    /// Matches JSON object or array strings.
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"^\s*[\{\[].*[\}\]]\s*$", RegexOptions.Singleline)]
    private static partial Regex JSONRegex();

    #endregion Private Methods

    #region Private Classes

    /// <summary>
    /// Converter factory supporting nullable and non-nullable enums decorated with <see cref="JsonConverterAttribute"/>.
    /// </summary>
    private sealed class CustomEnumConverterFactory : JsonConverterFactory
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            Type enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            return enumType.IsEnum && enumType.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is not null;
        }

        /// <inheritdoc/>
        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(typeToConvert);
            Type enumType = underlyingType ?? typeToConvert;
            Type innerConverterType = enumType.GetCustomAttribute<JsonConverterAttribute>()!.ConverterType!;
            if (underlyingType is null)
            {
                return (JsonConverter?)Activator.CreateInstance(innerConverterType);
            }

            Type wrapperType = typeof(NullableEnumConverter<,>).MakeGenericType(enumType, innerConverterType);
            return (JsonConverter?)Activator.CreateInstance(wrapperType);
        }
    }

    /// <summary>
    /// Wraps a non-nullable enum converter to handle nullable enum properties.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <typeparam name="TConverter">The underlying enum converter type.</typeparam>
    private sealed class NullableEnumConverter<TEnum, TConverter> : JsonConverter<TEnum?>
        where TEnum : struct, Enum
        where TConverter : JsonConverter<TEnum>, new()
    {
        /// <summary>
        /// The inner converter for the non-nullable enum type.
        /// </summary>
        private readonly TConverter _inner = new();

        /// <inheritdoc/>
        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : this._inner.Read(ref reader, typeof(TEnum), options);

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                this._inner.Write(writer, value.Value, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    /// <summary>
    /// Serializes polymorphic <see cref="EventSubCondition"/> instances using their runtime type.
    /// </summary>
    private sealed class EventSubConditionConverter : JsonConverter<EventSubCondition>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(EventSubCondition);

        /// <inheritdoc/>
        public override EventSubCondition? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            reader.Skip();
            return null;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, EventSubCondition value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    #endregion Private Classes

}

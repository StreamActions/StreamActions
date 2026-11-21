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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Http;
using StreamActions.Core.Models;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Services;

/// <summary>
/// SSRF-hardened <see cref="ISafeExternalHttpClient"/> implementation with DNS/IP validation,
/// host-allowlisted workspace secret injection, manual redirect enforcement, bounded response streaming,
/// timeout enforcement, and secret redaction.
/// </summary>
public sealed partial class SafeExternalHttpClient : ISafeExternalHttpClient, IDisposable
{
    /// <summary>
    /// Maximum hard timeout ceiling applied to every outbound HTTP request.
    /// </summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of HTTP redirects followed per request.
    /// </summary>
    public const int MaxRedirects = 3;

    /// <summary>
    /// Maximum number of decompressed response bytes permitted per request (64 KiB).
    /// </summary>
    public const int MaxResponseBytes = BoundedReadStream.DefaultMaxBytes;

    /// <summary>
    /// Prefix identifying workspace secret tokens inside outbound URLs and headers.
    /// </summary>
    private const string _secretTokenPrefix = "$secret.";

    /// <summary>
    /// Characters terminating the authority section of a URI.
    /// </summary>
    private static readonly char[] _authorityTerminators = ['/', '?', '#', '\\'];

    /// <summary>
    /// Secret redactor used to scrub outbound secrets from responses and exception messages.
    /// </summary>
    private readonly ISecretRedactor _secretRedactor;

    /// <summary>
    /// Time provider used for deterministic timeout enforcement.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger for outbound HTTP safety diagnostics.
    /// </summary>
    private readonly ILogger<SafeExternalHttpClient> _logger;

    /// <summary>
    /// Optional secret vault used to resolve <c>$secret.NAME</c> tokens.
    /// </summary>
    private readonly ISecretVault? _secretVault;

    /// <summary>
    /// Optional test seam overriding DNS host resolution while keeping IP validation active.
    /// </summary>
    private readonly Func<string, CancellationToken, ValueTask<IPAddress[]>>? _resolveHostAddressesAsync;

    /// <summary>
    /// Optional test seam rewriting the already-validated <see cref="IPEndPoint"/> to a local ephemeral server.
    /// </summary>
    private readonly Func<IPEndPoint, IPEndPoint>? _rewriteTestEndPoint;

    /// <summary>
    /// Underlying <see cref="SocketsHttpHandler"/> configured with SSRF-safe connection validation.
    /// </summary>
    private readonly SocketsHttpHandler _handler;

    /// <summary>
    /// Underlying <see cref="HttpClient"/> executing requests without automatic redirects or cookies.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Tracks whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeExternalHttpClient"/> class for dependency injection.
    /// </summary>
    /// <param name="secretRedactor">The secret redactor.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="secretVault">The optional workspace secret vault.</param>
    [ActivatorUtilitiesConstructor]
    public SafeExternalHttpClient(
        ISecretRedactor secretRedactor,
        TimeProvider timeProvider,
        ILogger<SafeExternalHttpClient> logger,
        ISecretVault? secretVault = null)
        : this(secretRedactor, timeProvider, logger, secretVault, resolveHostAddressesAsync: null, rewriteTestEndPoint: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeExternalHttpClient"/> class with optional test seams for DNS resolution and endpoint rewriting.
    /// </summary>
    /// <param name="secretRedactor">The secret redactor.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="secretVault">The optional workspace secret vault.</param>
    /// <param name="resolveHostAddressesAsync">Optional DNS resolution delegate used by tests.</param>
    /// <param name="rewriteTestEndPoint">Optional endpoint rewrite delegate applied only after IP validation passes.</param>
    internal SafeExternalHttpClient(
        ISecretRedactor secretRedactor,
        TimeProvider timeProvider,
        ILogger<SafeExternalHttpClient> logger,
        ISecretVault? secretVault,
        Func<string, CancellationToken, ValueTask<IPAddress[]>>? resolveHostAddressesAsync,
        Func<IPEndPoint, IPEndPoint>? rewriteTestEndPoint)
    {
        ArgumentNullException.ThrowIfNull(secretRedactor);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._secretRedactor = secretRedactor;
        this._timeProvider = timeProvider;
        this._logger = logger;
        this._secretVault = secretVault;
        this._resolveHostAddressesAsync = resolveHostAddressesAsync;
        this._rewriteTestEndPoint = rewriteTestEndPoint;

        this._handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectCallback = this.ConnectSafelyAsync,
        };

        this._httpClient = new HttpClient(this._handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <inheritdoc/>
    public async ValueTask<SafeExternalHttpResponse> SendAsync(
        SafeExternalHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        TimeSpan effectiveTimeout = ResolveEffectiveTimeout(request.Timeout);
        Uri initialUri = ParseAndValidateInitialUri(request.Url);
        ValidateDirectTargetHost(initialUri);

        using CancellationTokenSource timeoutCts = new(effectiveTimeout, this._timeProvider);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            Dictionary<string, ResolvedSecret> resolvedSecrets = await this.ResolveReferencedSecretsAsync(
                request,
                initialUri,
                linkedCts.Token).ConfigureAwait(false);

            Uri currentUri = SubstituteSecretsInUri(initialUri, resolvedSecrets);
            List<ActiveHeader> activeHeaders = BuildInitialHeaders(request.Headers, resolvedSecrets);
            HttpMethod currentMethod = request.Method ?? HttpMethod.Get;
            string? currentBody = request.Body;
            string? currentContentType = request.ContentType;
            int redirectCount = 0;

            while (true)
            {
                using HttpResponseMessage response = await this.SendHopAsync(
                    currentMethod,
                    currentUri,
                    activeHeaders,
                    currentBody,
                    currentContentType,
                    linkedCts.Token).ConfigureAwait(false);

                if (IsRedirectStatusCode(response.StatusCode) && (response.Headers.Location is not null))
                {
                    redirectCount++;
                    if (redirectCount > MaxRedirects)
                    {
                        throw new SafeExternalHttpException(
                            SafeExternalHttpFailureReason.TooManyRedirects,
                            $"External HTTP request exceeded the maximum limit of {MaxRedirects} redirects.");
                    }

                    Uri nextUri = ResolveAndValidateRedirectTarget(currentUri, response.Headers.Location);
                    ValidateSecretHostAllowlistsForRedirect(resolvedSecrets.Values, nextUri);

                    if (!IsSameOrigin(currentUri, nextUri))
                    {
                        activeHeaders = StripSensitiveHeaders(activeHeaders, resolvedSecrets.Values);
                        nextUri = StripSensitiveQueryParameters(nextUri, resolvedSecrets.Values);
                    }

                    (currentMethod, currentBody, currentContentType) = ApplyRedirectMethodRules(
                        response.StatusCode,
                        currentMethod,
                        currentBody,
                        currentContentType);

                    currentUri = nextUri;
                    continue;
                }

                return await this.ReadSafeResponseAsync(response, currentUri, redirectCount, linkedCts.Token).ConfigureAwait(false);
            }
        }
        catch (SafeExternalHttpException)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            LogRequestTimeout(this._logger, initialUri.IdnHost, effectiveTimeout);
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.Timeout,
                $"External HTTP request timed out after {effectiveTimeout.TotalSeconds:0.###} seconds.",
                new TimeoutException("External HTTP request timed out.", ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            if (FindInnerSafeExternalHttpException(ex) is SafeExternalHttpException safeInner)
            {
                throw safeInner;
            }

            string redactedMessage = this._secretRedactor.Redact(ex.Message) ?? "External HTTP request failed.";
            LogTransportFailure(this._logger, initialUri.IdnHost, redactedMessage);
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.RequestFailed,
                redactedMessage,
                new HttpRequestException(redactedMessage, null, ex.StatusCode));
        }
        catch (IOException ex)
        {
            if (FindInnerSafeExternalHttpException(ex) is SafeExternalHttpException safeInner)
            {
                throw safeInner;
            }

            string redactedMessage = this._secretRedactor.Redact(ex.Message) ?? "External HTTP response stream failed.";
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.RequestFailed,
                redactedMessage,
                new IOException(redactedMessage));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        this._httpClient.Dispose();
        this._handler.Dispose();
    }

    /// <summary>
    /// Resolves the effective timeout clamped to <see cref="MaxTimeout"/>.
    /// </summary>
    /// <param name="requestedTimeout">The caller-requested timeout, or <see langword="null"/>.</param>
    /// <returns>The validated effective timeout.</returns>
    private static TimeSpan ResolveEffectiveTimeout(TimeSpan? requestedTimeout) =>
        !requestedTimeout.HasValue
            ? MaxTimeout
            : requestedTimeout.Value <= TimeSpan.Zero
            ? throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.InvalidRequest,
                "Request timeout must be a positive duration.")
            : requestedTimeout.Value > MaxTimeout ? MaxTimeout : requestedTimeout.Value;

    /// <summary>
    /// Parses the initial request URL and enforces scheme and no-secret-in-authority rules.
    /// </summary>
    /// <param name="rawUrl">The raw URL string supplied by the caller.</param>
    /// <returns>The validated absolute <see cref="Uri"/>.</returns>
    private static Uri ParseAndValidateInitialUri(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.InvalidRequest,
                "Request URL must not be empty.");
        }

        string trimmed = rawUrl.Trim();
        CheckRawAuthorityForSecretReference(trimmed);

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsedUri))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.InvalidRequest,
                "Request URL is not a valid absolute URI.");
        }

        ValidateAllowedScheme(parsedUri);
        CheckParsedAuthorityForSecretReference(parsedUri);

        return parsedUri;
    }

    /// <summary>
    /// Rejects raw <c>$secret.</c> tokens in the URI authority segment prior to parsing.
    /// </summary>
    /// <param name="trimmedUrl">The trimmed URL string.</param>
    private static void CheckRawAuthorityForSecretReference(string trimmedUrl)
    {
        int schemeSeparatorIndex = trimmedUrl.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex < 0)
        {
            return;
        }

        string afterScheme = trimmedUrl[(schemeSeparatorIndex + 3)..];
        int authorityEnd = afterScheme.IndexOfAny(_authorityTerminators);
        string rawAuthority = authorityEnd >= 0 ? afterScheme[..authorityEnd] : afterScheme;

        if (ContainsSecretPrefix(rawAuthority))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.SecretInAuthority,
                "Secret references ($secret.*) are not permitted inside URI authority, host, port, or userinfo.");
        }
    }

    /// <summary>
    /// Rejects <c>$secret.</c> tokens inside parsed URI authority, host, or userinfo components.
    /// </summary>
    /// <param name="uri">The parsed absolute URI.</param>
    private static void CheckParsedAuthorityForSecretReference(Uri uri)
    {
        string authorityLeftPart = uri.GetLeftPart(UriPartial.Authority);
        if (ContainsSecretPrefix(authorityLeftPart)
            || ContainsSecretPrefix(uri.UserInfo)
            || ContainsSecretPrefix(uri.Host))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.SecretInAuthority,
                "Secret references ($secret.*) are not permitted inside URI authority, host, port, or userinfo.");
        }
    }

    /// <summary>
    /// Checks whether a string or its URI-unescaped form contains <c>$secret.</c>.
    /// </summary>
    /// <param name="value">The text to inspect.</param>
    /// <returns><see langword="true"/> if a secret reference prefix was detected; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsSecretPrefix(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Contains(_secretTokenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string unescaped = Uri.UnescapeDataString(value);
        return unescaped.Contains(_secretTokenPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures the URI scheme is <c>http</c> or <c>https</c>.
    /// </summary>
    /// <param name="uri">The URI to validate.</param>
    private static void ValidateAllowedScheme(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.DisallowedScheme,
                $"URI scheme '{uri.Scheme}' is not allowed. Only http and https are permitted.");
        }
    }

    /// <summary>
    /// Validates direct IP literals and <c>localhost</c> hostnames immediately before DNS or secret resolution.
    /// </summary>
    /// <param name="uri">The target URI.</param>
    private static void ValidateDirectTargetHost(Uri uri)
    {
        string host = uri.DnsSafeHost;
        if (SafeExternalIpAddressValidator.IsLocalhostHostName(host))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.BlockedIpAddress,
                $"Target host '{host}' is blocked by SSRF policy.");
        }

        if (IPAddress.TryParse(host, out IPAddress? literalIp) && !SafeExternalIpAddressValidator.IsAllowed(literalIp))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.BlockedIpAddress,
                $"Target IP address '{host}' is blocked by SSRF policy.");
        }
    }

    /// <summary>
    /// Extracts referenced secret names from the URL path/query and request header values.
    /// </summary>
    /// <param name="uri">The initial validated URI.</param>
    /// <param name="headers">Optional request headers.</param>
    /// <returns>The distinct normalized secret names referenced by the request.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes secret names to lowercase invariant.")]
    private static HashSet<string> ExtractReferencedSecretNames(
        Uri uri,
        IReadOnlyDictionary<string, string>? headers)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        string pathAndQuery = string.Concat(uri.AbsolutePath, uri.Query);

        CollectSecretNames(pathAndQuery, names);

        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!string.IsNullOrEmpty(header.Value))
                {
                    CollectSecretNames(header.Value, names);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// Collects normalized secret names from a single text value and rejects malformed <c>$secret.</c> placeholders.
    /// </summary>
    /// <param name="input">The text to scan.</param>
    /// <param name="names">The destination set of normalized secret names.</param>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes secret names to lowercase invariant.")]
    private static void CollectSecretNames(string input, HashSet<string> names)
    {
        MatchCollection matches = SecretTokenRegex().Matches(input);
        foreach (Match match in matches)
        {
            _ = names.Add(match.Groups[1].Value.ToLowerInvariant());
        }

        if ((matches.Count == 0) && input.Contains(_secretTokenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.InvalidRequest,
                "Malformed $secret.NAME placeholder detected.");
        }
    }

    /// <summary>
    /// Substitutes resolved <c>$secret.NAME</c> tokens in the URI path and query with URL-escaped secret values.
    /// </summary>
    /// <param name="initialUri">The original validated URI.</param>
    /// <param name="resolvedSecrets">The resolved secrets keyed by normalized secret name.</param>
    /// <returns>The URI with secret tokens substituted.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes secret names to lowercase invariant.")]
    private static Uri SubstituteSecretsInUri(
        Uri initialUri,
        IReadOnlyDictionary<string, ResolvedSecret> resolvedSecrets)
    {
        if (resolvedSecrets.Count == 0)
        {
            return initialUri;
        }

        string pathAndQuery = string.Concat(initialUri.AbsolutePath, initialUri.Query);
        string substitutedPathAndQuery = SecretTokenRegex().Replace(
            pathAndQuery,
            match =>
            {
                string key = match.Groups[1].Value.ToLowerInvariant();
                return resolvedSecrets[key].EscapedValue;
            });

        string authority = initialUri.GetLeftPart(UriPartial.Authority);
        return new Uri(string.Concat(authority, substitutedPathAndQuery), UriKind.Absolute);
    }

    /// <summary>
    /// Builds initial active headers after substituting <c>$secret.NAME</c> tokens in header values.
    /// </summary>
    /// <param name="headers">The caller-supplied headers.</param>
    /// <param name="resolvedSecrets">The resolved secrets keyed by normalized secret name.</param>
    /// <returns>The list of active headers for the initial request.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes secret names to lowercase invariant.")]
    private static List<ActiveHeader> BuildInitialHeaders(
        IReadOnlyDictionary<string, string>? headers,
        IReadOnlyDictionary<string, ResolvedSecret> resolvedSecrets)
    {
        if (headers is null || (headers.Count == 0))
        {
            return [];
        }

        List<ActiveHeader> result = new(headers.Count);
        foreach (KeyValuePair<string, string> entry in headers)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)
                || ContainsControlOrNewline(entry.Key))
            {
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.InvalidRequest,
                    "Request header name is empty or contains invalid characters.");
            }

            string rawValue = entry.Value ?? string.Empty;
            bool hadSecretToken = false;
            string resolvedValue = rawValue;

            if (resolvedSecrets.Count > 0)
            {
                resolvedValue = SecretTokenRegex().Replace(
                    rawValue,
                    match =>
                    {
                        hadSecretToken = true;
                        string key = match.Groups[1].Value.ToLowerInvariant();
                        return resolvedSecrets[key].PlaintextValue;
                    });
            }

            if (ContainsControlOrNewline(resolvedValue))
            {
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.InvalidRequest,
                    $"Header '{entry.Key}' contains invalid control or newline characters.");
            }

            result.Add(new(entry.Key.Trim(), resolvedValue, hadSecretToken));
        }

        return result;
    }

    /// <summary>
    /// Checks whether a header name or value contains CR, LF, or NUL characters.
    /// </summary>
    /// <param name="value">The text to check.</param>
    /// <returns><see langword="true"/> if forbidden characters are present; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsControlOrNewline(string value) =>
        value.IndexOfAny(['\r', '\n', '\0']) >= 0;

    /// <summary>
    /// Resolves the <see cref="MediaTypeHeaderValue"/> for an outbound request body.
    /// </summary>
    /// <param name="contentType">The explicit request content type, if provided.</param>
    /// <param name="headerContentType">The <c>Content-Type</c> header value, if provided.</param>
    /// <returns>The parsed <see cref="MediaTypeHeaderValue"/>.</returns>
    private static MediaTypeHeaderValue ResolveRequestMediaType(string? contentType, string? headerContentType)
    {
        string effectiveMediaType = !string.IsNullOrWhiteSpace(contentType)
            ? contentType
            : !string.IsNullOrWhiteSpace(headerContentType)
                ? headerContentType
                : "text/plain";

        return MediaTypeHeaderValue.Parse(effectiveMediaType);
    }

    /// <summary>
    /// Sends a single HTTP hop with configured headers and optional body content.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The target URI.</param>
    /// <param name="headers">The active headers for this hop.</param>
    /// <param name="body">The optional body text.</param>
    /// <param name="contentType">The optional body media type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The raw <see cref="HttpResponseMessage"/>.</returns>
    private async ValueTask<HttpResponseMessage> SendHopAsync(
        HttpMethod method,
        Uri uri,
        IReadOnlyList<ActiveHeader> headers,
        string? body,
        string? contentType,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = new(method, uri);
        string? headerContentType = null;

        foreach (ActiveHeader header in headers)
        {
            if (string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                headerContentType = header.ResolvedValue;
                continue;
            }

            _ = message.Headers.TryAddWithoutValidation(header.Name, header.ResolvedValue);
        }

        if (body is not null)
        {
            MediaTypeHeaderValue parsedMediaType = ResolveRequestMediaType(contentType, headerContentType);
            using StringContent content = new(body, Encoding.UTF8);
            content.Headers.ContentType = parsedMediaType;
            message.Content = content;

            return await this._httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }

        return await this._httpClient
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether an HTTP status code triggers manual redirect handling.
    /// </summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    /// <returns><see langword="true"/> for 301, 302, 303, 307, and 308; otherwise, <see langword="false"/>.</returns>
    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    /// <summary>
    /// Resolves and validates a redirect <c>Location</c> target URI against scheme, authority-secret, and direct SSRF guards.
    /// </summary>
    /// <param name="currentUri">The URI of the hop that returned the redirect.</param>
    /// <param name="location">The redirect <c>Location</c> URI.</param>
    /// <returns>The validated absolute target URI for the next hop.</returns>
    private static Uri ResolveAndValidateRedirectTarget(Uri currentUri, Uri location)
    {
        Uri resolved;
        if (location.IsAbsoluteUri)
        {
            ValidateAllowedScheme(location);
            CheckParsedAuthorityForSecretReference(location);
            resolved = location;
        }
        else
        {
            if (!Uri.TryCreate(currentUri, location, out Uri? combined))
            {
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.InvalidRequest,
                    "Redirect Location header could not be resolved into a valid URI.");
            }

            ValidateAllowedScheme(combined);
            CheckParsedAuthorityForSecretReference(combined);
            resolved = combined;
        }

        ValidateDirectTargetHost(resolved);
        return resolved;
    }

    /// <summary>
    /// Re-validates every resolved secret's <c>AllowedHostPatterns</c> against a redirect target host.
    /// </summary>
    /// <param name="resolvedSecrets">The secrets resolved for this request.</param>
    /// <param name="redirectTarget">The redirect target URI.</param>
    private static void ValidateSecretHostAllowlistsForRedirect(
        IEnumerable<ResolvedSecret> resolvedSecrets,
        Uri redirectTarget)
    {
        foreach (ResolvedSecret secret in resolvedSecrets)
        {
            if (!HostPatternMatcher.IsHostAllowed(secret.AllowedHostPatterns, redirectTarget.IdnHost))
            {
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.SecretHostNotAllowed,
                    $"Redirect target host '{redirectTarget.IdnHost}' is not permitted by secret '{secret.SecretName}' AllowedHostPatterns.",
                    secretResolutionStatus: SecretResolutionStatus.HostNotAllowed);
            }
        }
    }

    /// <summary>
    /// Determines whether two absolute URIs share the exact same scheme, normalized host, and port.
    /// </summary>
    /// <param name="left">The current URI.</param>
    /// <param name="right">The redirect target URI.</param>
    /// <returns><see langword="true"/> if same-origin; otherwise, <see langword="false"/>.</returns>
    private static bool IsSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase)
        && (left.Port == right.Port);

    /// <summary>
    /// Strips <c>Authorization</c>, <c>Proxy-Authorization</c>, <c>Cookie</c>, and secret-bearing headers on cross-origin, scheme-downgrade, or port-change redirects.
    /// </summary>
    /// <param name="headers">The active request headers.</param>
    /// <param name="resolvedSecrets">The secrets resolved for this request.</param>
    /// <returns>The filtered header list for the redirected hop.</returns>
    private static List<ActiveHeader> StripSensitiveHeaders(
        IReadOnlyList<ActiveHeader> headers,
        IReadOnlyCollection<ResolvedSecret> resolvedSecrets)
    {
        List<ActiveHeader> filtered = new(headers.Count);
        foreach (ActiveHeader header in headers)
        {
            if (string.Equals(header.Name, "Authorization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header.Name, "Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header.Name, "Cookie", StringComparison.OrdinalIgnoreCase)
                || header.HadSecretToken
                || ContainsAnySecretValue(header.ResolvedValue, resolvedSecrets))
            {
                continue;
            }

            filtered.Add(header);
        }

        return filtered;
    }

    /// <summary>
    /// Strips <c>$secret.*</c>-bearing or secret-value-matching query parameters on cross-origin, scheme-downgrade, or port-change redirects.
    /// </summary>
    /// <param name="targetUri">The redirect target URI.</param>
    /// <param name="resolvedSecrets">The secrets resolved for this request.</param>
    /// <returns>The sanitized redirect URI.</returns>
    private static Uri StripSensitiveQueryParameters(
        Uri targetUri,
        IReadOnlyCollection<ResolvedSecret> resolvedSecrets)
    {
        if (string.IsNullOrEmpty(targetUri.Query))
        {
            return targetUri;
        }

        string rawQuery = targetUri.Query.TrimStart('?');
        string[] segments = rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);
        List<string> kept = new(segments.Length);

        foreach (string segment in segments)
        {
            if (ContainsSecretPrefix(segment) || ContainsAnySecretValue(segment, resolvedSecrets))
            {
                continue;
            }

            string unescapedSegment = Uri.UnescapeDataString(segment);
            if (ContainsAnySecretValue(unescapedSegment, resolvedSecrets))
            {
                continue;
            }

            kept.Add(segment);
        }

        string basePart = targetUri.GetLeftPart(UriPartial.Path);
        string rebuilt = kept.Count > 0 ? $"{basePart}?{string.Join('&', kept)}" : basePart;
        return new Uri(rebuilt, UriKind.Absolute);
    }

    /// <summary>
    /// Checks whether a string contains any resolved secret's raw or URL-escaped plaintext value.
    /// </summary>
    /// <param name="input">The text to inspect.</param>
    /// <param name="resolvedSecrets">The resolved secrets.</param>
    /// <returns><see langword="true"/> if a secret value is found; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsAnySecretValue(string input, IReadOnlyCollection<ResolvedSecret> resolvedSecrets)
    {
        if (string.IsNullOrEmpty(input) || (resolvedSecrets.Count == 0))
        {
            return false;
        }

        foreach (ResolvedSecret secret in resolvedSecrets)
        {
            if ((!string.IsNullOrEmpty(secret.PlaintextValue) && input.Contains(secret.PlaintextValue, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(secret.EscapedValue) && input.Contains(secret.EscapedValue, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Applies HTTP redirect method and body rewrite rules for 301/302/303 vs 307/308.
    /// </summary>
    /// <param name="statusCode">The redirect HTTP status code.</param>
    /// <param name="currentMethod">The current HTTP method.</param>
    /// <param name="currentBody">The current request body.</param>
    /// <param name="currentContentType">The current request content type.</param>
    /// <returns>The updated method, body, and content type for the next hop.</returns>
    private static (HttpMethod Method, string? Body, string? ContentType) ApplyRedirectMethodRules(
        HttpStatusCode statusCode,
        HttpMethod currentMethod,
        string? currentBody,
        string? currentContentType)
    {
        if ((statusCode == HttpStatusCode.SeeOther)
            || ((statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found) && (currentMethod == HttpMethod.Post)))
        {
            return (HttpMethod.Get, null, null);
        }

        return (currentMethod, currentBody, currentContentType);
    }

    /// <summary>
    /// Resolves the text <see cref="Encoding"/> specified by the response <c>Content-Type</c> charset, defaulting to UTF-8.
    /// </summary>
    /// <param name="response">The HTTP response message.</param>
    /// <returns>The resolved <see cref="Encoding"/>.</returns>
    private static Encoding ResolveResponseEncoding(HttpResponseMessage response)
    {
        string? charset = response.Content.Headers.ContentType?.CharSet;
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        string trimmed = charset.Trim().Trim('"');
        try
        {
            return Encoding.GetEncoding(trimmed);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Searches an exception's inner chain for an underlying <see cref="SafeExternalHttpException"/>.
    /// </summary>
    /// <param name="exception">The outer exception.</param>
    /// <returns>The nested <see cref="SafeExternalHttpException"/>, or <see langword="null"/> if none was found.</returns>
    private static SafeExternalHttpException? FindInnerSafeExternalHttpException(Exception exception)
    {
        Exception? current = exception.InnerException;
        while (current is not null)
        {
            if (current is SafeExternalHttpException safeEx)
            {
                return safeEx;
            }

            current = current.InnerException;
        }

        return null;
    }

    /// <summary>
    /// Resolves all distinct <c>$secret.NAME</c> tokens referenced by the initial request and registers them with <see cref="ISecretRedactor"/>.
    /// </summary>
    /// <param name="request">The outbound HTTP request.</param>
    /// <param name="initialUri">The parsed initial URI.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary of resolved secrets keyed by normalized secret name.</returns>
    private async ValueTask<Dictionary<string, ResolvedSecret>> ResolveReferencedSecretsAsync(
        SafeExternalHttpRequest request,
        Uri initialUri,
        CancellationToken cancellationToken)
    {
        HashSet<string> secretNames = ExtractReferencedSecretNames(initialUri, request.Headers);
        Dictionary<string, ResolvedSecret> resolved = new(StringComparer.Ordinal);

        if (secretNames.Count == 0)
        {
            return resolved;
        }

        if (!request.WorkspaceId.HasValue || (request.WorkspaceId.Value == Guid.Empty) || (this._secretVault is null))
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.SecretResolutionFailed,
                "A valid WorkspaceId and ISecretVault are required when referencing $secret.* tokens.");
        }

        foreach (string secretName in secretNames)
        {
            SecretResolutionResult result = await this._secretVault
                .ResolveForHostAsync(request.WorkspaceId.Value, secretName, initialUri, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                SafeExternalHttpFailureReason failureReason = result.Status == SecretResolutionStatus.HostNotAllowed
                    ? SafeExternalHttpFailureReason.SecretHostNotAllowed
                    : SafeExternalHttpFailureReason.SecretResolutionFailed;

                throw new SafeExternalHttpException(
                    failureReason,
                    $"Secret '{secretName}' could not be resolved for host '{initialUri.IdnHost}' ({result.Status}).",
                    secretResolutionStatus: result.Status);
            }

            string plaintext = result.PlaintextValue!;
            string escaped = Uri.EscapeDataString(plaintext);

            this._secretRedactor.RegisterSecret(plaintext);
            this._secretRedactor.RegisterSecret(escaped);

            resolved[secretName] = new(
                secretName,
                plaintext,
                escaped,
                result.AllowedHostPatterns ?? []);
        }

        return resolved;
    }

    /// <summary>
    /// Reads the HTTP response body within <see cref="MaxResponseBytes"/> and scrubs body, headers, and final URI through <see cref="ISecretRedactor"/>.
    /// </summary>
    /// <param name="response">The HTTP response message.</param>
    /// <param name="finalUri">The final request URI.</param>
    /// <param name="redirectCount">The number of redirects followed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The scrubbed <see cref="SafeExternalHttpResponse"/>.</returns>
    private async ValueTask<SafeExternalHttpResponse> ReadSafeResponseAsync(
        HttpResponseMessage response,
        Uri finalUri,
        int redirectCount,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.ResponseTooLarge,
                $"Response Content-Length exceeds the maximum allowed limit of {MaxResponseBytes} bytes.");
        }

        using MemoryStream memoryStream = new();
        Stream rawStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (rawStream.ConfigureAwait(false))
        {
            BoundedReadStream boundedStream = new(rawStream, MaxResponseBytes);
            await using (boundedStream.ConfigureAwait(false))
            {
                await boundedStream.CopyToAsync(memoryStream, 8192, cancellationToken).ConfigureAwait(false);
            }
        }

        Encoding encoding = ResolveResponseEncoding(response);
        string rawBody = memoryStream.Length == 0
            ? string.Empty
            : encoding.GetString(memoryStream.GetBuffer(), 0, checked((int)memoryStream.Length));
        string redactedBody = this._secretRedactor.Redact(rawBody) ?? string.Empty;

        Dictionary<string, IReadOnlyList<string>> scrubbedHeaders = new(StringComparer.OrdinalIgnoreCase);
        this.CopyScrubbedHeaders(response.Headers, scrubbedHeaders);
        this.CopyScrubbedHeaders(response.Content.Headers, scrubbedHeaders);

        Uri safeFinalUri = this.ScrubFinalUri(finalUri);

        return new(
            (int)response.StatusCode,
            redactedBody,
            scrubbedHeaders,
            safeFinalUri,
            redirectCount);
    }

    /// <summary>
    /// Copies HTTP headers while scrubbing all values via <see cref="ISecretRedactor"/>.
    /// </summary>
    /// <param name="source">The source header collection.</param>
    /// <param name="destination">The destination scrubbed header dictionary.</param>
    private void CopyScrubbedHeaders(
        HttpHeaders source,
        Dictionary<string, IReadOnlyList<string>> destination)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            List<string> values = [];
            foreach (string value in header.Value)
            {
                values.Add(this._secretRedactor.Redact(value) ?? string.Empty);
            }

            destination[header.Key] = values;
        }
    }

    /// <summary>
    /// Scrubs any registered secret values from <paramref name="finalUri"/>.
    /// </summary>
    /// <param name="finalUri">The final target URI.</param>
    /// <returns>A URI safe to expose to callers.</returns>
    private Uri ScrubFinalUri(Uri finalUri)
    {
        string raw = finalUri.AbsoluteUri;
        string redacted = this._secretRedactor.Redact(raw) ?? raw;
        return string.Equals(raw, redacted, StringComparison.Ordinal)
            ? finalUri
            : Uri.TryCreate(redacted, UriKind.Absolute, out Uri? parsed)
            ? parsed
            : new Uri(finalUri.GetLeftPart(UriPartial.Path), UriKind.Absolute);
    }

    /// <summary>
    /// <see cref="SocketsHttpHandler.ConnectCallback"/> enforcing DNS resolution and <see cref="SafeExternalIpAddressValidator"/> on every returned IP before socket connection.
    /// </summary>
    /// <param name="context">The connection context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A connected <see cref="NetworkStream"/>.</returns>
    private async ValueTask<Stream> ConnectSafelyAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        string host = context.DnsEndPoint.Host;
        int port = context.DnsEndPoint.Port;

        if (SafeExternalIpAddressValidator.IsLocalhostHostName(host))
        {
            LogBlockedHost(this._logger, host);
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.BlockedIpAddress,
                $"Target host '{host}' is blocked by SSRF policy.");
        }

        IPAddress[] candidateAddresses;
        if (IPAddress.TryParse(host, out IPAddress? literalAddress))
        {
            if (!SafeExternalIpAddressValidator.IsAllowed(literalAddress))
            {
                LogBlockedHost(this._logger, host);
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.BlockedIpAddress,
                    $"Target IP address '{host}' is blocked by SSRF policy.");
            }

            candidateAddresses = [literalAddress];
        }
        else
        {
            try
            {
                candidateAddresses = this._resolveHostAddressesAsync is not null
                    ? await this._resolveHostAddressesAsync(host, cancellationToken).ConfigureAwait(false)
                    : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.DnsResolutionFailed,
                    $"DNS resolution failed for host '{host}'.",
                    ex);
            }

            if (candidateAddresses.Length == 0)
            {
                throw new SafeExternalHttpException(
                    SafeExternalHttpFailureReason.DnsResolutionFailed,
                    $"DNS resolution returned no addresses for host '{host}'.");
            }

            foreach (IPAddress address in candidateAddresses)
            {
                if (!SafeExternalIpAddressValidator.IsAllowed(address))
                {
                    LogBlockedHost(this._logger, host);
                    throw new SafeExternalHttpException(
                        SafeExternalHttpFailureReason.BlockedIpAddress,
                        $"Resolved IP address for host '{host}' is blocked by SSRF policy.");
                }
            }
        }

        SocketException? lastSocketException = null;
        foreach (IPAddress address in candidateAddresses)
        {
            IPEndPoint endPoint = new(address, port);
            IPEndPoint connectEndPoint = this._rewriteTestEndPoint is not null
                ? this._rewriteTestEndPoint(endPoint)
                : endPoint;

            Socket? socket = new(connectEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            try
            {
                await socket.ConnectAsync(connectEndPoint, cancellationToken).ConfigureAwait(false);
                Socket connectedSocket = socket;
                socket = null;
                return new NetworkStream(connectedSocket, ownsSocket: true);
            }
            catch (SocketException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastSocketException = ex;
            }
            finally
            {
                socket?.Dispose();
            }
        }

        throw new HttpRequestException(
            $"Failed to connect to host '{host}'.",
            lastSocketException);
    }

    /// <summary>
    /// Matches <c>$secret.NAME</c> placeholders.
    /// </summary>
    /// <returns>The compiled secret placeholder regular expression.</returns>
    [GeneratedRegex(@"\$secret\.([A-Za-z0-9_.-]{1,128})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretTokenRegex();

    /// <summary>
    /// Logs a warning when an outbound request is blocked by SSRF validation.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="host">The blocked target host.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Blocked outbound HTTP request to disallowed host {Host}.")]
    private static partial void LogBlockedHost(ILogger logger, string host);

    /// <summary>
    /// Logs a warning when an outbound HTTP request exceeds its timeout ceiling.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="host">The target host.</param>
    /// <param name="timeout">The elapsed timeout duration.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbound HTTP request to {Host} timed out after {Timeout}.")]
    private static partial void LogRequestTimeout(ILogger logger, string host, TimeSpan timeout);

    /// <summary>
    /// Logs a warning when an outbound HTTP transport failure occurs.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="host">The target host.</param>
    /// <param name="message">The redacted error message.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbound HTTP request to {Host} failed: {Message}")]
    private static partial void LogTransportFailure(ILogger logger, string host, string message);

    /// <summary>
    /// Holds a resolved workspace secret alongside its escaped representation and host allowlist.
    /// </summary>
    /// <param name="SecretName">The normalized secret name.</param>
    /// <param name="PlaintextValue">The decrypted plaintext secret.</param>
    /// <param name="EscapedValue">The URL-escaped secret value.</param>
    /// <param name="AllowedHostPatterns">The allowed host patterns configured on the secret.</param>
    private sealed record ResolvedSecret(
        string SecretName,
        string PlaintextValue,
        string EscapedValue,
        IReadOnlyList<string> AllowedHostPatterns);

    /// <summary>
    /// Represents a resolved request header ready for transmission.
    /// </summary>
    /// <param name="Name">The header name.</param>
    /// <param name="ResolvedValue">The header value after secret substitution.</param>
    /// <param name="HadSecretToken">Whether this header originally contained a <c>$secret.*</c> token.</param>
    private sealed record ActiveHeader(
        string Name,
        string ResolvedValue,
        bool HadSecretToken);
}

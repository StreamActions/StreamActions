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

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StreamActions.Core.Models;

/// <summary>
/// Categorizes failure reasons thrown by <see cref="Abstractions.ISafeExternalHttpClient"/>.
/// </summary>
public enum SafeExternalHttpFailureReason
{
    /// <summary>
    /// The outbound request specification is malformed or missing required parameters.
    /// </summary>
    InvalidRequest = 0,

    /// <summary>
    /// The target URI or redirect <c>Location</c> uses a scheme other than <c>http</c> or <c>https</c>.
    /// </summary>
    DisallowedScheme = 1,

    /// <summary>
    /// A <c>$secret.*</c> token appeared inside the URI authority, host, port, or userinfo.
    /// </summary>
    SecretInAuthority = 2,

    /// <summary>
    /// One or more referenced <c>$secret.*</c> tokens could not be resolved or decrypted.
    /// </summary>
    SecretResolutionFailed = 3,

    /// <summary>
    /// A referenced secret's <c>AllowedHostPatterns</c> policy forbids the initial or redirected target host.
    /// </summary>
    SecretHostNotAllowed = 4,

    /// <summary>
    /// The destination host or resolved IP address is in a blocked SSRF range (loopback, private, link-local, metadata, or non-global).
    /// </summary>
    BlockedIpAddress = 5,

    /// <summary>
    /// DNS resolution returned no usable IP addresses or failed.
    /// </summary>
    DnsResolutionFailed = 6,

    /// <summary>
    /// The response exceeded the maximum allowed number of HTTP redirects.
    /// </summary>
    TooManyRedirects = 7,

    /// <summary>
    /// The decompressed response payload exceeded the maximum allowed byte limit.
    /// </summary>
    ResponseTooLarge = 8,

    /// <summary>
    /// The outbound HTTP request exceeded the configured timeout ceiling.
    /// </summary>
    Timeout = 9,

    /// <summary>
    /// The underlying HTTP transport failed while sending or reading the request.
    /// </summary>
    RequestFailed = 10,
}

/// <summary>
/// Represents an outbound HTTP request executed by <see cref="Abstractions.ISafeExternalHttpClient"/>.
/// </summary>
/// <param name="Url">The target URL string, optionally containing <c>$secret.NAME</c> tokens in path or query.</param>
/// <param name="Method">The HTTP method to send (defaults to <see cref="HttpMethod.Get"/> when <see langword="null"/>).</param>
/// <param name="Headers">Optional request headers, whose values may contain <c>$secret.NAME</c> tokens.</param>
/// <param name="Body">Optional raw request body (never scanned for <c>$secret.*</c> token substitution).</param>
/// <param name="ContentType">Optional media type for <paramref name="Body"/> (defaults to <c>text/plain</c> when omitted).</param>
/// <param name="WorkspaceId">The owning workspace identifier, required when <c>$secret.NAME</c> tokens are present.</param>
/// <param name="Timeout">Optional request timeout, capped at 5 seconds.</param>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Raw URL string is required so $secret.NAME tokens (including authority rejection checks) can be inspected prior to Uri construction.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Raw URL string is required so $secret.NAME tokens (including authority rejection checks) can be inspected prior to Uri construction.")]
public sealed record SafeExternalHttpRequest(
    string Url,
    HttpMethod? Method = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    string? Body = null,
    string? ContentType = null,
    Guid? WorkspaceId = null,
    TimeSpan? Timeout = null)
{
    /// <summary>
    /// Formats record members while redacting header values and request body contents.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.Method)).Append(" = ").Append(this.Method ?? HttpMethod.Get);
        _ = builder.Append(", ").Append(nameof(this.Url)).Append(" = ").Append(this.Url);
        _ = builder.Append(", HeaderCount = ").Append(this.Headers?.Count ?? 0);
        _ = builder.Append(", HasBody = ").Append(!string.IsNullOrEmpty(this.Body));
        _ = builder.Append(", ").Append(nameof(this.ContentType)).Append(" = ").Append(this.ContentType);
        _ = builder.Append(", ").Append(nameof(this.WorkspaceId)).Append(" = ").Append(this.WorkspaceId);
        _ = builder.Append(", ").Append(nameof(this.Timeout)).Append(" = ").Append(this.Timeout);
        return true;
    }
}

/// <summary>
/// Represents the scrubbed response returned by <see cref="Abstractions.ISafeExternalHttpClient"/>.
/// </summary>
/// <param name="StatusCode">The HTTP status code returned by the remote endpoint.</param>
/// <param name="Body">The response body decoded as text and scrubbed via <see cref="Abstractions.ISecretRedactor"/>.</param>
/// <param name="Headers">The response headers with all values scrubbed via <see cref="Abstractions.ISecretRedactor"/>.</param>
/// <param name="FinalUri">The final target URI after any redirects, with secret values scrubbed.</param>
/// <param name="RedirectCount">The number of HTTP redirects followed.</param>
public sealed record SafeExternalHttpResponse(
    int StatusCode,
    string Body,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    Uri FinalUri,
    int RedirectCount = 0)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="StatusCode"/> represents a 2xx success status.
    /// </summary>
    public bool IsSuccessStatusCode => this.StatusCode is >= 200 and <= 299;
}

/// <summary>
/// Exception thrown when an outbound request violates SSRF, secret policy, size, redirect, or timeout guards.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SafeExternalHttpException"/> class with failure reason metadata.
/// </remarks>
/// <param name="reason">The categorized failure reason.</param>
/// <param name="message">The redacted error message.</param>
/// <param name="innerException">The optional sanitized inner exception.</param>
/// <param name="secretResolutionStatus">The optional secret resolution status when secret lookup failed.</param>
public sealed class SafeExternalHttpException(
    SafeExternalHttpFailureReason reason,
    string message,
    Exception? innerException = null,
    SecretResolutionStatus? secretResolutionStatus = null) : Exception(message, innerException)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SafeExternalHttpException"/> class.
    /// </summary>
    public SafeExternalHttpException()
        : this(SafeExternalHttpFailureReason.RequestFailed, "The external HTTP request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeExternalHttpException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The redacted error message.</param>
    public SafeExternalHttpException(string message)
        : this(SafeExternalHttpFailureReason.RequestFailed, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeExternalHttpException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The redacted error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SafeExternalHttpException(string message, Exception innerException)
        : this(SafeExternalHttpFailureReason.RequestFailed, message, innerException)
    {
    }

    /// <summary>
    /// Gets the categorized failure reason.
    /// </summary>
    public SafeExternalHttpFailureReason Reason { get; } = reason;

    /// <summary>
    /// Gets the underlying secret resolution status when <see cref="Reason"/> relates to secret lookup.
    /// </summary>
    public SecretResolutionStatus? SecretResolutionStatus { get; } = secretResolutionStatus;
}

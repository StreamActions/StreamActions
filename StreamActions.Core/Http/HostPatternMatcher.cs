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

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Http;

/// <summary>
/// Provides canonical normalization, validation, wildcard matching, and policy-expansion checks for secret host patterns.
/// </summary>
public static partial class HostPatternMatcher
{
    /// <summary>
    /// Characters forbidden anywhere inside a host pattern.
    /// </summary>
    private static readonly SearchValues<char> _forbiddenPatternChars = SearchValues.Create(":/\\?#@");

    /// <summary>
    /// Maximum allowed length of a normalized DNS hostname in characters.
    /// </summary>
    private const int _maxHostLength = 253;

    /// <summary>
    /// Prefix used for subdomain wildcard host patterns.
    /// </summary>
    private const string _wildcardPrefix = "*.";

    /// <summary>
    /// Shared IDN mapping instance for converting Unicode domain names to lowercase Punycode ASCII.
    /// </summary>
    private static readonly IdnMapping _idnMapping = new();

    /// <summary>
    /// Validates and normalizes a single allowed host pattern to canonical lowercase ASCII/Punycode form.
    /// </summary>
    /// <param name="pattern">The host pattern to validate (for example, <c>api.example.com</c> or <c>*.example.com</c>).</param>
    /// <returns>The normalized host pattern.</returns>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is empty, contains scheme/port/path/userinfo characters, or violates host pattern syntax.</exception>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "DNS hostnames and allowed host patterns are canonicalized to lowercase ASCII.")]
    public static string NormalizeAndValidatePattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        string trimmed = pattern.Trim();
        if (trimmed.AsSpan().IndexOfAny(_forbiddenPatternChars) >= 0)
        {
            throw new ArgumentException($"Host pattern '{pattern}' must not contain schemes, ports, paths, userinfo, queries, or fragments.", nameof(pattern));
        }

        foreach (char ch in trimmed)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                throw new ArgumentException($"Host pattern '{pattern}' must not contain whitespace or control characters.", nameof(pattern));
            }
        }

        bool isWildcard = trimmed.StartsWith(_wildcardPrefix, StringComparison.Ordinal);
        string hostPart = isWildcard ? trimmed[_wildcardPrefix.Length..] : trimmed;

        if (hostPart.Contains('*', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Host pattern '{pattern}' may only use '*' as a leading '*.' subdomain wildcard.", nameof(pattern));
        }

        if (string.IsNullOrEmpty(hostPart) || hostPart.StartsWith('.') || hostPart.EndsWith('.'))
        {
            throw new ArgumentException($"Host pattern '{pattern}' contains an empty domain label.", nameof(pattern));
        }

        string asciiHost;
        try
        {
            asciiHost = _idnMapping.GetAscii(hostPart).ToLowerInvariant();
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Host pattern '{pattern}' is not a valid ASCII or IDN hostname.", nameof(pattern), ex);
        }

        if (asciiHost.Length is 0 or > _maxHostLength)
        {
            throw new ArgumentException($"Host pattern '{pattern}' exceeds the maximum hostname length of {_maxHostLength} characters.", nameof(pattern));
        }

        if (isWildcard && IPAddress.TryParse(asciiHost, out _))
        {
            throw new ArgumentException($"Wildcard host pattern '{pattern}' cannot target an IP address.", nameof(pattern));
        }

        string[] labels = asciiHost.Split('.');
        if (isWildcard && (labels.Length < 2))
        {
            throw new ArgumentException($"Wildcard host pattern '{pattern}' must include at least two dot-separated labels after '*.' (for example, '*.example.com').", nameof(pattern));
        }

        foreach (string label in labels)
        {
            if (!DnsLabelRegex().IsMatch(label))
            {
                throw new ArgumentException($"Host pattern '{pattern}' contains an invalid DNS label '{label}'.", nameof(pattern));
            }
        }

        return isWildcard ? string.Concat(_wildcardPrefix, asciiHost) : asciiHost;
    }

    /// <summary>
    /// Attempts to normalize a target hostname (ASCII or IDN) to canonical lowercase form for pattern matching.
    /// </summary>
    /// <param name="host">The raw hostname from a URI.</param>
    /// <param name="normalizedHost">When this method returns <see langword="true"/>, contains the canonicalized hostname.</param>
    /// <returns><see langword="true"/> if the hostname was normalized successfully; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "DNS hostnames are canonicalized to lowercase ASCII for ordinal matching.")]
    public static bool TryNormalizeHost(string? host, out string normalizedHost)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            normalizedHost = string.Empty;
            return false;
        }

        string trimmed = host.Trim();
        if ((trimmed.Length >= 2) && trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1];
        }

        if (IPAddress.TryParse(trimmed, out IPAddress? ipAddress))
        {
            normalizedHost = ipAddress.ToString();
            return true;
        }

        if ((trimmed.Length > 1) && trimmed.EndsWith('.'))
        {
            trimmed = trimmed[..^1];
        }

        try
        {
            normalizedHost = _idnMapping.GetAscii(trimmed).ToLowerInvariant();
            return normalizedHost.Length > 0;
        }
        catch (ArgumentException)
        {
            normalizedHost = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Determines whether a target host matches a single normalized host pattern.
    /// </summary>
    /// <param name="normalizedPattern">A host pattern previously normalized via <see cref="NormalizeAndValidatePattern"/>.</param>
    /// <param name="host">The target host to test.</param>
    /// <returns><see langword="true"/> if <paramref name="host"/> matches <paramref name="normalizedPattern"/>; otherwise, <see langword="false"/>.</returns>
    public static bool IsMatch(string normalizedPattern, string? host)
    {
        if (string.IsNullOrWhiteSpace(normalizedPattern) || !TryNormalizeHost(host, out string normalizedHost))
        {
            return false;
        }

        if (normalizedPattern.StartsWith(_wildcardPrefix, StringComparison.Ordinal))
        {
            string suffix = normalizedPattern[_wildcardPrefix.Length..];
            return (normalizedHost.Length > (suffix.Length + 1))
                && (normalizedHost[normalizedHost.Length - suffix.Length - 1] == '.')
                && normalizedHost.EndsWith(suffix, StringComparison.Ordinal);
        }

        return string.Equals(normalizedPattern, normalizedHost, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether a target host matches at least one pattern in an allowlist.
    /// </summary>
    /// <param name="normalizedPatterns">The collection of normalized allowed host patterns.</param>
    /// <param name="host">The target host to check.</param>
    /// <returns><see langword="true"/> if <paramref name="host"/> matches any pattern in <paramref name="normalizedPatterns"/>; otherwise, <see langword="false"/>.</returns>
    public static bool IsHostAllowed(IReadOnlyCollection<string> normalizedPatterns, string? host)
    {
        ArgumentNullException.ThrowIfNull(normalizedPatterns);

        if (!TryNormalizeHost(host, out string normalizedHost))
        {
            return false;
        }

        foreach (string pattern in normalizedPatterns)
        {
            if (IsMatch(pattern, normalizedHost))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether <paramref name="candidatePattern"/> is identical to or strictly nested within <paramref name="existingPattern"/>.
    /// </summary>
    /// <param name="candidatePattern">The normalized candidate pattern.</param>
    /// <param name="existingPattern">The normalized existing pattern.</param>
    /// <returns><see langword="true"/> if <paramref name="candidatePattern"/> does not expand beyond <paramref name="existingPattern"/>; otherwise, <see langword="false"/>.</returns>
    public static bool IsPatternCoveredBy(string candidatePattern, string existingPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(existingPattern);

        if (string.Equals(candidatePattern, existingPattern, StringComparison.Ordinal))
        {
            return true;
        }

        if (!existingPattern.StartsWith(_wildcardPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (candidatePattern.StartsWith(_wildcardPrefix, StringComparison.Ordinal))
        {
            string candidateSuffix = candidatePattern[_wildcardPrefix.Length..];
            return IsMatch(existingPattern, candidateSuffix);
        }

        return IsMatch(existingPattern, candidatePattern);
    }

    /// <summary>
    /// Determines whether every pattern in <paramref name="candidatePatterns"/> is covered by at least one pattern in <paramref name="existingPatterns"/>.
    /// </summary>
    /// <param name="candidatePatterns">The normalized updated host patterns.</param>
    /// <param name="existingPatterns">The normalized existing host patterns.</param>
    /// <returns><see langword="true"/> if no host expansion occurs; otherwise, <see langword="false"/>.</returns>
    public static bool ArePatternsCoveredBy(
        IReadOnlyCollection<string> candidatePatterns,
        IReadOnlyCollection<string> existingPatterns)
    {
        ArgumentNullException.ThrowIfNull(candidatePatterns);
        ArgumentNullException.ThrowIfNull(existingPatterns);

        foreach (string candidate in candidatePatterns)
        {
            bool covered = false;
            foreach (string existing in existingPatterns)
            {
                if (IsPatternCoveredBy(candidate, existing))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Matches a valid RFC 1123 lowercase ASCII DNS label.
    /// </summary>
    /// <returns>The compiled DNS label regular expression.</returns>
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex DnsLabelRegex();
}

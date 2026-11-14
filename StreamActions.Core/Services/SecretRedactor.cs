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

using StreamActions.Core.Abstractions;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Services;

/// <summary>
/// Thread-safe implementation of <see cref="ISecretRedactor"/> that scrubs sensitive tokens,
/// credentials, and registered runtime secrets from text.
/// </summary>
public sealed partial class SecretRedactor : ISecretRedactor
{
    /// <summary>
    /// Replacement text substituted for redacted secrets.
    /// </summary>
    private const string _redactedPlaceholder = "[REDACTED]";

    /// <summary>
    /// Minimum character length required for a registered secret to be redacted.
    /// </summary>
    private const int _minimumSecretLength = 4;

    /// <summary>
    /// Thread-safe set of registered runtime secret values.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _registeredSecrets = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string? Redact(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (input.Length == 0)
        {
            return input;
        }

        string result = AuthorizationHeaderRegex().Replace(input, $"$1{_redactedPlaceholder}");
        result = BearerTokenRegex().Replace(result, $"Bearer {_redactedPlaceholder}");
        result = ClientSecretQueryRegex().Replace(result, $"$1{_redactedPlaceholder}");

        if (!this._registeredSecrets.IsEmpty)
        {
            foreach (string secret in this._registeredSecrets.Keys.OrderByDescending(static s => s.Length))
            {
                result = result.Replace(secret, _redactedPlaceholder, StringComparison.Ordinal);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public void RegisterSecret(string secretValue)
    {
        if (string.IsNullOrWhiteSpace(secretValue) || secretValue.Length < _minimumSecretLength)
        {
            return;
        }

        _ = this._registeredSecrets.TryAdd(secretValue, 0);
    }

    /// <summary>
    /// Matches bearer token values in text.
    /// </summary>
    /// <returns>The compiled regular expression for bearer tokens.</returns>
    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    /// <summary>
    /// Matches HTTP <c>Authorization</c> header lines and their credential values.
    /// </summary>
    /// <returns>The compiled regular expression for <c>Authorization</c> headers.</returns>
    [GeneratedRegex(@"(Authorization\s*:\s*)(?:Bearer|OAuth|Basic|Bot)?\s*[^\r\n]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    /// <summary>
    /// Matches <c>client_secret</c> query or form parameters and their values.
    /// </summary>
    /// <returns>The compiled regular expression for <c>client_secret</c> parameters.</returns>
    [GeneratedRegex(@"([?&;]client_secret=)[^&\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClientSecretQueryRegex();
}

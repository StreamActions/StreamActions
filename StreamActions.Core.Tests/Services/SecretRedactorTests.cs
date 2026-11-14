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

using FluentAssertions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class SecretRedactorTests
{
    #region Redact

    [Fact]
    [Trait("Member", "Redact")]
    public void RedactWhenInputIsNullReturnsNull()
    {
        SecretRedactor redactor = new();

        string? result = redactor.Redact(null);

        _ = result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "Redact")]
    public void RedactWhenInputIsEmptyReturnsEmpty()
    {
        SecretRedactor redactor = new();

        string? result = redactor.Redact(string.Empty);

        _ = result.Should().BeEmpty();
    }

    [Theory]
    [Trait("Member", "Redact")]
    [InlineData("Token: Bearer abc123.def_456~+/==", "Token: Bearer [REDACTED]")]
    [InlineData("bearer my-jwt-token-value", "Bearer [REDACTED]")]
    public void RedactWhenInputContainsBearerTokenRedactsToken(string input, string expected)
    {
        SecretRedactor redactor = new();

        string? result = redactor.Redact(input);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [Trait("Member", "Redact")]
    [InlineData("Authorization: Bearer secret-token-123", "Authorization: [REDACTED]")]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "Authorization: [REDACTED]")]
    [InlineData("authorization: OAuth oauth-token-value\nNext: line", "authorization: [REDACTED]\nNext: line")]
    [InlineData("Authorization:Bot bot-token-value", "Authorization:[REDACTED]")]
    public void RedactWhenInputContainsAuthorizationHeaderRedactsHeaderValue(string input, string expected)
    {
        SecretRedactor redactor = new();

        string? result = redactor.Redact(input);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [Trait("Member", "Redact")]
    [InlineData("https://id.twitch.tv/oauth2/token?client_id=abc&client_secret=supersecret123&grant_type=client_credentials",
        "https://id.twitch.tv/oauth2/token?client_id=abc&client_secret=[REDACTED]&grant_type=client_credentials")]
    [InlineData("https://example.com/auth?client_secret=my_secret",
        "https://example.com/auth?client_secret=[REDACTED]")]
    [InlineData("https://example.com/auth;CLIENT_SECRET=case_secret",
        "https://example.com/auth;CLIENT_SECRET=[REDACTED]")]
    public void RedactWhenInputContainsClientSecretQueryParameterRedactsSecret(string input, string expected)
    {
        SecretRedactor redactor = new();

        string? result = redactor.Redact(input);

        _ = result.Should().Be(expected);
    }

    [Fact]
    [Trait("Member", "Redact")]
    public void RedactWhenInputContainsRegisteredSecretRedactsSecret()
    {
        SecretRedactor redactor = new();
        redactor.RegisterSecret("my-super-secret-key");

        string? result = redactor.Redact("Connecting with key my-super-secret-key to server");

        _ = result.Should().Be("Connecting with key [REDACTED] to server");
    }

    [Fact]
    [Trait("Member", "Redact")]
    public void RedactWhenInputContainsNoSecretsReturnsOriginalString()
    {
        SecretRedactor redactor = new();
        const string input = "Normal log message without any sensitive data.";

        string? result = redactor.Redact(input);

        _ = result.Should().Be(input);
    }

    #endregion

    #region RegisterSecret

    [Theory]
    [Trait("Member", "RegisterSecret")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterSecretWhenSecretIsNullOrWhitespaceIgnoresSecret(string? secretValue)
    {
        SecretRedactor redactor = new();
        const string input = "Normal log message with spaces.";

        redactor.RegisterSecret(secretValue!);
        string? result = redactor.Redact(input);

        _ = result.Should().Be(input);
    }

    [Theory]
    [Trait("Member", "RegisterSecret")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    public void RegisterSecretWhenSecretIsTooShortIgnoresSecret(string shortSecret)
    {
        SecretRedactor redactor = new();
        string input = $"The word {shortSecret} should remain intact.";

        redactor.RegisterSecret(shortSecret);
        string? result = redactor.Redact(input);

        _ = result.Should().Be(input);
    }

    [Fact]
    [Trait("Member", "RegisterSecret")]
    public async Task RegisterSecretWhenCalledConcurrentlyRemainsThreadSafe()
    {
        SecretRedactor redactor = new();
        const int workerCount = 32;

        Task[] tasks =
        [
            .. Enumerable.Range(0, workerCount).Select(i => Task.Run(() =>
            {
                string secret = $"secret-value-{i:D3}";
                redactor.RegisterSecret(secret);
                string? redacted = redactor.Redact($"Payload contains {secret}");
                _ = redacted.Should().Be("Payload contains [REDACTED]");
            })),
        ];

        await Task.WhenAll(tasks);
    }

    #endregion
}

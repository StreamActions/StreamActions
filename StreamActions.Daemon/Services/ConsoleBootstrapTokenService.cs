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
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Manages single-instance 128-bit console bootstrap tokens with a 5-minute TTL and 3-strike brute-force burn protection.
/// </summary>
internal sealed partial class ConsoleBootstrapTokenService : IBootstrapTokenService
{
    /// <summary>
    /// Maximum number of consecutive failed validation attempts before the active token is burned.
    /// </summary>
    public const int MaxFailedAttempts = 3;

    /// <summary>
    /// Lifetime of a generated bootstrap token and the post-burn cooldown duration.
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Time provider used to evaluate token expiration and cooldowns.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger instance for token lifecycle events.
    /// </summary>
    private readonly ILogger<ConsoleBootstrapTokenService> _logger;

    /// <summary>
    /// Optional console output writer override used when printing newly generated tokens.
    /// </summary>
    private readonly TextWriter? _consoleOutput;

    /// <summary>
    /// Synchronization lock protecting active token state.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// Currently active hexadecimal bootstrap token, or <see langword="null"/> if none is active.
    /// </summary>
    private string? _activeToken;

    /// <summary>
    /// UTC expiration timestamp of the currently active token.
    /// </summary>
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// UTC timestamp until which token issuance is blocked after a token is burned.
    /// </summary>
    private DateTimeOffset _cooldownUntilUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// Number of consecutive failed validation attempts against the current token.
    /// </summary>
    private int _failedAttempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleBootstrapTokenService"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider controlling token expiration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="consoleOutput">An optional <see cref="TextWriter"/> receiving newly generated tokens instead of <see cref="Console.Out"/>.</param>
    public ConsoleBootstrapTokenService(
        TimeProvider timeProvider,
        ILogger<ConsoleBootstrapTokenService> logger,
        TextWriter? consoleOutput = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._timeProvider = timeProvider;
        this._logger = logger;
        this._consoleOutput = consoleOutput;
    }

    /// <inheritdoc/>
    public BootstrapTokenRequestResult RequestOrReuseActiveToken()
    {
        lock (this._gate)
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            if (now < this._cooldownUntilUtc)
            {
                return new(false, null, this._cooldownUntilUtc - now);
            }

            if ((this._activeToken is not null) && (now < this._expiresAtUtc))
            {
                return new(true, this._activeToken, null);
            }

            this._activeToken = RandomNumberGenerator.GetHexString(32, lowercase: true);
            this._expiresAtUtc = now + TokenLifetime;
            this._cooldownUntilUtc = DateTimeOffset.MinValue;
            this._failedAttempts = 0;

            LogTokenGenerated(this._logger, this._expiresAtUtc);
            TextWriter output = this._consoleOutput ?? Console.Out;
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Console bootstrap token (valid until {this._expiresAtUtc:O}): {this._activeToken}"));

            return new(true, this._activeToken, null);
        }
    }

    /// <inheritdoc/>
    public string GetOrCreateToken()
    {
        BootstrapTokenRequestResult result = this.RequestOrReuseActiveToken();
        if (!result.Succeeded || (result.Token is null))
        {
            int remainingSeconds = Math.Max(1, (int)Math.Ceiling((result.CooldownRemaining ?? TimeSpan.Zero).TotalSeconds));
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Bootstrap token generation is in cooldown for {remainingSeconds} second(s)."));
        }

        return result.Token;
    }

    /// <inheritdoc/>
    public bool ValidateToken(string candidate)
    {
        lock (this._gate)
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            if ((now < this._cooldownUntilUtc) || (this._activeToken is null))
            {
                return false;
            }

            if (now >= this._expiresAtUtc)
            {
                this.InvalidateLocked();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                byte[] candidateBytes = Encoding.UTF8.GetBytes(candidate.Trim());
                byte[] activeBytes = Encoding.UTF8.GetBytes(this._activeToken);
                if (CryptographicOperations.FixedTimeEquals(candidateBytes, activeBytes))
                {
                    this._failedAttempts = 0;
                    return true;
                }
            }

            this._failedAttempts++;
            if (this._failedAttempts >= MaxFailedAttempts)
            {
                this.InvalidateLocked();
                this._cooldownUntilUtc = now + TokenLifetime;
                LogTokenBurned(this._logger);
            }

            return false;
        }
    }

    /// <summary>
    /// Immediately invalidates any active bootstrap token.
    /// </summary>
    public void Invalidate()
    {
        lock (this._gate)
        {
            this.InvalidateLocked();
        }
    }

    /// <summary>
    /// Clears the active token state while holding <see cref="_gate"/>.
    /// </summary>
    private void InvalidateLocked()
    {
        this._activeToken = null;
        this._expiresAtUtc = DateTimeOffset.MinValue;
        this._failedAttempts = 0;
    }

    /// <summary>
    /// Logs that a new console bootstrap token was generated.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="expiresAtUtc">The UTC expiration timestamp of the generated token.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Generated console bootstrap token valid until {ExpiresAtUtc:O}.")]
    private static partial void LogTokenGenerated(ILogger logger, DateTimeOffset expiresAtUtc);

    /// <summary>
    /// Logs that the active console bootstrap token was burned after repeated failed validation attempts.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Console bootstrap token was invalidated after 3 failed validation attempts.")]
    private static partial void LogTokenBurned(ILogger logger);
}

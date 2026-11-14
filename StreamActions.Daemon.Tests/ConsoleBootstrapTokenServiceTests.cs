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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Daemon.Services;
using Xunit;

namespace StreamActions.Daemon.Tests;

public sealed class ConsoleBootstrapTokenServiceTests
{
    #region GetOrCreateToken
    [Fact]
    [Trait("Member", "GetOrCreateToken")]
    public void GetOrCreateToken_WhenCalledTwiceWithinTtl_ReturnsSameTokenAndLogsOnce()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger);

        string firstToken = service.GetOrCreateToken();
        timeProvider.Advance(TimeSpan.FromMinutes(4));
        string secondToken = service.GetOrCreateToken();

        _ = firstToken.Should().HaveLength(32);
        _ = secondToken.Should().Be(firstToken);
        _ = logger.Messages.Should().ContainSingle();
    }

    [Fact]
    [Trait("Member", "GetOrCreateToken")]
    public void GetOrCreateToken_WhenTtlExpires_GeneratesFreshToken()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger);

        string firstToken = service.GetOrCreateToken();
        timeProvider.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        _ = service.ValidateToken(firstToken).Should().BeFalse();

        string refreshedToken = service.GetOrCreateToken();
        _ = refreshedToken.Should().NotBe(firstToken);
        _ = service.ValidateToken(refreshedToken).Should().BeTrue();
    }
    #endregion

    #region ValidateToken
    [Fact]
    [Trait("Member", "ValidateToken")]
    public void ValidateToken_WhenThreeWrongGuessesSubmitted_BurnsTokenImmediately()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger);

        string activeToken = service.GetOrCreateToken();

        _ = service.ValidateToken("wrong-1").Should().BeFalse();
        _ = service.ValidateToken("wrong-2").Should().BeFalse();
        _ = service.ValidateToken("wrong-3").Should().BeFalse();
        _ = service.ValidateToken(activeToken).Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ValidateToken")]
    public void ValidateToken_WhenTwoWrongGuessesThenCorrectGuess_AcceptsCorrectToken()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger);

        string activeToken = service.GetOrCreateToken();

        _ = service.ValidateToken("wrong-1").Should().BeFalse();
        _ = service.ValidateToken("wrong-2").Should().BeFalse();
        _ = service.ValidateToken(activeToken).Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Invalidate")]
    public void Invalidate_WhenActiveTokenExists_RejectsSubsequentValidation()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger);

        string activeToken = service.GetOrCreateToken();
        service.Invalidate();

        _ = service.ValidateToken(activeToken).Should().BeFalse();
    }
    #endregion

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            this.Messages.Add(formatter(state, exception));
    }
}

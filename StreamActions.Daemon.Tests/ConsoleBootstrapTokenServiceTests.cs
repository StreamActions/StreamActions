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
using StreamActions.Core.Abstractions;
using StreamActions.Daemon.Services;
using Xunit;

namespace StreamActions.Daemon.Tests;

public sealed class ConsoleBootstrapTokenServiceTests
{
    #region RequestOrReuseActiveToken
    [Fact]
    [Trait("Member", "RequestOrReuseActiveToken")]
    public void RequestOrReuseActiveToken_WhenGeneratingNewToken_WritesToConsoleOutputAndOmitsTokenFromLogger()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

        BootstrapTokenRequestResult first = service.RequestOrReuseActiveToken();
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        BootstrapTokenRequestResult second = service.RequestOrReuseActiveToken();

        _ = first.Succeeded.Should().BeTrue();
        _ = first.Token.Should().NotBeNull().And.HaveLength(32);
        _ = first.Token.Should().MatchRegex("^[0-9a-f]{32}$");
        _ = first.CooldownRemaining.Should().BeNull();
        _ = second.Should().Be(first);

        _ = consoleOutput.ToString().Should().Contain(first.Token!);
        _ = logger.Messages.Should().ContainSingle();
        _ = logger.Messages[0].Should().NotContain(first.Token!);
    }

    [Fact]
    [Trait("Member", "RequestOrReuseActiveToken")]
    public void RequestOrReuseActiveToken_WhenTokenBurned_EnforcesFiveMinuteCooldownThenAllowsFreshToken()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

        BootstrapTokenRequestResult initial = service.RequestOrReuseActiveToken();
        _ = service.ValidateToken("wrong-1").Should().BeFalse();
        _ = service.ValidateToken("wrong-2").Should().BeFalse();
        _ = service.ValidateToken("wrong-3").Should().BeFalse();

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        BootstrapTokenRequestResult duringCooldown = service.RequestOrReuseActiveToken();
        _ = duringCooldown.Succeeded.Should().BeFalse();
        _ = duringCooldown.Token.Should().BeNull();
        _ = duringCooldown.CooldownRemaining.Should().Be(TimeSpan.FromMinutes(3));
        _ = service.ValidateToken(initial.Token!).Should().BeFalse();

        timeProvider.Advance(TimeSpan.FromMinutes(3));
        BootstrapTokenRequestResult afterCooldown = service.RequestOrReuseActiveToken();
        _ = afterCooldown.Succeeded.Should().BeTrue();
        _ = afterCooldown.Token.Should().NotBeNull().And.NotBe(initial.Token);
        _ = afterCooldown.CooldownRemaining.Should().BeNull();
        _ = service.ValidateToken(afterCooldown.Token!).Should().BeTrue();
        _ = consoleOutput.ToString().Should().Contain(afterCooldown.Token!);
    }
    #endregion

    #region GetOrCreateToken
    [Fact]
    [Trait("Member", "GetOrCreateToken")]
    public void GetOrCreateToken_WhenCalledTwiceWithinTtl_ReturnsSameTokenAndLogsOnce()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

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
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

        string firstToken = service.GetOrCreateToken();
        timeProvider.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        _ = service.ValidateToken(firstToken).Should().BeFalse();

        string refreshedToken = service.GetOrCreateToken();
        _ = refreshedToken.Should().NotBe(firstToken);
        _ = service.ValidateToken(refreshedToken).Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "GetOrCreateToken")]
    public void GetOrCreateToken_WhenInCooldown_ThrowsInvalidOperationException()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

        _ = service.GetOrCreateToken();
        _ = service.ValidateToken("wrong-1").Should().BeFalse();
        _ = service.ValidateToken("wrong-2").Should().BeFalse();
        _ = service.ValidateToken("wrong-3").Should().BeFalse();

        Action act = () => _ = service.GetOrCreateToken();
        _ = act.Should().Throw<InvalidOperationException>();
    }
    #endregion

    #region ValidateToken
    [Fact]
    [Trait("Member", "ValidateToken")]
    public void ValidateToken_WhenNoActiveTokenExists_ReturnsFalse()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

        _ = service.ValidateToken("anything").Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ValidateToken")]
    public void ValidateToken_WhenThreeWrongGuessesSubmitted_BurnsTokenImmediately()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

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
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

        string activeToken = service.GetOrCreateToken();

        _ = service.ValidateToken("wrong-1").Should().BeFalse();
        _ = service.ValidateToken("wrong-2").Should().BeFalse();
        _ = service.ValidateToken(activeToken).Should().BeTrue();
    }
    #endregion

    #region Invalidate
    [Fact]
    [Trait("Member", "Invalidate")]
    public void Invalidate_WhenActiveTokenExists_RejectsSubsequentValidation()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        RecordingLogger<ConsoleBootstrapTokenService> logger = new();
        using StringWriter consoleOutput = new();
        ConsoleBootstrapTokenService service = new(timeProvider, logger, consoleOutput);

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

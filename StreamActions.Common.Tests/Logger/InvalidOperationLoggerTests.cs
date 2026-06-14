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
using StreamActions.Common.Exceptions;
using StreamActions.Common.Logger;
using System;
using System.Linq;
using Xunit;

namespace StreamActions.Common.Tests.Logger;

public class InvalidOperationLoggerTests
{
    private readonly TestLogger _logger = new();

    #region ArgumentNull

    [Fact]
    [Trait("Member", "Log")]
    public void Log_ArgumentNullException_IsNull_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = null!;
        Action action = () => InvalidOperationLogger.Log(exception, this._logger);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "Log")]
    public void Log_ArgumentNullException_LogsAndReturnsException()
    {
        var exception = new ArgumentNullException("param1", "Exception message");
        var result = InvalidOperationLogger.Log(exception, this._logger);

        result.Should().Be(exception);
        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1000);
        log.Exception.Should().Be(exception);
        log.Message.Should().Contain("Argument Null: Exception message (Parameter 'param1')");
    }

    [Fact]
    [Trait("Member", "ArgumentNull")]
    public void ArgumentNull_MemberMessageException_LogsCorrectly()
    {
        var exception = new ArgumentNullException("param1");
        InvalidOperationLogger.ArgumentNull(this._logger, "TestMember", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1000);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestMember] Argument Null: TestMessage");
    }

    [Fact]
    [Trait("Member", "ArgumentNull")]
    public void ArgumentNull_TypeMemberParamMessageException_LogsCorrectly()
    {
        var exception = new ArgumentNullException("param1");
        InvalidOperationLogger.ArgumentNull(this._logger, "TestType", "TestMember", "param1", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1000);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestType.TestMember] Argument Null: TestMessage (paramName was `param1`)");
    }

    #endregion

    #region ArgumentOutOfRange

    [Fact]
    [Trait("Member", "Log")]
    public void Log_ArgumentOutOfRangeException_IsNull_ThrowsArgumentNullException()
    {
        ArgumentOutOfRangeException exception = null!;
        Action action = () => InvalidOperationLogger.Log(exception, this._logger);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "Log")]
    public void Log_ArgumentOutOfRangeException_LogsAndReturnsException()
    {
        var exception = new ArgumentOutOfRangeException("param1", 42, "Exception message");
        var result = InvalidOperationLogger.Log(exception, this._logger);

        result.Should().Be(exception);
        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.Exception.Should().Be(exception);
        log.Message.Should().Contain("Exception message (Parameter 'param1')\nActual value was 42.");
    }

    [Fact]
    [Trait("Member", "ArgumentOutOfRange")]
    public void ArgumentOutOfRange_MemberMessageException_LogsCorrectly()
    {
        var exception = new ArgumentOutOfRangeException("param1");
        InvalidOperationLogger.ArgumentOutOfRange(this._logger, "TestMember", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1001);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestMember] Argument Out of Range: TestMessage");
    }

    [Fact]
    [Trait("Member", "ArgumentOutOfRange")]
    public void ArgumentOutOfRange_TypeMemberParamValueMessageException_LogsCorrectly()
    {
        var exception = new ArgumentOutOfRangeException("param1");
        InvalidOperationLogger.ArgumentOutOfRange(this._logger, "TestType", "TestMember", "param1", 42, "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1000);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestType.TestMember] Argument Null: TestMessage (paramName was `param1`) (actualValue was `42`)");
    }

    #endregion

    #region InvalidOperation

    [Fact]
    [Trait("Member", "Log")]
    public void Log_InvalidOperationException_IsNull_ThrowsArgumentNullException()
    {
        InvalidOperationException exception = null!;
        Action action = () => InvalidOperationLogger.Log(exception, this._logger);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "Log")]
    public void Log_InvalidOperationException_LogsAndReturnsException()
    {
        var exception = new InvalidOperationException("Exception message");
        var result = InvalidOperationLogger.Log(exception, this._logger);

        result.Should().Be(exception);
        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1002);
        log.Exception.Should().Be(exception);
        log.Message.Should().Contain("Invalid Operation: Exception message");
    }

    [Fact]
    [Trait("Member", "InvalidOperation")]
    public void InvalidOperation_MemberMessageException_LogsCorrectly()
    {
        var exception = new InvalidOperationException("Exception message");
        InvalidOperationLogger.InvalidOperation(this._logger, "TestMember", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1002);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestMember] Invalid Operation: TestMessage");
    }

    [Fact]
    [Trait("Member", "InvalidOperation")]
    public void InvalidOperation_TypeMemberMessageException_LogsCorrectly()
    {
        var exception = new InvalidOperationException("Exception message");
        InvalidOperationLogger.InvalidOperation(this._logger, "TestType", "TestMember", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Debug);
        log.EventId.Id.Should().Be(1002);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestType.TestMember] Invalid Operation: TestMessage");
    }

    #endregion

    #region ScopeMissing

    [Fact]
    [Trait("Member", "Log")]
    public void Log_ScopeMissingException_IsNull_ThrowsArgumentNullException()
    {
        ScopeMissingException exception = null!;
        Action action = () => InvalidOperationLogger.Log(exception, this._logger);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "Log")]
    public void Log_ScopeMissingException_LogsAndReturnsException()
    {
        var exception = new ScopeMissingException("scope:test", "Exception message");
        var result = InvalidOperationLogger.Log(exception, this._logger);

        result.Should().Be(exception);
        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Error);
        log.EventId.Id.Should().Be(1010);
        log.Exception.Should().Be(exception);
        log.Message.Should().Contain("Scope Missing: Missing scopes scope:test, Exception message.");
    }

    [Fact]
    [Trait("Member", "ScopeMissing")]
    public void ScopeMissing_MemberMessageException_LogsCorrectly()
    {
        var exception = new ScopeMissingException("scope:test", "Exception message");
        InvalidOperationLogger.ScopeMissing(this._logger, "TestMember", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Error);
        log.EventId.Id.Should().Be(1010);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestMember] Scope Missing: TestMessage");
    }

    [Fact]
    [Trait("Member", "ScopeMissing")]
    public void ScopeMissing_TypeMemberScopeMessageException_LogsCorrectly()
    {
        var exception = new ScopeMissingException("scope:test", "Exception message");
        InvalidOperationLogger.ScopeMissing(this._logger, "TestType", "TestMember", "scope:test", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Error);
        log.EventId.Id.Should().Be(1010);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestType.TestMember] Scope Missing: TestMessage (scope was `scope:test`)");
    }

    #endregion

    #region TokenType

    [Fact]
    [Trait("Member", "Log")]
    public void Log_TokenTypeException_IsNull_ThrowsArgumentNullException()
    {
        TokenTypeException exception = null!;
        Action action = () => InvalidOperationLogger.Log(exception, this._logger);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "Log")]
    public void Log_TokenTypeException_LogsAndReturnsException()
    {
        var exception = new TokenTypeException("expectedType", "actualType", new InvalidOperationException("inner exception"));
        var result = InvalidOperationLogger.Log(exception, this._logger);

        result.Should().Be(exception);
        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Error);
        log.EventId.Id.Should().Be(1020);
        log.Exception.Should().Be(exception);
        log.Message.Should().Contain("Token Type: Incorrect token type. Expected: expectedType. Actual: actualType.");
    }

    [Fact]
    [Trait("Member", "TokenType")]
    public void TokenType_MemberMessageException_LogsCorrectly()
    {
        var exception = new TokenTypeException("expectedType", "actualType");
        InvalidOperationLogger.TokenType(this._logger, "TestMember", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Error);
        log.EventId.Id.Should().Be(1020);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestMember] Token Type: TestMessage");
    }

    [Fact]
    [Trait("Member", "TokenType")]
    public void TokenType_TypeMemberExpectedActualMessageException_LogsCorrectly()
    {
        var exception = new TokenTypeException("expectedType", "actualType");
        InvalidOperationLogger.TokenType(this._logger, "TestType", "TestMember", "expectedType", "actualType", "TestMessage", exception);

        this._logger.Logs.Should().ContainSingle();
        var log = this._logger.Logs.Single();
        log.LogLevel.Should().Be(LogLevel.Error);
        log.EventId.Id.Should().Be(1020);
        log.Exception.Should().Be(exception);
        log.Message.Should().Be("[TestType.TestMember] Token Type: TestMessage (expected was `expectedType`) (actual was `actualType`)");
    }

    #endregion
}

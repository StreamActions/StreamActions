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
using StreamActions.Common.Exceptions;
using System;
using Xunit;

namespace StreamActions.Common.Tests.Exceptions;

public class ScopeMissingExceptionTests
{
    #region ScopeMissingException

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithoutParameters_SetsCorrectMessageAndNullScope()
    {
        // Act
        var exception = new ScopeMissingException();

        // Assert
        exception.Message.Should().Be("Missing scope Unknown.");
        exception.Scope.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithSingleScope_SetsCorrectMessageAndScope()
    {
        // Arrange
        var scope = "user:read:email";

        // Act
        var exception = new ScopeMissingException(scope);

        // Assert
        exception.Message.Should().Be("Missing scope user:read:email.");
        exception.Scope.Should().Be("user:read:email");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithSingleNullScope_SetsCorrectMessageAndScope()
    {
        // Arrange
        string? scope = null;

        // Act
        var exception = new ScopeMissingException(scope);

        // Assert
        exception.Message.Should().Be("Missing scope Unknown.");
        exception.Scope.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithSingleScopeAndInnerException_SetsCorrectProperties()
    {
        // Arrange
        var scope = "user:read:email";
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new ScopeMissingException(scope, innerException);

        // Assert
        exception.Message.Should().Be("Missing scope user:read:email.");
        exception.Scope.Should().Be("user:read:email");
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithSingleNullScopeAndInnerException_SetsCorrectProperties()
    {
        // Arrange
        string? scope = null;
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new ScopeMissingException(scope, innerException);

        // Assert
        exception.Message.Should().Be("Missing scope Unknown.");
        exception.Scope.Should().BeNull();
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithMultipleScopes_SetsCorrectMessageAndScope()
    {
        // Arrange
        var scopes = new[] { "user:read:email", "channel:read:subscriptions" };

        // Act
        var exception = new ScopeMissingException(scopes);

        // Assert
        exception.Message.Should().Be("Missing scopes user:read:email, channel:read:subscriptions.");
        exception.Scope.Should().Be("user:read:email, channel:read:subscriptions");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithMultipleScopesContainingNulls_SetsCorrectMessageAndScope()
    {
        // Arrange
        var scopes = new string?[] { "user:read:email", null, "channel:read:subscriptions" };

        // Act
        var exception = new ScopeMissingException(scopes);

        // Assert
        exception.Message.Should().Be("Missing scopes user:read:email, , channel:read:subscriptions.");
        exception.Scope.Should().Be("user:read:email, , channel:read:subscriptions");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithNullScopesArray_SetsCorrectMessageAndScope()
    {
        // Arrange
        string?[]? scopes = null;

        // Act
        var exception = new ScopeMissingException(scopes!);

        // Assert
        exception.Message.Should().Be("Missing scopes Unknown.");
        exception.Scope.Should().Be("Unknown");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithMultipleScopesAndInnerException_SetsCorrectProperties()
    {
        // Arrange
        var scopes = new[] { "user:read:email", "channel:read:subscriptions" };
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new ScopeMissingException(scopes, innerException);

        // Assert
        exception.Message.Should().Be("Missing scopes user:read:email, channel:read:subscriptions.");
        exception.Scope.Should().Be("user:read:email, channel:read:subscriptions");
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    [Trait("Member", "ScopeMissingException")]
    public void Constructor_WhenCalledWithNullScopesArrayAndInnerException_SetsCorrectProperties()
    {
        // Arrange
        string?[]? scopes = null;
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new ScopeMissingException(scopes!, innerException);

        // Assert
        exception.Message.Should().Be("Missing scopes Unknown.");
        exception.Scope.Should().Be("Unknown");
        exception.InnerException.Should().Be(innerException);
    }

    #endregion ScopeMissingException
}

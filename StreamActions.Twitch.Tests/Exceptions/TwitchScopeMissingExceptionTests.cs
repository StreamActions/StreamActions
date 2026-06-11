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

using System;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;

namespace StreamActions.Twitch.Tests.Exceptions;

public class TwitchScopeMissingExceptionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_Default_ShouldInitializeSuccessfully()
    {
        // Act
        var exception = new TwitchScopeMissingException();

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().BeNull();
        exception.Message.Should().Be("Missing scope Unknown.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_StringScope_ShouldInitializeSuccessfully()
    {
        // Arrange
        var scope = "chat:read";

        // Act
        var exception = new TwitchScopeMissingException(scope);

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().Be(scope);
        // Base ScopeMissingException evaluates: "Missing scope " + scope ?? "Unknown" + "."
        exception.Message.Should().Be($"Missing scope {scope}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullStringScope_ShouldInitializeSuccessfully()
    {
        // Arrange
        string? scope = null;

        // Act
        var exception = new TwitchScopeMissingException(scope);

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().BeNull();
        exception.Message.Should().Be("Missing scope ");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_ScopeObject_ShouldInitializeSuccessfully()
    {
        // Arrange
        Scope? nullScope = null;

        // Act
        var nullScopeException = new TwitchScopeMissingException(nullScope);

        // Assert
        nullScopeException.Should().NotBeNull();
        nullScopeException.Scope.Should().BeNull();
        nullScopeException.Message.Should().Be("Missing scope ");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_StringScopes_ShouldInitializeSuccessfully()
    {
        // Arrange
        var scopes = new[] { "chat:read", "chat:edit" };

        // Act
        var exception = new TwitchScopeMissingException(scopes);

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().Be("chat:read, chat:edit");
        exception.Message.Should().Be("Missing scopes chat:read, chat:edit.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullStringScopesArray_ShouldInitializeSuccessfully()
    {
        // Arrange
        string?[]? scopes = null;

        // Act
#pragma warning disable CS8604 // Possible null reference argument.
        var exception = new TwitchScopeMissingException(scopes);
#pragma warning restore CS8604 // Possible null reference argument.

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().Be("Unknown");
        exception.Message.Should().Be("Missing scopes Unknown.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_ScopeObjects_ShouldInitializeSuccessfully()
    {
        // Let's test with a null Scope array for the scopes overload
        Scope?[]? nullScopes = null;

        // Act
#pragma warning disable CS8604 // Possible null reference argument.
        var nullScopesException = new TwitchScopeMissingException(nullScopes);
#pragma warning restore CS8604 // Possible null reference argument.

        // Assert
        nullScopesException.Should().NotBeNull();
        nullScopesException.Scope.Should().Be("");
        nullScopesException.Message.Should().Be("Missing scopes .");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_StringScopeAndInnerException_ShouldInitializeSuccessfully()
    {
        // Arrange
        var scope = "chat:read";
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new TwitchScopeMissingException(scope, innerException);

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().Be(scope);
        exception.Message.Should().Be($"Missing scope {scope}");
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_ScopeObjectAndInnerException_ShouldInitializeSuccessfully()
    {
        // Let's test with a null Scope object for the scope+exception overload
        Scope? nullScope = null;
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var nullScopeException = new TwitchScopeMissingException(nullScope, innerException);

        // Assert
        nullScopeException.Should().NotBeNull();
        nullScopeException.Scope.Should().BeNull();
        nullScopeException.Message.Should().Be("Missing scope ");
        nullScopeException.InnerException.Should().Be(innerException);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_StringScopesAndInnerException_ShouldInitializeSuccessfully()
    {
        // Arrange
        var scopes = new[] { "chat:read", "chat:edit" };
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new TwitchScopeMissingException(scopes, innerException);

        // Assert
        exception.Should().NotBeNull();
        exception.Scope.Should().Be("chat:read, chat:edit");
        exception.Message.Should().Be("Missing scopes chat:read, chat:edit.");
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_ScopeObjectsAndInnerException_ShouldInitializeSuccessfully()
    {
        // Let's test with a null Scope array for the scopes+exception overload
        Scope?[]? nullScopes = null;
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
#pragma warning disable CS8604 // Possible null reference argument.
        var nullScopesException = new TwitchScopeMissingException(nullScopes, innerException);
#pragma warning restore CS8604 // Possible null reference argument.

        // Assert
        nullScopesException.Should().NotBeNull();
        nullScopesException.Scope.Should().Be("");
        nullScopesException.Message.Should().Be("Missing scopes .");
        nullScopesException.InnerException.Should().Be(innerException);
    }
}

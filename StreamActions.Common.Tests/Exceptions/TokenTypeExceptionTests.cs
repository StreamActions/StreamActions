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

/// <summary>
/// Tests for the <see cref="TokenTypeException"/> class.
/// </summary>
public class TokenTypeExceptionTests
{
    #region TokenTypeException()

    [Fact]
    [Trait("Member", "TokenTypeException")]
    public void Constructor_Default_SetsMessageAndProperties()
    {
        // Act
        var exception = new TokenTypeException();

        // Assert
        exception.Message.Should().Be("Incorrect token type.");
        exception.ExpectedType.Should().BeNull();
        exception.ActualType.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    #endregion TokenTypeException()

    #region TokenTypeException(string?, string?)

    [Fact]
    [Trait("Member", "TokenTypeException")]
    public void Constructor_WithExpectedAndActual_SetsMessageAndProperties()
    {
        // Arrange
        string expected = "expectedType";
        string actual = "actualType";

        // Act
        var exception = new TokenTypeException(expected, actual);

        // Assert
        exception.Message.Should().Be("Incorrect token type. Expected: expectedType. Actual: actualType.");
        exception.ExpectedType.Should().Be(expected);
        exception.ActualType.Should().Be(actual);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "TokenTypeException")]
    public void Constructor_WithNullExpectedAndActual_SetsMessageAndProperties()
    {
        // Act
        var exception = new TokenTypeException(null, null);

        // Assert
        exception.Message.Should().Be("Incorrect token type. Expected: . Actual: .");
        exception.ExpectedType.Should().BeNull();
        exception.ActualType.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    #endregion TokenTypeException(string?, string?)

    #region TokenTypeException(string?, string?, Exception?)

    [Fact]
    [Trait("Member", "TokenTypeException")]
    public void Constructor_WithExpectedActualAndInnerException_SetsMessageAndProperties()
    {
        // Arrange
        string expected = "expectedType";
        string actual = "actualType";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new TokenTypeException(expected, actual, innerException);

        // Assert
        exception.Message.Should().Be("Incorrect token type. Expected: expectedType. Actual: actualType.");
        exception.ExpectedType.Should().Be(expected);
        exception.ActualType.Should().Be(actual);
        exception.InnerException.Should().Be(innerException);
    }

    #endregion TokenTypeException(string?, string?, Exception?)
}

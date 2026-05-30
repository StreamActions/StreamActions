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
using StreamActions.Common.Extensions;

namespace StreamActions.Common.Tests.Extensions;

/// <summary>
/// Performs unit testing on methods in <see cref="DateTimeExtensions"/>.
/// </summary>
public class DateTimeExtensionsTests
{
    [Fact]
    [Trait("Member", "ToRfc3339")]
    public void ToRfc3339_WithUtcDateTime_ReturnsCorrectString()
    {
        // Arrange
        DateTime dt = new(2023, 10, 24, 15, 30, 45, DateTimeKind.Utc);

        // Act
        string result = dt.ToRfc3339();

        // Assert
        result.Should().Be("2023-10-24T15:30:45Z");
    }

    [Fact]
    [Trait("Member", "ToRfc3339")]
    public void ToRfc3339_WithUnspecifiedDateTime_TreatsAsLocalAndConvertsToUtcString()
    {
        // Arrange
        DateTime dtUnspecified = new(2023, 10, 24, 15, 30, 45, DateTimeKind.Unspecified);
        DateTime dtExpected = dtUnspecified.ToUniversalTime();
        string expectedString = dtExpected.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        // Act
        string result = dtUnspecified.ToRfc3339();

        // Assert
        result.Should().Be(expectedString);
    }

    [Fact]
    [Trait("Member", "ToRfc3339")]
    public void ToRfc3339_WithLocalDateTime_ConvertsToUtcString()
    {
        // Arrange
        DateTime dtLocal = new(2023, 10, 24, 15, 30, 45, DateTimeKind.Local);
        DateTime dtExpected = dtLocal.ToUniversalTime();
        string expectedString = dtExpected.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        // Act
        string result = dtLocal.ToRfc3339();

        // Assert
        result.Should().Be(expectedString);
    }

    [Fact]
    [Trait("Member", "ToRfc3339")]
    public void ToRfc3339_WithMinValue_ReturnsCorrectString()
    {
        // Arrange
        DateTime dt = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

        // Act
        string result = dt.ToRfc3339();

        // Assert
        // The expected value is 0001-01-01T00:00:00Z but might vary based on local time conversion if MinValue is local.
        // We use MinValue.ToUniversalTime() to ensure it's in UTC for a deterministic test.
        result.Should().Be("0001-01-01T00:00:00Z");
    }

    [Fact]
    [Trait("Member", "ToRfc3339")]
    public void ToRfc3339_WithMaxValue_ReturnsCorrectString()
    {
        // Arrange
        DateTime dt = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

        // Act
        string result = dt.ToRfc3339();

        // Assert
        result.Should().Be("9999-12-31T23:59:59Z");
    }
}

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
using FluentAssertions;
using StreamActions.Common.Json.Serialization;
using Xunit;

namespace StreamActions.Common.Tests.Json.Serialization;

/// <summary>
/// Performs unit testing on <see cref="JsonCustomEnumAttribute"/>.
/// </summary>
public class JsonCustomEnumAttributeTests
{
    #region Constructor

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_SetsValueProperty()
    {
        // Arrange
        const string expectedValue = "test-value";

        // Act
        var attribute = new JsonCustomEnumAttribute(expectedValue);

        // Assert
        attribute.Value.Should().Be(expectedValue);
    }

    #endregion Constructor

    #region AttributeUsage

    [Fact]
    [Trait("Member", "AttributeUsage")]
    public void Class_HasCorrectAttributeUsage()
    {
        // Arrange
        var type = typeof(JsonCustomEnumAttribute);

        // Act
        var attributes = type.GetCustomAttributes(typeof(AttributeUsageAttribute), false);

        // Assert
        attributes.Should().ContainSingle();
        var usageAttribute = (AttributeUsageAttribute)attributes[0];
        usageAttribute.ValidOn.Should().Be(AttributeTargets.Field);
        usageAttribute.AllowMultiple.Should().BeFalse();
    }

    #endregion AttributeUsage
}

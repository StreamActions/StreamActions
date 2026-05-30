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

using System.Collections.Specialized;
using StreamActions.Common.Extensions;

namespace StreamActions.Common.Tests.Extensions;

public class NameValueCollectionExtensionsTests
{
    [Fact]
    public void Add_NullCollection_ThrowsArgumentNullException()
    {
        // Arrange
        NameValueCollection? collection = null;

        // Act & Assert
        Action act = () => collection!.Add("key", ["value1"]);
        act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
    }

    [Fact]
    public void Add_NullValues_ThrowsArgumentNullException()
    {
        // Arrange
        NameValueCollection collection = [];
        IEnumerable<string?>? values = null;

        // Act & Assert
        Action act = () => collection.Add("key", values!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("values");
    }

    [Fact]
    public void Add_ValidValues_AddsToCollection()
    {
        // Arrange
        NameValueCollection collection = [];
        List<string> values = ["value1", "value2"];

        // Act
        collection.Add("key", values);

        // Assert
        collection.GetValues("key").Should().BeEquivalentTo(values);
    }

    [Fact]
    public void Add_WithNullName_AddsToCollection()
    {
        // Arrange
        NameValueCollection collection = [];
        List<string> values = ["value1", "value2"];

        // Act
        collection.Add(null, values);

        // Assert
        collection.GetValues(null).Should().BeEquivalentTo(values);
    }

    [Fact]
    public void Add_WithNullValuesInEnumerable_AddsToCollection()
    {
        // Arrange
        NameValueCollection collection = [];
        List<string?> values = ["value1", null, "value3"];

        // Act
        collection.Add("key", values);

        // Assert
        // NameValueCollection ignores null values when retrieving via GetValues if the key already exists.
        collection.GetValues("key").Should().BeEquivalentTo(new[] { "value1", "value3" });
    }
}

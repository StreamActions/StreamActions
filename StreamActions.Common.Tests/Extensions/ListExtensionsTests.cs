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
using System.Collections.Generic;
using StreamActions.Common.Extensions;

namespace StreamActions.Common.Tests.Extensions;

public class ListExtensionsTests
{
    #region GetElementAtOrDefault

    [Fact]
    public void GetElementAtOrDefault_ThrowsArgumentNullException_WhenSourceIsNull()
    {
        List<int> source = null!;
        Action act = () => source.GetElementAtOrDefault(0);
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void GetElementAtOrDefault_ReturnsDefaultValue_WhenIndexIsNegative()
    {
        List<int> source = [1, 2, 3];
        var result = source.GetElementAtOrDefault(-1);
        result.Should().Be(0); // default(int)
    }

    [Fact]
    public void GetElementAtOrDefault_ReturnsElement_WhenIndexIsValid()
    {
        List<int> source = [10, 20, 30];
        var result = source.GetElementAtOrDefault(1);
        result.Should().Be(20);
    }

    [Fact]
    public void GetElementAtOrDefault_ReturnsDefaultValue_WhenIndexIsOutOfRange()
    {
        List<int> source = [10, 20, 30];
        var result = source.GetElementAtOrDefault(5);
        result.Should().Be(0); // default(int)
    }

    [Fact]
    public void GetElementAtOrDefault_ReturnsSpecifiedDefaultValue_WhenIndexIsOutOfRange()
    {
        List<int> source = [10, 20, 30];
        var result = source.GetElementAtOrDefault(5, 99);
        result.Should().Be(99);
    }

    #endregion GetElementAtOrDefault

    #region IsNullEmptyOrOutOfRange

    [Fact]
    public void IsNullEmptyOrOutOfRange_ThrowsArgumentNullException_WhenSourceIsNull()
    {
        List<string> source = null!;
        Action act = () => source.IsNullEmptyOrOutOfRange(0);
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void IsNullEmptyOrOutOfRange_ReturnsTrue_WhenIndexIsNegative()
    {
        List<string> source = ["test"];
        var result = source.IsNullEmptyOrOutOfRange(-1);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullEmptyOrOutOfRange_ReturnsTrue_WhenIndexIsOutOfRange()
    {
        List<string> source = ["test"];
        var result = source.IsNullEmptyOrOutOfRange(5);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullEmptyOrOutOfRange_ReturnsTrue_WhenElementIsNull()
    {
        List<string> source = [null!];
        var result = source.IsNullEmptyOrOutOfRange(0);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullEmptyOrOutOfRange_ReturnsTrue_WhenElementIsEmpty()
    {
        List<string> source = [""];
        var result = source.IsNullEmptyOrOutOfRange(0);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullEmptyOrOutOfRange_ReturnsFalse_WhenElementHasContent()
    {
        List<string> source = ["content"];
        var result = source.IsNullEmptyOrOutOfRange(0);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsNullEmptyOrOutOfRange_ReturnsFalse_WhenElementIsWhitespace()
    {
        List<string> source = ["   "];
        var result = source.IsNullEmptyOrOutOfRange(0);
        result.Should().BeFalse();
    }

    #endregion IsNullEmptyOrOutOfRange

    #region IsNullWhiteSpaceOrOutOfRange

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ThrowsArgumentNullException_WhenSourceIsNull()
    {
        List<string?> source = null!;
        Action act = () => source.IsNullWhiteSpaceOrOutOfRange(0);
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ReturnsTrue_WhenIndexIsNegative()
    {
        List<string?> source = ["test"];
        var result = source.IsNullWhiteSpaceOrOutOfRange(-1);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ReturnsTrue_WhenIndexIsOutOfRange()
    {
        List<string?> source = ["test"];
        var result = source.IsNullWhiteSpaceOrOutOfRange(5);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ReturnsTrue_WhenElementIsNull()
    {
        List<string?> source = [null];
        var result = source.IsNullWhiteSpaceOrOutOfRange(0);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ReturnsTrue_WhenElementIsEmpty()
    {
        List<string?> source = [""];
        var result = source.IsNullWhiteSpaceOrOutOfRange(0);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ReturnsTrue_WhenElementIsWhitespace()
    {
        List<string?> source = ["   "];
        var result = source.IsNullWhiteSpaceOrOutOfRange(0);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNullWhiteSpaceOrOutOfRange_ReturnsFalse_WhenElementHasContent()
    {
        List<string?> source = ["content"];
        var result = source.IsNullWhiteSpaceOrOutOfRange(0);
        result.Should().BeFalse();
    }

    #endregion IsNullWhiteSpaceOrOutOfRange
}

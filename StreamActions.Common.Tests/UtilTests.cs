/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Tests;

/// <summary>
/// Performs unit testing on the <see cref="Util.IsValidHexColor(string)"/> method.
/// </summary>
public class UtilTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("#000000", true)]
    [InlineData("#123456", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("#FF573", false)]
    [InlineData("#FF5733", true)]
    [InlineData("#FF57333", false)]
    [InlineData("#FFFFFF", true)]
    [InlineData("#F", false)]
    [InlineData("#FF", false)]
    [InlineData("#FFF", false)]
    [InlineData("#FFFF", false)]
    [InlineData("#FFFFF", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("#aBcDeF", true)]
    [InlineData("FF5733", false)]
    [InlineData("The hex color is #FF5733 in the middle of this line.", false)]
    [InlineData("#FF5733 is at the beginning of this line.", false)]
    [InlineData("This line ends with #FF5733.", false)]
    [InlineData(null, false)]
    public void IsValidHexColorTest(string hexColor, bool expected) => Util.IsValidHexColor(hexColor).Should().Be(expected);
}

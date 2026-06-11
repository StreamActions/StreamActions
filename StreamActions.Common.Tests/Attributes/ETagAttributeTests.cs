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
using System.Globalization;
using StreamActions.Common.Attributes;

namespace StreamActions.Common.Tests.Attributes;

/// <summary>
/// Performs unit testing on methods in <see cref="ETagAttribute"/>.
/// </summary>
public class ETagAttributeTests
{
    #region Constructor

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
    {
        // Arrange
        string expectedFriendlyName = "FriendlyName";
        int expectedIssueId = 123;
        string expectedUri = "https://example.com/api";
        string expectedETag = "abcdef123456";
        string expectedTimestampString = "2023-10-25T14:30:00Z";
        DateTime expectedTimestamp = DateTime.Parse(expectedTimestampString, CultureInfo.InvariantCulture);
        string expectedParser = "MyParser";
        string[] expectedParameters = ["param1", "param2"];

        // Act
        ETagAttribute attribute = new(
            expectedFriendlyName,
            expectedIssueId,
            expectedUri,
            expectedETag,
            expectedTimestampString,
            expectedParser,
            expectedParameters);

        // Assert
        _ = attribute.FriendlyName.Should().Be(expectedFriendlyName);
        _ = attribute.IssueId.Should().Be(expectedIssueId);
        _ = attribute.Uri.Should().Be(expectedUri);
        _ = attribute.ETag.Should().Be(expectedETag);
        _ = attribute.Timestamp.Should().Be(expectedTimestamp);
        _ = attribute.Parser.Should().Be(expectedParser);
        _ = attribute.Parameters.Should().BeEquivalentTo(expectedParameters);
    }

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_WithInvalidTimestampFormat_ThrowsFormatException()
    {
        // Arrange
        string friendlyName = "FriendlyName";
        int issueId = 123;
        string uri = "https://example.com/api";
        string eTag = "abcdef123456";
        string invalidTimestamp = "invalid-date";
        string parser = "MyParser";
        string[] parameters = ["param1", "param2"];

        // Act
        Action act = () => new ETagAttribute(
            friendlyName,
            issueId,
            uri,
            eTag,
            invalidTimestamp,
            parser,
            parameters);

        // Assert
        _ = act.Should().Throw<FormatException>();
    }

    #endregion Constructor
}

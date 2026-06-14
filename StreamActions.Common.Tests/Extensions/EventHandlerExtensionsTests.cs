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

using StreamActions.Common.Extensions;
using System;
using System.Threading.Tasks;

namespace StreamActions.Common.Tests.Extensions;

/// <summary>
/// Performs unit testing on methods in <see cref="EventHandlerExtensions"/>.
/// </summary>
public class EventHandlerExtensionsTests
{
    #region InvokeAsync

    [Fact]
    [Trait("Member", "InvokeAsync")]
    public async Task InvokeAsync_WhenHandlerIsNotNull_InvokesHandler()
    {
        // Arrange
        bool wasInvoked = false;
        object expectedSender = new();
        EventArgs expectedArgs = new();

        EventHandler<EventArgs> handler = (sender, e) =>
        {
            sender.Should().Be(expectedSender);
            e.Should().Be(expectedArgs);
            wasInvoked = true;
        };

        // Act
        await handler.InvokeAsync(expectedSender, expectedArgs).ConfigureAwait(true);

        // Assert
        wasInvoked.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "InvokeAsync")]
    public async Task InvokeAsync_WhenHandlerIsNull_DoesNotThrow()
    {
        // Arrange
        EventHandler<EventArgs>? handler = null;

        // Act
        Func<Task> act = async () => await handler!.InvokeAsync(new object(), new EventArgs()).ConfigureAwait(true);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(true);
    }

    #endregion
}

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
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using StreamActions.Common.Extensions;
using Xunit;

namespace StreamActions.Common.Tests.Extensions;

/// <summary>
/// Performs unit testing on methods in <see cref="CancellationTokenExtensions"/>.
/// </summary>
public class CancellationTokenExtensionsTests
{
    #region WaitAsync

    [Fact]
    [Trait("Member", "WaitAsync")]
    public async Task WaitAsync_TokenAlreadyCanceled_CompletesImmediately()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Task waitTask = cts.Token.WaitAsync();

        // Wait a short time to allow the background task to complete if it hasn't already.
        // WaitAsync spawns a background thread (Task.Run), so it might take a moment.
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(1000));

        // Assert
        completedTask.Should().Be(waitTask, "the task should complete immediately since the token was already canceled");
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "WaitAsync")]
    public async Task WaitAsync_TokenCanceledAfterDelay_CompletesTask()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        // Act
        Task waitTask = cts.Token.WaitAsync();

        // Assert it's not complete initially
        waitTask.IsCompleted.Should().BeFalse("the task should not complete before the token is canceled");

        // Cancel after a short delay
        cts.CancelAfter(50);

        // Wait for it to complete or timeout
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(2000));

        // Assert
        completedTask.Should().Be(waitTask, "the task should complete after the token is canceled");
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "WaitAsync")]
    public async Task WaitAsync_TokenNotCanceled_DoesNotComplete()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        // Act
        Task waitTask = cts.Token.WaitAsync();

        // Wait a short amount of time
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(100));

        // Assert
        completedTask.Should().NotBe(waitTask, "the task should not complete if the token is not canceled");
        waitTask.IsCompleted.Should().BeFalse();
    }

    #endregion WaitAsync
}

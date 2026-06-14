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
using StreamActions.Common.Limiters;
using System;
using System.Reflection;
using Xunit;

namespace StreamActions.Common.Tests.Limiters;

public class LinearBackoffTests
{
    #region CalcNextDurationTicks

    [Fact]
    [Trait("Member", "CalcNextDurationTicks")]
    public void LinearBackoff_CalcNextDurationTicks_CalculatesNextDurationCorrectly_AndHandlesMultipleCalls()
    {
        // Arrange
        var initial = TimeSpan.FromMilliseconds(10);
        var max = TimeSpan.FromMilliseconds(50);
        var step = TimeSpan.FromMilliseconds(15);

        // LinearBackoff is sealed, so we must use reflection to invoke the protected override CalcNextDurationTicks
        var backoff = new LinearBackoff(initial, max, step);
        var calcMethod = typeof(LinearBackoff).GetMethod("CalcNextDurationTicks", BindingFlags.NonPublic | BindingFlags.Instance);
        var nextDurationField = typeof(BackoffBase).GetField("_nextDuration", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert 1: Initial state
        var nextTicksResult1 = calcMethod?.Invoke(backoff, null);
        nextTicksResult1.Should().NotBeNull();
        long nextTicks1 = (long)nextTicksResult1!;
        nextTicks1.Should().Be(TimeSpan.FromMilliseconds(25).Ticks);

        // Act & Assert 2: Second call (simulate waiting and updating internal state)
        nextDurationField?.SetValue(backoff, TimeSpan.FromTicks(nextTicks1));
        var nextTicksResult2 = calcMethod?.Invoke(backoff, null);
        nextTicksResult2.Should().NotBeNull();
        long nextTicks2 = (long)nextTicksResult2!;
        nextTicks2.Should().Be(TimeSpan.FromMilliseconds(40).Ticks);

        // Act & Assert 3: Third call (simulate waiting and updating internal state)
        nextDurationField?.SetValue(backoff, TimeSpan.FromTicks(nextTicks2));
        var nextTicksResult3 = calcMethod?.Invoke(backoff, null);
        nextTicksResult3.Should().NotBeNull();
        long nextTicks3 = (long)nextTicksResult3!;
        // The base class logic handles clamping in Wait(), so Calc just returns the raw math.
        nextTicks3.Should().Be(TimeSpan.FromMilliseconds(55).Ticks);
    }

    [Fact]
    [Trait("Member", "Properties")]
    public void LinearBackoff_Properties_ReturnCorrectValues()
    {
        // Arrange
        var initial = TimeSpan.FromMilliseconds(10);
        var max = TimeSpan.FromMilliseconds(100);
        var step = TimeSpan.FromMilliseconds(5);

        // Act
        var backoff = new LinearBackoff(initial, max, step);

        // Assert
        backoff.IntialDuration.Should().Be(initial);
        backoff.MaxDuration.Should().Be(max);
        backoff.StepSize.Should().Be(step);
        backoff.NextDuration.Should().Be(initial);
        backoff.IsReset.Should().BeTrue();
        backoff.IsMaxDuration.Should().BeFalse();
    }

    #endregion
}

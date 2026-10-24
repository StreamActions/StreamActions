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

using StreamActions.Common.Limiters;

namespace StreamActions.Common.Tests.Limiters;

public sealed class ExponentialBackoffTests
{
    #region CalcNextDurationTicks

    [Fact]
    [Trait("Member", "CalcNextDurationTicks")]
    public async Task CalcNextDurationTicks_DoublesDuration_UntilMax()
    {
        TimeSpan initial = TimeSpan.FromTicks(10);
        TimeSpan max = TimeSpan.FromTicks(100);
        ExponentialBackoff backoff = new(initial, max);

        _ = backoff.NextDuration.Should().Be(initial);

        Task task = backoff.Wait(TimeSpan.FromSeconds(1));
        Task completedTask = await Task.WhenAny(task, Task.Delay(2000, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        _ = completedTask.Should().Be(task);
        await task.ConfigureAwait(true); // propagate any exceptions
        _ = backoff.NextDuration.Should().Be(TimeSpan.FromTicks(20));

        task = backoff.Wait(TimeSpan.FromSeconds(1));
        completedTask = await Task.WhenAny(task, Task.Delay(2000, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        _ = completedTask.Should().Be(task);
        await task.ConfigureAwait(true);
        _ = backoff.NextDuration.Should().Be(TimeSpan.FromTicks(40));

        task = backoff.Wait(TimeSpan.FromSeconds(1));
        completedTask = await Task.WhenAny(task, Task.Delay(2000, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        _ = completedTask.Should().Be(task);
        await task.ConfigureAwait(true);
        _ = backoff.NextDuration.Should().Be(TimeSpan.FromTicks(80));

        // 80 * 2 = 160, but max is 100
        task = backoff.Wait(TimeSpan.FromSeconds(1));
        completedTask = await Task.WhenAny(task, Task.Delay(2000, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        _ = completedTask.Should().Be(task);
        await task.ConfigureAwait(true);
        _ = backoff.NextDuration.Should().Be(max);

        // Subsequent waits should remain at max
        task = backoff.Wait(TimeSpan.FromSeconds(1));
        completedTask = await Task.WhenAny(task, Task.Delay(2000, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        _ = completedTask.Should().Be(task);
        await task.ConfigureAwait(true);
        _ = backoff.NextDuration.Should().Be(max);
    }

    #endregion CalcNextDurationTicks

    #region Reset

    [Fact]
    [Trait("Member", "Reset")]
    public async Task Reset_RestoresInitialDuration()
    {
        TimeSpan initial = TimeSpan.FromTicks(10);
        TimeSpan max = TimeSpan.FromTicks(100);
        ExponentialBackoff backoff = new(initial, max);

        _ = backoff.NextDuration.Should().Be(initial);

        Task task = backoff.Wait(TimeSpan.FromSeconds(1));
        Task completedTask = await Task.WhenAny(task, Task.Delay(2000, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        _ = completedTask.Should().Be(task);
        await task.ConfigureAwait(true);

        _ = backoff.NextDuration.Should().Be(TimeSpan.FromTicks(20));

        backoff.Reset(TimeSpan.FromSeconds(1));

        _ = backoff.NextDuration.Should().Be(initial);
    }

    #endregion Reset
}

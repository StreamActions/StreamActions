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
using System.Globalization;
using System.Reflection;

namespace StreamActions.Common.Tests.Limiters;

public sealed class TokenBucketRateLimiterTests
{
    #region ReturnToken

    [Fact]
    [Trait("Member", "ReturnToken")]
    public void ReturnToken_IncrementsRemainingTokenCount()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        MethodInfo? returnTokenMethod = typeof(TokenBucketRateLimiter).GetMethod("ReturnToken", BindingFlags.Instance | BindingFlags.NonPublic);

        long initial = limiter.Remaining;
        _ = returnTokenMethod!.Invoke(limiter, [TimeSpan.FromSeconds(1)]);

        _ = limiter.Remaining.Should().Be((int)(initial + 1));
    }

    [Fact]
    [Trait("Member", "ReturnToken")]
    public async Task ReturnToken_ThrowsTimeoutException_WhenUpgradeableReadLockFails()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        MethodInfo? returnTokenMethod = typeof(TokenBucketRateLimiter).GetMethod("ReturnToken", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? rwlField = typeof(TokenBucketRateLimiter).GetField("_rwl", BindingFlags.Instance | BindingFlags.NonPublic);
        ReaderWriterLockSlim rwl = (ReaderWriterLockSlim)rwlField!.GetValue(limiter)!;

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource tcs2 = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task t = Task.Run(() =>
        {
            rwl.EnterWriteLock();
            tcs.SetResult();
            tcs2.Task.Wait();
            rwl.ExitWriteLock();
        }, TestContext.Current.CancellationToken);

        await tcs.Task.ConfigureAwait(true);

        try
        {
            Action act = () =>
            {
                try
                {
                    _ = returnTokenMethod!.Invoke(limiter, [TimeSpan.FromMilliseconds(10)]);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException!;
                }
            };

            _ = act.Should().Throw<TimeoutException>().WithMessage("Timed out attempting to acquire upgradeable read lock.");
        }
        finally
        {
            tcs2.SetResult();
            await t.ConfigureAwait(true);
        }
    }

    [Fact]
    [Trait("Member", "ReturnToken")]
    public async Task ReturnToken_ThrowsTimeoutException_WhenWriteLockFails()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        MethodInfo? returnTokenMethod = typeof(TokenBucketRateLimiter).GetMethod("ReturnToken", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? rwlField = typeof(TokenBucketRateLimiter).GetField("_rwl", BindingFlags.Instance | BindingFlags.NonPublic);
        ReaderWriterLockSlim rwl = (ReaderWriterLockSlim)rwlField!.GetValue(limiter)!;

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource tcs2 = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task t = Task.Run(() =>
        {
            rwl.EnterReadLock();
            tcs.SetResult();
            tcs2.Task.Wait();
            rwl.ExitReadLock();
        }, TestContext.Current.CancellationToken);

        await tcs.Task.ConfigureAwait(true);

        try
        {
            Action act = () =>
            {
                try
                {
                    _ = returnTokenMethod!.Invoke(limiter, [TimeSpan.FromMilliseconds(100)]);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException!;
                }
            };

            _ = act.Should().Throw<TimeoutException>().WithMessage("Timed out attempting to acquire write lock.");
        }
        finally
        {
            tcs2.SetResult();
            await t.ConfigureAwait(true);
        }
    }

    #endregion ReturnToken

    #region UpdateLimit

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_UpdatesLimitAndRemainingWhenLower()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));

        limiter.UpdateLimit(3);

        _ = limiter.Limit.Should().Be(3);
        _ = limiter.Remaining.Should().Be(3);
    }

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_UpdatesLimitOnlyWhenHigher()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        limiter.UpdateRemaining(2);

        limiter.UpdateLimit(10);

        _ = limiter.Limit.Should().Be(10);
        _ = limiter.Remaining.Should().Be(2);
    }

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_FailsSilentlyWhenLessThanOne()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));

        limiter.UpdateLimit(0);

        _ = limiter.Limit.Should().Be(5);
        _ = limiter.Remaining.Should().Be(5);

        limiter.UpdateLimit(-1);

        _ = limiter.Limit.Should().Be(5);
        _ = limiter.Remaining.Should().Be(5);
    }

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_DoesNothingWhenSame()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));

        limiter.UpdateLimit(5);

        _ = limiter.Limit.Should().Be(5);
        _ = limiter.Remaining.Should().Be(5);
    }

    #endregion UpdateLimit

    #region WaitForRateLimit

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_AcquiresTokenImmediatelyWhenAvailable()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        // Wait should complete successfully, consuming the token
        _ = limiter.Remaining.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ThrowsTimeoutException_WhenNoTokenAndTimeoutExceeded()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

        _ = await act.Should().ThrowAsync<TimeoutException>().WithMessage("Timed out waiting for the rate limit.").ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ThrowsOperationCanceledException_WhenCanceled()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync().ConfigureAwait(true);

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(true);

        _ = await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_WaitsForRefillAndAcquiresToken()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        _ = limiter.Remaining.Should().Be(0);
    }

    #endregion WaitForRateLimit

    #region ParseHeaders

    [Fact]
    [Trait("Member", "ParseHeaders")]
    public void ParseHeaders_UpdatesLimitRemainingAndNextReset_WhenHeadersPresent_Seconds()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = new();
        response.Headers.Add("Ratelimit-Limit", "100");
        response.Headers.Add("Ratelimit-Remaining", "50");
        long futureEpoch = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        response.Headers.Add("Ratelimit-Reset", futureEpoch.ToString(CultureInfo.InvariantCulture)); // some unix timestamp in seconds

        limiter.ParseHeaders(response.Headers);

        _ = limiter.Limit.Should().Be(100);
        _ = limiter.Remaining.Should().Be(50);
        _ = limiter.NextReset.Should().Be(DateTimeOffset.FromUnixTimeSeconds(futureEpoch).UtcDateTime.Ticks);
    }

    [Fact]
    [Trait("Member", "ParseHeaders")]
    public void ParseHeaders_UpdatesNextReset_WhenHeaderResetTypeIsMilliseconds()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = new();
        // Add dummy headers to prevent InvalidOperationException on missing headers
        response.Headers.Add("Ratelimit-Limit", "100");
        response.Headers.Add("Ratelimit-Remaining", "50");
        long futureEpoch = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        response.Headers.Add("Ratelimit-Reset", futureEpoch.ToString(CultureInfo.InvariantCulture));

        limiter.ParseHeaders(response.Headers, headerResetType: TokenBucketRateLimiter.HeaderResetType.Milliseconds);

        _ = limiter.NextReset.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(futureEpoch).UtcDateTime.Ticks);
    }

    [Fact]
    [Trait("Member", "ParseHeaders")]
    public void ParseHeaders_UpdatesNextReset_WhenHeaderResetTypeIsTicks()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = new();
        // Add dummy headers to prevent InvalidOperationException on missing headers
        response.Headers.Add("Ratelimit-Limit", "100");
        response.Headers.Add("Ratelimit-Remaining", "50");
        long futureTicks = DateTime.UtcNow.Ticks + TimeSpan.FromHours(1).Ticks;
        response.Headers.Add("Ratelimit-Reset", futureTicks.ToString(CultureInfo.InvariantCulture));

        limiter.ParseHeaders(response.Headers, headerResetType: TokenBucketRateLimiter.HeaderResetType.Ticks);

        _ = limiter.NextReset.Should().Be(futureTicks);
    }

    [Fact]
    [Trait("Member", "ParseHeaders")]
    public void ParseHeaders_UpdatesNextReset_WhenHeaderResetTypeIsISO8601()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = new();
        // Add dummy headers to prevent InvalidOperationException on missing headers
        response.Headers.Add("Ratelimit-Limit", "100");
        response.Headers.Add("Ratelimit-Remaining", "50");
        string isoDate = DateTime.UtcNow.AddHours(1).ToString("O");
        response.Headers.Add("Ratelimit-Reset", isoDate);

        limiter.ParseHeaders(response.Headers, headerResetType: TokenBucketRateLimiter.HeaderResetType.ISO8601);

        _ = limiter.NextReset.Should().Be(DateTime.Parse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal).Ticks);
    }

    [Fact]
    [Trait("Member", "ParseHeaders")]
    public void ParseHeaders_IgnoresInvalidHeaderValues()
    {
        using TokenBucketRateLimiter limiter = new(10, TimeSpan.FromSeconds(10));
        limiter.UpdateRemaining(5);
        long originalReset = limiter.NextReset;

        using HttpResponseMessage response = new();
        response.Headers.Add("Ratelimit-Limit", "invalid");
        response.Headers.Add("Ratelimit-Remaining", "invalid");
        response.Headers.Add("Ratelimit-Reset", "invalid");

        limiter.ParseHeaders(response.Headers);

        _ = limiter.Limit.Should().Be(10);
        _ = limiter.Remaining.Should().Be(5);
        _ = limiter.NextReset.Should().Be(originalReset);
    }

    [Fact]
    [Trait("Member", "ParseHeaders")]
    public void ParseHeaders_ThrowsArgumentOutOfRangeException_ForInvalidHeaderResetType()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = new();
        // Add dummy headers to prevent InvalidOperationException on missing headers
        response.Headers.Add("Ratelimit-Limit", "100");
        response.Headers.Add("Ratelimit-Remaining", "50");
        response.Headers.Add("Ratelimit-Reset", "1000");

        Action act = () => limiter.ParseHeaders(response.Headers, headerResetType: (TokenBucketRateLimiter.HeaderResetType)999);

        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion ParseHeaders
    #region UpdatePeriod

    [Fact]
    [Trait("Member", "UpdatePeriod")]
    public async Task UpdatePeriod_UpdatesPeriodTicks_Successfully()
    {
        // 100ms
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        // Update period to 50ms
        limiter.UpdatePeriod(TimeSpan.FromMilliseconds(50).Ticks);

        _ = limiter.Period.Should().Be(TimeSpan.FromMilliseconds(50).Ticks);

        // Consume the token, the next one should be available in 50ms instead of 100ms
        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        _ = limiter.Remaining.Should().Be(0);

        long expectedNextReset = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks;
        // Check if next reset is around 50ms from now (allow a small margin for execution time)
        _ = Math.Abs(limiter.NextResetDateTime.Ticks - expectedNextReset).Should().BeLessThan(TimeSpan.FromMilliseconds(500).Ticks);
    }

    [Fact]
    [Trait("Member", "UpdatePeriod")]
    public async Task UpdatePeriod_UpdatesPeriodTimeSpan_Successfully()
    {
        // 100ms
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        // Update period to 50ms
        limiter.UpdatePeriod(TimeSpan.FromMilliseconds(50));

        _ = limiter.PeriodTimeSpan.Should().Be(TimeSpan.FromMilliseconds(50));

        // Consume the token, the next one should be available in 50ms instead of 100ms
        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        _ = limiter.Remaining.Should().Be(0);

        long expectedNextReset = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks;
        // Check if next reset is around 50ms from now (allow a small margin for execution time)
        _ = Math.Abs(limiter.NextResetDateTime.Ticks - expectedNextReset).Should().BeLessThan(TimeSpan.FromMilliseconds(500).Ticks);
    }

    [Theory]
    [Trait("Member", "UpdatePeriod")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void UpdatePeriod_FailsSilentlyIfPeriodIsLessThanOne(long invalidPeriodTicks)
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdatePeriod(invalidPeriodTicks);

        _ = limiter.Period.Should().Be(TimeSpan.FromMilliseconds(100).Ticks);
    }

    #endregion UpdatePeriod
}

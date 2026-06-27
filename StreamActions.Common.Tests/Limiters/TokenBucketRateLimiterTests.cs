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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StreamActions.Common.Tests.Limiters;

public sealed class TokenBucketRateLimiterTests
{
    #region ReturnToken

    [Fact]
    [Trait("Member", "ReturnToken")]
    public void ReturnToken_IncrementsRemainingTokenCount()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        var returnTokenMethod = typeof(TokenBucketRateLimiter).GetMethod("ReturnToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        long initial = limiter.Remaining;
        returnTokenMethod!.Invoke(limiter, new object[] { TimeSpan.FromSeconds(1) });

        limiter.Remaining.Should().Be((int)(initial + 1));
    }

    [Fact]
    [Trait("Member", "ReturnToken")]
    public async Task ReturnToken_ThrowsTimeoutException_WhenUpgradeableReadLockFails()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        var returnTokenMethod = typeof(TokenBucketRateLimiter).GetMethod("ReturnToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rwlField = typeof(TokenBucketRateLimiter).GetField("_rwl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rwl = (ReaderWriterLockSlim)rwlField!.GetValue(limiter)!;

        var tcs = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        var t = Task.Run(() =>
        {
            rwl.EnterWriteLock();
            tcs.SetResult();
            tcs2.Task.Wait();
            rwl.ExitWriteLock();
        });

        await tcs.Task.ConfigureAwait(true);

        try
        {
            Action act = () =>
            {
                try
                {
                    returnTokenMethod!.Invoke(limiter, new object[] { TimeSpan.FromMilliseconds(10) });
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    throw ex.InnerException!;
                }
            };

            act.Should().Throw<TimeoutException>().WithMessage("Timed out attempting to acquire upgradeable read lock.");
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
        var returnTokenMethod = typeof(TokenBucketRateLimiter).GetMethod("ReturnToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rwlField = typeof(TokenBucketRateLimiter).GetField("_rwl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rwl = (ReaderWriterLockSlim)rwlField!.GetValue(limiter)!;

        var tcs = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        var t = Task.Run(() =>
        {
            rwl.EnterReadLock();
            tcs.SetResult();
            tcs2.Task.Wait();
            rwl.ExitReadLock();
        });

        await tcs.Task.ConfigureAwait(true);

        try
        {
            Action act = () =>
            {
                try
                {
                    returnTokenMethod!.Invoke(limiter, new object[] { TimeSpan.FromMilliseconds(100) });
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    throw ex.InnerException!;
                }
            };

            act.Should().Throw<TimeoutException>().WithMessage("Timed out attempting to acquire write lock.");
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

        limiter.Limit.Should().Be(3);
        limiter.Remaining.Should().Be(3);
    }

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_UpdatesLimitOnlyWhenHigher()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));
        limiter.UpdateRemaining(2);

        limiter.UpdateLimit(10);

        limiter.Limit.Should().Be(10);
        limiter.Remaining.Should().Be(2);
    }

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_FailsSilentlyWhenLessThanOne()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));

        limiter.UpdateLimit(0);

        limiter.Limit.Should().Be(5);
        limiter.Remaining.Should().Be(5);

        limiter.UpdateLimit(-1);

        limiter.Limit.Should().Be(5);
        limiter.Remaining.Should().Be(5);
    }

    [Fact]
    [Trait("Member", "UpdateLimit")]
    public void UpdateLimit_DoesNothingWhenSame()
    {
        using TokenBucketRateLimiter limiter = new(5, TimeSpan.FromSeconds(10));

        limiter.UpdateLimit(5);

        limiter.Limit.Should().Be(5);
        limiter.Remaining.Should().Be(5);
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
        limiter.Remaining.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ThrowsTimeoutException_WhenNoTokenAndTimeoutExceeded()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("Timed out waiting for the rate limit.").ConfigureAwait(true);
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

        await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_WaitsForRefillAndAcquiresToken()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        limiter.Remaining.Should().Be(0);
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
        response.Headers.Add("Ratelimit-Reset", futureEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture)); // some unix timestamp in seconds

        limiter.ParseHeaders(response.Headers);

        limiter.Limit.Should().Be(100);
        limiter.Remaining.Should().Be(50);
        limiter.NextReset.Should().Be(DateTimeOffset.FromUnixTimeSeconds(futureEpoch).UtcDateTime.Ticks);
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
        response.Headers.Add("Ratelimit-Reset", futureEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture));

        limiter.ParseHeaders(response.Headers, headerResetType: TokenBucketRateLimiter.HeaderResetType.Milliseconds);

        limiter.NextReset.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(futureEpoch).UtcDateTime.Ticks);
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
        response.Headers.Add("Ratelimit-Reset", futureTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));

        limiter.ParseHeaders(response.Headers, headerResetType: TokenBucketRateLimiter.HeaderResetType.Ticks);

        limiter.NextReset.Should().Be(futureTicks);
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

        limiter.NextReset.Should().Be(DateTime.Parse(isoDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal).Ticks);
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

        limiter.Limit.Should().Be(10);
        limiter.Remaining.Should().Be(5);
        limiter.NextReset.Should().Be(originalReset);
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

        act.Should().Throw<ArgumentOutOfRangeException>();
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

        limiter.Period.Should().Be(TimeSpan.FromMilliseconds(50).Ticks);

        // Consume the token, the next one should be available in 50ms instead of 100ms
        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        limiter.Remaining.Should().Be(0);

        long expectedNextReset = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks;
        // Check if next reset is around 50ms from now (allow a small margin for execution time)
        Math.Abs(limiter.NextResetDateTime.Ticks - expectedNextReset).Should().BeLessThan(TimeSpan.FromMilliseconds(500).Ticks);
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

        limiter.PeriodTimeSpan.Should().Be(TimeSpan.FromMilliseconds(50));

        // Consume the token, the next one should be available in 50ms instead of 100ms
        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        limiter.Remaining.Should().Be(0);

        long expectedNextReset = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks;
        // Check if next reset is around 50ms from now (allow a small margin for execution time)
        Math.Abs(limiter.NextResetDateTime.Ticks - expectedNextReset).Should().BeLessThan(TimeSpan.FromMilliseconds(500).Ticks);
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

        limiter.Period.Should().Be(TimeSpan.FromMilliseconds(100).Ticks);
    }

    #endregion UpdatePeriod
}

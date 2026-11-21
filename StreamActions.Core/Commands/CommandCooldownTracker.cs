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

using System.Collections.Concurrent;

namespace StreamActions.Core.Commands;

/// <summary>
/// Result of evaluating global and per-user cooldowns before executing a command.
/// </summary>
/// <param name="IsAllowed"><see langword="true"/> if neither global nor per-user cooldown is active; otherwise, <see langword="false"/>.</param>
/// <param name="IsGlobalCooldown"><see langword="true"/> if rejected due to an active global cooldown.</param>
/// <param name="Remaining">The remaining cooldown duration when <paramref name="IsAllowed"/> is <see langword="false"/>.</param>
public readonly record struct CooldownCheckResult(
    bool IsAllowed,
    bool IsGlobalCooldown,
    TimeSpan Remaining)
{
    /// <summary>
    /// Gets a result indicating no cooldown is currently active.
    /// </summary>
    public static CooldownCheckResult Allowed => new(true, false, TimeSpan.Zero);
}

/// <summary>
/// Captures prior cooldown timestamps before execution so they can be restored if execution cancels or fails.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier.</param>
/// <param name="BucketKey">The shared cooldown bucket key (keyed by command identity/primary trigger).</param>
/// <param name="PlatformId">The platform identifier.</param>
/// <param name="PlatformUserId">The platform user identifier.</param>
/// <param name="PreviousGlobalUtc">The prior global execution timestamp, if any.</param>
/// <param name="PreviousUserUtc">The prior per-user execution timestamp, if any.</param>
/// <param name="RecordedGlobal">Whether a global cooldown timestamp was updated.</param>
/// <param name="RecordedUser">Whether a per-user cooldown timestamp was updated.</param>
public sealed record CooldownSnapshot(
    Guid WorkspaceId,
    string BucketKey,
    string PlatformId,
    string PlatformUserId,
    DateTimeOffset? PreviousGlobalUtc,
    DateTimeOffset? PreviousUserUtc,
    bool RecordedGlobal,
    bool RecordedUser);

/// <summary>
/// Tracks global and per-user command cooldowns across both chat and whisper invocations using <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CommandCooldownTracker"/> class.
/// </remarks>
/// <param name="timeProvider">Optional <see cref="TimeProvider"/>; defaults to <see cref="TimeProvider.System"/>.</param>
public sealed class CommandCooldownTracker(TimeProvider? timeProvider = null)
{
    /// <summary>
    /// Time provider used for deterministic cooldown checks and recording.
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Last execution UTC timestamps keyed by <c>(WorkspaceId, BucketKey)</c>.
    /// </summary>
    private readonly ConcurrentDictionary<GlobalCooldownKey, DateTimeOffset> _globalCooldowns = new();

    /// <summary>
    /// Last execution UTC timestamps keyed by <c>(WorkspaceId, BucketKey, PlatformId, PlatformUserId)</c>.
    /// </summary>
    private readonly ConcurrentDictionary<UserCooldownKey, DateTimeOffset> _userCooldowns = new();

    /// <summary>
    /// Lock coordinating check-and-restore operations per bucket.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// Checks whether a command invocation is currently blocked by its global or per-user cooldown.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="bucketKey">The shared command bucket key (ensuring primary triggers and aliases share one bucket).</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The invoking user's platform user identifier.</param>
    /// <param name="globalCooldownSeconds">Configured global cooldown in seconds.</param>
    /// <param name="userCooldownSeconds">Configured per-user cooldown in seconds.</param>
    /// <returns>A <see cref="CooldownCheckResult"/> indicating whether execution is permitted.</returns>
    public CooldownCheckResult CheckCooldown(
        Guid workspaceId,
        string bucketKey,
        string platformId,
        string platformUserId,
        int globalCooldownSeconds,
        int userCooldownSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketKey);

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        lock (this._lock)
        {
            if (globalCooldownSeconds > 0)
            {
                GlobalCooldownKey globalKey = new(workspaceId, bucketKey);
                if (this._globalCooldowns.TryGetValue(globalKey, out DateTimeOffset lastGlobal))
                {
                    TimeSpan elapsed = now - lastGlobal;
                    TimeSpan required = TimeSpan.FromSeconds(globalCooldownSeconds);
                    if (elapsed < required)
                    {
                        return new CooldownCheckResult(false, true, required - elapsed);
                    }
                }
            }

            if (userCooldownSeconds > 0 && !string.IsNullOrWhiteSpace(platformUserId))
            {
                UserCooldownKey userKey = new(workspaceId, bucketKey, platformId, platformUserId);
                if (this._userCooldowns.TryGetValue(userKey, out DateTimeOffset lastUser))
                {
                    TimeSpan elapsed = now - lastUser;
                    TimeSpan required = TimeSpan.FromSeconds(userCooldownSeconds);
                    if (elapsed < required)
                    {
                        return new CooldownCheckResult(false, false, required - elapsed);
                    }
                }
            }
        }

        return CooldownCheckResult.Allowed;
    }

    /// <summary>
    /// Records the execution timestamp for global and/or per-user cooldowns and returns a <see cref="CooldownSnapshot"/> for rollback on cancel or error.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="bucketKey">The shared command bucket key.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The invoking user's platform user identifier.</param>
    /// <param name="globalCooldownSeconds">Configured global cooldown in seconds.</param>
    /// <param name="userCooldownSeconds">Configured per-user cooldown in seconds.</param>
    /// <returns>A <see cref="CooldownSnapshot"/> capturing prior timestamps.</returns>
    public CooldownSnapshot RecordExecution(
        Guid workspaceId,
        string bucketKey,
        string platformId,
        string platformUserId,
        int globalCooldownSeconds,
        int userCooldownSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketKey);

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset? prevGlobal = null;
        DateTimeOffset? prevUser = null;
        bool recordedGlobal = false;
        bool recordedUser = false;

        lock (this._lock)
        {
            if (globalCooldownSeconds > 0)
            {
                GlobalCooldownKey globalKey = new(workspaceId, bucketKey);
                if (this._globalCooldowns.TryGetValue(globalKey, out DateTimeOffset existingGlobal))
                {
                    prevGlobal = existingGlobal;
                }

                this._globalCooldowns[globalKey] = now;
                recordedGlobal = true;
            }

            if (userCooldownSeconds > 0 && !string.IsNullOrWhiteSpace(platformUserId))
            {
                UserCooldownKey userKey = new(workspaceId, bucketKey, platformId, platformUserId);
                if (this._userCooldowns.TryGetValue(userKey, out DateTimeOffset existingUser))
                {
                    prevUser = existingUser;
                }

                this._userCooldowns[userKey] = now;
                recordedUser = true;
            }
        }

        return new CooldownSnapshot(
            workspaceId,
            bucketKey,
            platformId,
            platformUserId,
            prevGlobal,
            prevUser,
            recordedGlobal,
            recordedUser);
    }

    /// <summary>
    /// Restores cooldown state to the timestamps captured in <paramref name="snapshot"/> when a command execution is canceled or throws.
    /// </summary>
    /// <param name="snapshot">The snapshot returned by <see cref="RecordExecution"/>.</param>
    public void RestoreCooldown(CooldownSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (this._lock)
        {
            if (snapshot.RecordedGlobal)
            {
                GlobalCooldownKey globalKey = new(snapshot.WorkspaceId, snapshot.BucketKey);
                if (snapshot.PreviousGlobalUtc is DateTimeOffset previous)
                {
                    this._globalCooldowns[globalKey] = previous;
                }
                else
                {
                    _ = this._globalCooldowns.TryRemove(globalKey, out _);
                }
            }

            if (snapshot.RecordedUser && !string.IsNullOrWhiteSpace(snapshot.PlatformUserId))
            {
                UserCooldownKey userKey = new(snapshot.WorkspaceId, snapshot.BucketKey, snapshot.PlatformId, snapshot.PlatformUserId);
                if (snapshot.PreviousUserUtc is DateTimeOffset previous)
                {
                    this._userCooldowns[userKey] = previous;
                }
                else
                {
                    _ = this._userCooldowns.TryRemove(userKey, out _);
                }
            }
        }
    }

    /// <summary>
    /// Case-insensitive composite key for global command cooldowns.
    /// </summary>
    /// <param name="WorkspaceId">The workspace identifier.</param>
    /// <param name="BucketKey">The command bucket key.</param>
    private readonly record struct GlobalCooldownKey(Guid WorkspaceId, string BucketKey) : IEquatable<GlobalCooldownKey>
    {
        /// <inheritdoc/>
        public bool Equals(GlobalCooldownKey other) =>
            this.WorkspaceId == other.WorkspaceId
            && string.Equals(this.BucketKey, other.BucketKey, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(this.WorkspaceId, StringComparer.OrdinalIgnoreCase.GetHashCode(this.BucketKey));
    }

    /// <summary>
    /// Case-insensitive composite key for per-user command cooldowns.
    /// </summary>
    /// <param name="WorkspaceId">The workspace identifier.</param>
    /// <param name="BucketKey">The command bucket key.</param>
    /// <param name="PlatformId">The platform identifier.</param>
    /// <param name="PlatformUserId">The platform user identifier.</param>
    private readonly record struct UserCooldownKey(
        Guid WorkspaceId,
        string BucketKey,
        string PlatformId,
        string PlatformUserId) : IEquatable<UserCooldownKey>
    {
        /// <inheritdoc/>
        public bool Equals(UserCooldownKey other) =>
            this.WorkspaceId == other.WorkspaceId
            && string.Equals(this.BucketKey, other.BucketKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.PlatformId, other.PlatformId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.PlatformUserId, other.PlatformUserId, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(
                this.WorkspaceId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.BucketKey),
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformUserId));
    }
}

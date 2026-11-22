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

using StreamActions.Core.Abstractions;

namespace StreamActions.Core.Templates;

/// <summary>
/// Optional strongly-typed service container passed directly to <see cref="TagEvaluationContext"/> when constructing contexts without a DI container.
/// </summary>
public sealed record TagEvaluationDependencies
{
    /// <summary>
    /// Gets the counter persistence store for counter tags.
    /// </summary>
    public ICounterStore? CounterStore { get; init; }

    /// <summary>
    /// Gets the localizer used for counter status prefixes.
    /// </summary>
    public IStreamActionsLocalizer? Localizer { get; init; }

    /// <summary>
    /// Gets the stream status provider used to query live game/category metadata.
    /// </summary>
    public IStreamStatusProvider? StreamStatusProvider { get; init; }

    /// <summary>
    /// Gets the workspace key-value store used by <c>{kv_*}</c> tags.
    /// </summary>
    public IWorkspaceKeyValueStore? KeyValueStore { get; init; }

    /// <summary>
    /// Gets the SSRF-hardened external HTTP client used by <c>{api}</c>.
    /// </summary>
    public ISafeExternalHttpClient? SafeExternalHttpClient { get; init; }

    /// <summary>
    /// Gets the command dispatcher used by <c>{botcmd}</c>.
    /// </summary>
    public ICommandDispatcher? CommandDispatcher { get; init; }

    /// <summary>
    /// Gets the command repository used by <c>{commands_list}</c>, <c>{price}</c>, and <c>{pay}</c>.
    /// </summary>
    public ICommandRepository? CommandRepository { get; init; }

    /// <summary>
    /// Gets the viewer loyalty store used by <c>{points}</c> and <c>{points_*}</c> tags.
    /// </summary>
    public IViewerLoyaltyStore? ViewerLoyaltyStore { get; init; }

    /// <summary>
    /// Gets the loyalty settings provider used by <c>{pointname}</c>.
    /// </summary>
    public ILoyaltySettingsProvider? LoyaltySettingsProvider { get; init; }

    /// <summary>
    /// Gets the loyalty rank provider used by <c>{sender_rank}</c> and <c>{rank}</c>.
    /// </summary>
    public ILoyaltyRankProvider? LoyaltyRankProvider { get; init; }

    /// <summary>
    /// Gets the channel chatter provider used by <c>{random_chatter}</c>.
    /// </summary>
    public IChannelChatterProvider? ChannelChatterProvider { get; init; }

    /// <summary>
    /// Gets the platform user lookup service used by <c>{account_age}</c>.
    /// </summary>
    public IPlatformUserLookup? PlatformUserLookup { get; init; }

    /// <summary>
    /// Gets the tip summary provider used by <c>{last_tip}</c>.
    /// </summary>
    public ITipProvider? TipProvider { get; init; }

    /// <summary>
    /// Gets the overlay alert dispatcher used by <c>{alert}</c>, <c>{alerttext}</c>, and <c>{playsound}</c>.
    /// </summary>
    public IOverlayAlertDispatcher? OverlayAlertDispatcher { get; init; }

    /// <summary>
    /// Gets the cross-platform message dispatcher used by <c>{delaysay}</c>, <c>{twitchsay}</c>, and <c>{discordsay}</c>.
    /// </summary>
    public ICrossPlatformMessageDispatcher? CrossPlatformMessageDispatcher { get; init; }

    /// <summary>
    /// Gets the delayed command scheduler used by <c>{delaycommand}</c>.
    /// </summary>
    public IDelayedCommandScheduler? DelayedCommandScheduler { get; init; }

    /// <summary>
    /// Gets the time provider used by time/countdown/countup tags and evaluation timeouts.
    /// </summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>
    /// Attempts to resolve a matching dependency instance for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The service contract type.</typeparam>
    /// <returns>The configured instance, or <see langword="null"/> if not supplied.</returns>
    internal T? TryResolve<T>() where T : class =>
        typeof(T) == typeof(ICounterStore) ? this.CounterStore as T
        : typeof(T) == typeof(IStreamActionsLocalizer) ? this.Localizer as T
        : typeof(T) == typeof(IStreamStatusProvider) ? this.StreamStatusProvider as T
        : typeof(T) == typeof(IWorkspaceKeyValueStore) ? this.KeyValueStore as T
        : typeof(T) == typeof(ISafeExternalHttpClient) ? this.SafeExternalHttpClient as T
        : typeof(T) == typeof(ICommandDispatcher) ? this.CommandDispatcher as T
        : typeof(T) == typeof(ICommandRepository) ? this.CommandRepository as T
        : typeof(T) == typeof(IViewerLoyaltyStore) ? this.ViewerLoyaltyStore as T
        : typeof(T) == typeof(ILoyaltySettingsProvider) ? this.LoyaltySettingsProvider as T
        : typeof(T) == typeof(ILoyaltyRankProvider) ? this.LoyaltyRankProvider as T
        : typeof(T) == typeof(IChannelChatterProvider) ? this.ChannelChatterProvider as T
        : typeof(T) == typeof(IPlatformUserLookup) ? this.PlatformUserLookup as T
        : typeof(T) == typeof(ITipProvider) ? this.TipProvider as T
        : typeof(T) == typeof(IOverlayAlertDispatcher) ? this.OverlayAlertDispatcher as T
        : typeof(T) == typeof(ICrossPlatformMessageDispatcher) ? this.CrossPlatformMessageDispatcher as T
        : typeof(T) == typeof(IDelayedCommandScheduler) ? this.DelayedCommandScheduler as T
        : typeof(T) == typeof(TimeProvider) ? this.TimeProvider as T
        : null;
}

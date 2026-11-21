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
    /// Gets the command repository used by <c>{commands_list}</c>.
    /// </summary>
    public ICommandRepository? CommandRepository { get; init; }

    /// <summary>
    /// Gets the time provider used by time/countdown/countup tags and evaluation timeouts.
    /// </summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>
    /// Attempts to resolve a matching dependency instance for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The service contract type.</typeparam>
    /// <returns>The configured instance, or <see langword="null"/> if not supplied.</returns>
    internal T? TryResolve<T>() where T : class
    {
        if (typeof(T) == typeof(ICounterStore))
        {
            return this.CounterStore as T;
        }

        if (typeof(T) == typeof(IStreamActionsLocalizer))
        {
            return this.Localizer as T;
        }

        if (typeof(T) == typeof(IStreamStatusProvider))
        {
            return this.StreamStatusProvider as T;
        }

        if (typeof(T) == typeof(IWorkspaceKeyValueStore))
        {
            return this.KeyValueStore as T;
        }

        if (typeof(T) == typeof(ISafeExternalHttpClient))
        {
            return this.SafeExternalHttpClient as T;
        }

        if (typeof(T) == typeof(ICommandDispatcher))
        {
            return this.CommandDispatcher as T;
        }

        if (typeof(T) == typeof(ICommandRepository))
        {
            return this.CommandRepository as T;
        }

        if (typeof(T) == typeof(TimeProvider))
        {
            return this.TimeProvider as T;
        }

        return null;
    }
}

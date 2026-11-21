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
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Globalization;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Shared helper methods for resolving counter scopes and localized counter mutation prefixes.
/// </summary>
internal static class CounterTagHelper
{
    /// <summary>
    /// Localization key for the counter increase prefix in the <c>core</c> module.
    /// </summary>
    internal const string IncreasedPrefixKey = "responses.counter.increased";

    /// <summary>
    /// Localization key for the counter decrease prefix in the <c>core</c> module.
    /// </summary>
    internal const string DecreasedPrefixKey = "responses.counter.decreased";

    /// <summary>
    /// Localization key for the counter set prefix in the <c>core</c> module.
    /// </summary>
    internal const string SetPrefixKey = "responses.counter.set";

    /// <summary>
    /// Fallback English prefix when no <see cref="IStreamActionsLocalizer"/> is registered.
    /// </summary>
    private const string _fallbackIncreasedPrefix = "[Increased] ";

    /// <summary>
    /// Fallback English prefix when no <see cref="IStreamActionsLocalizer"/> is registered.
    /// </summary>
    private const string _fallbackDecreasedPrefix = "[Decreased] ";

    /// <summary>
    /// Fallback English prefix when no <see cref="IStreamActionsLocalizer"/> is registered.
    /// </summary>
    private const string _fallbackSetPrefix = "[Set] ";

    /// <summary>
    /// Resolves the effective <see cref="CounterScopeMode"/> and game/category key for <paramref name="counterName"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter store.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple of <see cref="CounterScopeMode"/> and normalized game/category name.</returns>
    internal static async ValueTask<(CounterScopeMode ScopeMode, string GameCategory)> ResolveScopeAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        CancellationToken cancellationToken)
    {
        string? game = context.CurrentGameCategory;
        if (string.IsNullOrWhiteSpace(game))
        {
            IStreamStatusProvider? statusProvider = context.GetService<IStreamStatusProvider>();
            if (statusProvider is not null)
            {
                game = await statusProvider.GetCurrentGameCategoryAsync(context.WorkspaceId, context.PlatformId, cancellationToken).ConfigureAwait(false);
            }
        }

        string normalizedGame = string.IsNullOrWhiteSpace(game) ? string.Empty : game.Trim();

        if (!string.IsNullOrWhiteSpace(context.BoundCounterName)
            && string.Equals(counterName, context.BoundCounterName, StringComparison.OrdinalIgnoreCase)
            && context.BoundCounterScopeMode is CounterScopeMode explicitScope)
        {
            return explicitScope == CounterScopeMode.PerGame
                ? (CounterScopeMode.PerGame, normalizedGame)
                : (CounterScopeMode.Global, string.Empty);
        }

        IReadOnlyList<WorkspaceCounter> allCounters = await counterStore.GetAllAsync(context.WorkspaceId, cancellationToken).ConfigureAwait(false);
        bool hasPerGame = false;
        bool hasGlobal = false;

        foreach (WorkspaceCounter counter in allCounters)
        {
            if (!string.Equals(counter.CounterName, counterName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (counter.ScopeMode == CounterScopeMode.PerGame)
            {
                hasPerGame = true;
            }
            else
            {
                hasGlobal = true;
            }
        }

        if (hasPerGame)
        {
            return (CounterScopeMode.PerGame, normalizedGame);
        }

        if (hasGlobal)
        {
            return (CounterScopeMode.Global, string.Empty);
        }

        if (!string.IsNullOrEmpty(normalizedGame))
        {
            WorkspaceCounter? globalExisting = await counterStore.GetAsync(
                context.WorkspaceId,
                counterName,
                CounterScopeMode.Global,
                string.Empty,
                cancellationToken).ConfigureAwait(false);
            if (globalExisting is not null)
            {
                return (CounterScopeMode.Global, string.Empty);
            }

            WorkspaceCounter? perGameExisting = await counterStore.GetAsync(
                context.WorkspaceId,
                counterName,
                CounterScopeMode.PerGame,
                normalizedGame,
                cancellationToken).ConfigureAwait(false);
            if (perGameExisting is not null)
            {
                return (CounterScopeMode.PerGame, normalizedGame);
            }
        }

        return (CounterScopeMode.Global, string.Empty);
    }

    /// <summary>
    /// Resolves a localized counter prefix string from <see cref="IStreamActionsLocalizer"/>, falling back to the canonical <c>en-US</c> prefix.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="key">The localization key inside the <c>core</c> module.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The localized prefix string.</returns>
    internal static async ValueTask<string> GetLocalizedPrefixAsync(
        ITagEvaluationContext context,
        string key,
        CancellationToken cancellationToken)
    {
        IStreamActionsLocalizer? localizer = context.GetService<IStreamActionsLocalizer>();
        if (localizer is not null)
        {
            string? localized = await localizer.GetStringAsync(context.WorkspaceId, "core", key, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (localized is not null)
            {
                return localized;
            }
        }

        return key switch
        {
            IncreasedPrefixKey => _fallbackIncreasedPrefix,
            DecreasedPrefixKey => _fallbackDecreasedPrefix,
            SetPrefixKey => _fallbackSetPrefix,
            _ => string.Empty,
        };
    }
}

/// <summary>
/// Handles the <c>{count [-noprefix] [delta] [name]}</c> tag, supporting bound counter behaviors and inline counter adjustments.
/// </summary>
public sealed class CountTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "count";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ICounterStore? counterStore = context.GetService<ICounterStore>();
        if (counterStore is null)
        {
            return TagResult.Incomplete;
        }

        bool noPrefix = false;
        List<string> positionalArgs = [];

        foreach (TemplateNode argNode in unevaluatedArgs)
        {
            TagResult argResult = await argNode.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            if (string.Equals(argResult.Value.Trim(), "-noprefix", StringComparison.OrdinalIgnoreCase))
            {
                noPrefix = true;
            }
            else if (!string.IsNullOrWhiteSpace(argResult.Value))
            {
                positionalArgs.Add(argResult.Value.Trim());
            }
        }

        if (positionalArgs.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(context.BoundCounterName))
            {
                return TagResult.Incomplete;
            }

            return await EvaluateBoundCounterAsync(
                context,
                counterStore,
                context.BoundCounterName.Trim(),
                noPrefix,
                cancellationToken).ConfigureAwait(false);
        }

        if (positionalArgs.Count == 1)
        {
            string single = positionalArgs[0];
            if (TryParseInlineMutation(single, out InlineCounterOperation inlineOp))
            {
                if (string.IsNullOrWhiteSpace(context.BoundCounterName))
                {
                    return TagResult.Incomplete;
                }

                return await ExecuteInlineMutationAsync(
                    context,
                    counterStore,
                    context.BoundCounterName.Trim(),
                    inlineOp,
                    noPrefix,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(context.BoundCounterName)
                && string.Equals(single, context.BoundCounterName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return await EvaluateBoundCounterAsync(
                    context,
                    counterStore,
                    single,
                    noPrefix,
                    cancellationToken).ConfigureAwait(false);
            }

            return await ReadCounterAsync(context, counterStore, single, cancellationToken).ConfigureAwait(false);
        }

        string first = positionalArgs[0];
        string second = positionalArgs[1];

        if (TryParseInlineMutation(first, out InlineCounterOperation firstOp))
        {
            return await ExecuteInlineMutationAsync(
                context,
                counterStore,
                second,
                firstOp,
                noPrefix,
                cancellationToken).ConfigureAwait(false);
        }

        if (TryParseInlineMutation(second, out InlineCounterOperation secondOp))
        {
            return await ExecuteInlineMutationAsync(
                context,
                counterStore,
                first,
                secondOp,
                noPrefix,
                cancellationToken).ConfigureAwait(false);
        }

        return TagResult.Incomplete;
    }

    /// <summary>
    /// Evaluates a bound counter operation according to <see cref="ITagEvaluationContext.CounterUsageBehavior"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter persistence store.</param>
    /// <param name="counterName">The bound counter name.</param>
    /// <param name="noPrefix">Whether localized status prefixes are suppressed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    private static async ValueTask<TagResult> EvaluateBoundCounterAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        bool noPrefix,
        CancellationToken cancellationToken)
    {
        (CounterScopeMode scopeMode, string gameCategory) = await CounterTagHelper.ResolveScopeAsync(
            context,
            counterStore,
            counterName,
            cancellationToken).ConfigureAwait(false);

        switch (context.CounterUsageBehavior)
        {
            case CounterUsageBehavior.None:
            case CounterUsageBehavior.ReadOnly:
                return await ReadResolvedCounterAsync(context, counterStore, counterName, scopeMode, gameCategory, cancellationToken).ConfigureAwait(false);

            case CounterUsageBehavior.IncrementOnUse:
                return await AdjustResolvedCounterAsync(context, counterStore, counterName, 1, scopeMode, gameCategory, includePrefix: false, cancellationToken).ConfigureAwait(false);

            case CounterUsageBehavior.DecrementOnUse:
                return await AdjustResolvedCounterAsync(context, counterStore, counterName, -1, scopeMode, gameCategory, includePrefix: false, cancellationToken).ConfigureAwait(false);

            case CounterUsageBehavior.ResetToZero:
                return await SetResolvedCounterAsync(context, counterStore, counterName, 0, scopeMode, gameCategory, CounterMutationKind.Reset, includePrefix: false, cancellationToken).ConfigureAwait(false);

            case CounterUsageBehavior.SetFromArg1:
            {
                if (context.Arguments.Count > 0
                    && long.TryParse(context.Arguments[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long setValue))
                {
                    return await SetResolvedCounterAsync(context, counterStore, counterName, setValue, scopeMode, gameCategory, CounterMutationKind.Set, includePrefix: false, cancellationToken).ConfigureAwait(false);
                }

                return await ReadResolvedCounterAsync(context, counterStore, counterName, scopeMode, gameCategory, cancellationToken).ConfigureAwait(false);
            }

            case CounterUsageBehavior.Parameterized:
            {
                if (!context.CanMutateBoundCounter
                    || context.Arguments.Count == 0
                    || string.IsNullOrWhiteSpace(context.Arguments[0]))
                {
                    return await ReadResolvedCounterAsync(context, counterStore, counterName, scopeMode, gameCategory, cancellationToken).ConfigureAwait(false);
                }

                string arg0 = context.Arguments[0].Trim();
                if (string.Equals(arg0, "+", StringComparison.Ordinal))
                {
                    return await AdjustResolvedCounterAsync(
                        context,
                        counterStore,
                        counterName,
                        1,
                        scopeMode,
                        gameCategory,
                        includePrefix: !noPrefix,
                        cancellationToken).ConfigureAwait(false);
                }

                if (string.Equals(arg0, "-", StringComparison.Ordinal))
                {
                    return await AdjustResolvedCounterAsync(
                        context,
                        counterStore,
                        counterName,
                        -1,
                        scopeMode,
                        gameCategory,
                        includePrefix: !noPrefix,
                        cancellationToken).ConfigureAwait(false);
                }

                if (long.TryParse(arg0, NumberStyles.None, CultureInfo.InvariantCulture, out long targetValue))
                {
                    return await SetResolvedCounterAsync(
                        context,
                        counterStore,
                        counterName,
                        targetValue,
                        scopeMode,
                        gameCategory,
                        CounterMutationKind.Set,
                        includePrefix: !noPrefix,
                        cancellationToken).ConfigureAwait(false);
                }

                return await ReadResolvedCounterAsync(context, counterStore, counterName, scopeMode, gameCategory, cancellationToken).ConfigureAwait(false);
            }

            default:
                return await ReadResolvedCounterAsync(context, counterStore, counterName, scopeMode, gameCategory, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes an explicit inline mutation such as <c>{count "+1" "deaths"}</c>, <c>{count "-1" "deaths"}</c>, or <c>{count "=5" "deaths"}</c>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter persistence store.</param>
    /// <param name="counterName">The target counter name.</param>
    /// <param name="operation">The parsed inline counter operation.</param>
    /// <param name="noPrefix">Whether localized prefixes are suppressed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    private static async ValueTask<TagResult> ExecuteInlineMutationAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        InlineCounterOperation operation,
        bool noPrefix,
        CancellationToken cancellationToken)
    {
        (CounterScopeMode scopeMode, string gameCategory) = await CounterTagHelper.ResolveScopeAsync(
            context,
            counterStore,
            counterName,
            cancellationToken).ConfigureAwait(false);

        return operation.IsSet
            ? await SetResolvedCounterAsync(
                context,
                counterStore,
                counterName,
                operation.Value,
                scopeMode,
                gameCategory,
                CounterMutationKind.Set,
                includePrefix: !noPrefix,
                cancellationToken).ConfigureAwait(false)
            : await AdjustResolvedCounterAsync(
                context,
                counterStore,
                counterName,
                operation.Value,
                scopeMode,
                gameCategory,
                includePrefix: !noPrefix,
                cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a counter value without mutating storage.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter persistence store.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The formatted counter value.</returns>
    private static async ValueTask<TagResult> ReadCounterAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        CancellationToken cancellationToken)
    {
        (CounterScopeMode scopeMode, string gameCategory) = await CounterTagHelper.ResolveScopeAsync(
            context,
            counterStore,
            counterName,
            cancellationToken).ConfigureAwait(false);

        return await ReadResolvedCounterAsync(context, counterStore, counterName, scopeMode, gameCategory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a counter for a pre-resolved scope and records a <see cref="CounterMutationKind.Read"/> mutation entry.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter persistence store.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="scopeMode">The resolved counter scope mode.</param>
    /// <param name="gameCategory">The resolved game/category key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A completed <see cref="TagResult"/> containing the current value.</returns>
    private static async ValueTask<TagResult> ReadResolvedCounterAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        CounterScopeMode scopeMode,
        string gameCategory,
        CancellationToken cancellationToken)
    {
        WorkspaceCounter? existing = await counterStore.GetAsync(
            context.WorkspaceId,
            counterName,
            scopeMode,
            gameCategory,
            cancellationToken).ConfigureAwait(false);

        long currentValue = existing?.CurrentValue ?? 0L;
        context.RecordCounterMutation(new CounterMutationResult(
            counterName,
            scopeMode,
            gameCategory,
            existing?.CurrentValue,
            currentValue,
            CounterMutationKind.Read));

        return TagResult.Complete(currentValue.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Atomically adjusts a counter by <paramref name="delta"/> and optionally prepends a localized increased/decreased prefix.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter persistence store.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="delta">The signed delta.</param>
    /// <param name="scopeMode">The resolved counter scope mode.</param>
    /// <param name="gameCategory">The resolved game/category key.</param>
    /// <param name="includePrefix">Whether to prepend the localized mutation prefix.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A completed <see cref="TagResult"/> with the formatted result.</returns>
    private static async ValueTask<TagResult> AdjustResolvedCounterAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        long delta,
        CounterScopeMode scopeMode,
        string gameCategory,
        bool includePrefix,
        CancellationToken cancellationToken)
    {
        WorkspaceCounter updated = await counterStore.AdjustAsync(
            context.WorkspaceId,
            counterName,
            delta,
            scopeMode,
            gameCategory,
            cancellationToken).ConfigureAwait(false);

        CounterMutationKind kind = delta >= 0 ? CounterMutationKind.Incremented : CounterMutationKind.Decremented;
        context.RecordCounterMutation(new CounterMutationResult(
            counterName,
            scopeMode,
            gameCategory,
            updated.CurrentValue - delta,
            updated.CurrentValue,
            kind));

        string formattedValue = updated.CurrentValue.ToString(CultureInfo.InvariantCulture);
        if (!includePrefix || delta == 0)
        {
            return TagResult.Complete(formattedValue);
        }

        string prefixKey = delta > 0 ? CounterTagHelper.IncreasedPrefixKey : CounterTagHelper.DecreasedPrefixKey;
        string prefix = await CounterTagHelper.GetLocalizedPrefixAsync(context, prefixKey, cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(prefix + formattedValue);
    }

    /// <summary>
    /// Sets a counter to <paramref name="value"/> and optionally prepends the localized set prefix.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="counterStore">The counter persistence store.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="value">The new counter value.</param>
    /// <param name="scopeMode">The resolved counter scope mode.</param>
    /// <param name="gameCategory">The resolved game/category key.</param>
    /// <param name="kind">The mutation kind (<see cref="CounterMutationKind.Set"/> or <see cref="CounterMutationKind.Reset"/>).</param>
    /// <param name="includePrefix">Whether to prepend the localized set prefix.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A completed <see cref="TagResult"/> with the formatted result.</returns>
    private static async ValueTask<TagResult> SetResolvedCounterAsync(
        ITagEvaluationContext context,
        ICounterStore counterStore,
        string counterName,
        long value,
        CounterScopeMode scopeMode,
        string gameCategory,
        CounterMutationKind kind,
        bool includePrefix,
        CancellationToken cancellationToken)
    {
        WorkspaceCounter updated = await counterStore.SetAsync(
            context.WorkspaceId,
            counterName,
            value,
            scopeMode,
            gameCategory,
            cancellationToken).ConfigureAwait(false);

        context.RecordCounterMutation(new CounterMutationResult(
            counterName,
            scopeMode,
            gameCategory,
            null,
            updated.CurrentValue,
            kind));

        string formattedValue = updated.CurrentValue.ToString(CultureInfo.InvariantCulture);
        if (!includePrefix)
        {
            return TagResult.Complete(formattedValue);
        }

        string prefix = await CounterTagHelper.GetLocalizedPrefixAsync(context, CounterTagHelper.SetPrefixKey, cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(prefix + formattedValue);
    }

    /// <summary>
    /// Attempts to parse an inline mutation token (<c>+</c>, <c>-</c>, <c>+N</c>, <c>-N</c>, or <c>=N</c>).
    /// </summary>
    /// <param name="token">The argument token.</param>
    /// <param name="operation">When this method returns <see langword="true"/>, contains the parsed operation.</param>
    /// <returns><see langword="true"/> if <paramref name="token"/> is a valid inline counter operation; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseInlineMutation(string token, out InlineCounterOperation operation)
    {
        if (string.Equals(token, "+", StringComparison.Ordinal))
        {
            operation = new InlineCounterOperation(IsSet: false, Value: 1);
            return true;
        }

        if (string.Equals(token, "-", StringComparison.Ordinal))
        {
            operation = new InlineCounterOperation(IsSet: false, Value: -1);
            return true;
        }

        if (token.Length > 1 && token[0] == '+'
            && long.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out long plusDelta))
        {
            operation = new InlineCounterOperation(IsSet: false, Value: plusDelta);
            return true;
        }

        if (token.Length > 1 && token[0] == '-'
            && long.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out long minusDelta))
        {
            operation = new InlineCounterOperation(IsSet: false, Value: -minusDelta);
            return true;
        }

        if (token.Length > 1 && token[0] == '='
            && long.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out long setVal))
        {
            operation = new InlineCounterOperation(IsSet: true, Value: setVal);
            return true;
        }

        operation = default;
        return false;
    }

    /// <summary>
    /// Parsed inline counter delta or assignment operation.
    /// </summary>
    /// <param name="IsSet"><see langword="true"/> for an absolute <c>=N</c> assignment; <see langword="false"/> for a relative delta.</param>
    /// <param name="Value">The target value or signed delta.</param>
    private readonly record struct InlineCounterOperation(bool IsSet, long Value);
}

/// <summary>
/// Handles the <c>{counter_get &lt;name&gt; [game]}</c> tag, reading a counter's value without mutating it.
/// </summary>
public sealed class CounterGetTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "counter_get";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        ICounterStore? counterStore = context.GetService<ICounterStore>();
        if (counterStore is null)
        {
            return TagResult.Incomplete;
        }

        TagResult nameResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!nameResult.IsComplete || string.IsNullOrWhiteSpace(nameResult.Value))
        {
            return TagResult.Incomplete;
        }

        string counterName = nameResult.Value.Trim();
        CounterScopeMode scopeMode;
        string gameCategory;

        if (unevaluatedArgs.Count >= 2)
        {
            TagResult gameResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!gameResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            if (string.IsNullOrWhiteSpace(gameResult.Value))
            {
                scopeMode = CounterScopeMode.Global;
                gameCategory = string.Empty;
            }
            else
            {
                scopeMode = CounterScopeMode.PerGame;
                gameCategory = gameResult.Value.Trim();
            }
        }
        else
        {
            (scopeMode, gameCategory) = await CounterTagHelper.ResolveScopeAsync(
                context,
                counterStore,
                counterName,
                cancellationToken).ConfigureAwait(false);
        }

        WorkspaceCounter? existing = await counterStore.GetAsync(
            context.WorkspaceId,
            counterName,
            scopeMode,
            gameCategory,
            cancellationToken).ConfigureAwait(false);

        long value = existing?.CurrentValue ?? 0L;
        context.RecordCounterMutation(new CounterMutationResult(
            counterName,
            scopeMode,
            gameCategory,
            existing?.CurrentValue,
            value,
            CounterMutationKind.Read));

        return TagResult.Complete(value.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Handles the <c>{counter_total &lt;name&gt;}</c> tag, summing all global and per-game entries for a counter name.
/// </summary>
public sealed class CounterTotalTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "counter_total";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        ICounterStore? counterStore = context.GetService<ICounterStore>();
        if (counterStore is null)
        {
            return TagResult.Incomplete;
        }

        TagResult nameResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!nameResult.IsComplete || string.IsNullOrWhiteSpace(nameResult.Value))
        {
            return TagResult.Incomplete;
        }

        string counterName = nameResult.Value.Trim();
        IReadOnlyList<WorkspaceCounter> counters = await counterStore.GetAllAsync(context.WorkspaceId, cancellationToken).ConfigureAwait(false);

        long total = 0L;
        foreach (WorkspaceCounter counter in counters)
        {
            if (string.Equals(counter.CounterName, counterName, StringComparison.OrdinalIgnoreCase))
            {
                total += counter.CurrentValue;
            }
        }

        return TagResult.Complete(total.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Handles the <c>{count_reset [name]}</c> tag, resetting the target or bound counter to zero.
/// </summary>
public sealed class CountResetTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "count_reset";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ICounterStore? counterStore = context.GetService<ICounterStore>();
        if (counterStore is null)
        {
            return TagResult.Incomplete;
        }

        string? counterName = context.BoundCounterName;
        if (unevaluatedArgs.Count >= 1)
        {
            TagResult nameResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!nameResult.IsComplete || string.IsNullOrWhiteSpace(nameResult.Value))
            {
                return TagResult.Incomplete;
            }

            counterName = nameResult.Value.Trim();
        }

        if (string.IsNullOrWhiteSpace(counterName))
        {
            return TagResult.Incomplete;
        }

        string trimmedName = counterName.Trim();
        (CounterScopeMode scopeMode, string gameCategory) = await CounterTagHelper.ResolveScopeAsync(
            context,
            counterStore,
            trimmedName,
            cancellationToken).ConfigureAwait(false);

        WorkspaceCounter updated = await counterStore.SetAsync(
            context.WorkspaceId,
            trimmedName,
            0L,
            scopeMode,
            gameCategory,
            cancellationToken).ConfigureAwait(false);

        context.RecordCounterMutation(new CounterMutationResult(
            trimmedName,
            scopeMode,
            gameCategory,
            null,
            updated.CurrentValue,
            CounterMutationKind.Reset));

        return TagResult.Complete(updated.CurrentValue.ToString(CultureInfo.InvariantCulture));
    }
}

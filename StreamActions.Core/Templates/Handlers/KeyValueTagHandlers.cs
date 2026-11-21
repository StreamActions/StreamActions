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
using System.Security.Cryptography;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{kv_get &lt;table&gt; &lt;section&gt; &lt;key&gt; [default]}</c> tag using <see cref="IWorkspaceKeyValueStore"/>.
/// </summary>
public sealed class KvGetTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "kv_get";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count < 3)
        {
            return TagResult.Incomplete;
        }

        IWorkspaceKeyValueStore? kvStore = context.GetService<IWorkspaceKeyValueStore>();
        if (kvStore is null)
        {
            return TagResult.Incomplete;
        }

        TagResult tableResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!tableResult.IsComplete || string.IsNullOrWhiteSpace(tableResult.Value))
        {
            return TagResult.Incomplete;
        }

        TagResult sectionResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!sectionResult.IsComplete || string.IsNullOrWhiteSpace(sectionResult.Value))
        {
            return TagResult.Incomplete;
        }

        TagResult keyResult = await unevaluatedArgs[2].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!keyResult.IsComplete || string.IsNullOrWhiteSpace(keyResult.Value))
        {
            return TagResult.Incomplete;
        }

        string? defaultValue = null;
        bool hasExplicitDefault = false;
        if (unevaluatedArgs.Count >= 4)
        {
            TagResult defaultResult = await unevaluatedArgs[3].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!defaultResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            defaultValue = defaultResult.Value;
            hasExplicitDefault = true;
        }

        string? stored = await kvStore.GetStringAsync(
            context.WorkspaceId,
            tableResult.Value.Trim(),
            sectionResult.Value.Trim(),
            keyResult.Value.Trim(),
            defaultValue,
            cancellationToken).ConfigureAwait(false);

        if (stored is not null)
        {
            return TagResult.Complete(stored);
        }

        return hasExplicitDefault
            ? TagResult.Complete(defaultValue ?? string.Empty)
            : TagResult.Incomplete;
    }
}

/// <summary>
/// Handles the <c>{kv_set &lt;table&gt; &lt;section&gt; &lt;key&gt; &lt;val&gt;}</c> tag using <see cref="IWorkspaceKeyValueStore"/>.
/// </summary>
public sealed class KvSetTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "kv_set";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count < 4)
        {
            return TagResult.Incomplete;
        }

        IWorkspaceKeyValueStore? kvStore = context.GetService<IWorkspaceKeyValueStore>();
        if (kvStore is null)
        {
            return TagResult.Incomplete;
        }

        TagResult tableResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!tableResult.IsComplete || string.IsNullOrWhiteSpace(tableResult.Value))
        {
            return TagResult.Incomplete;
        }

        TagResult sectionResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!sectionResult.IsComplete || string.IsNullOrWhiteSpace(sectionResult.Value))
        {
            return TagResult.Incomplete;
        }

        TagResult keyResult = await unevaluatedArgs[2].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!keyResult.IsComplete || string.IsNullOrWhiteSpace(keyResult.Value))
        {
            return TagResult.Incomplete;
        }

        TagResult valResult = await unevaluatedArgs[3].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!valResult.IsComplete)
        {
            return TagResult.Incomplete;
        }

        await kvStore.SetAsync(
            context.WorkspaceId,
            tableResult.Value.Trim(),
            sectionResult.Value.Trim(),
            keyResult.Value.Trim(),
            valResult.Value,
            cancellationToken).ConfigureAwait(false);

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{kv_rand &lt;table&gt; [section]}</c> tag, picking a random entry from <see cref="IWorkspaceKeyValueStore"/>.
/// </summary>
public sealed class KvRandTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Default section name used when <c>[section]</c> is omitted.
    /// </summary>
    public const string DefaultSection = "default";

    /// <inheritdoc />
    public string TagName => "kv_rand";

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

        IWorkspaceKeyValueStore? kvStore = context.GetService<IWorkspaceKeyValueStore>();
        if (kvStore is null)
        {
            return TagResult.Incomplete;
        }

        TagResult tableResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!tableResult.IsComplete || string.IsNullOrWhiteSpace(tableResult.Value))
        {
            return TagResult.Incomplete;
        }

        string section = DefaultSection;
        if (unevaluatedArgs.Count >= 2)
        {
            TagResult sectionResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!sectionResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            if (!string.IsNullOrWhiteSpace(sectionResult.Value))
            {
                section = sectionResult.Value.Trim();
            }
        }

        string table = tableResult.Value.Trim();
        IReadOnlyList<string> keys = await kvStore.GetKeysAsync(context.WorkspaceId, table, section, cancellationToken).ConfigureAwait(false);
        if (keys.Count == 0)
        {
            return TagResult.Incomplete;
        }

        string chosenKey = keys[RandomNumberGenerator.GetInt32(keys.Count)];
        string? value = await kvStore.GetStringAsync(context.WorkspaceId, table, section, chosenKey, null, cancellationToken).ConfigureAwait(false);
        return TagResult.FromNonEmpty(value);
    }
}

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

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{json_get &lt;jsonOrVarName&gt; &lt;path&gt; [defaultValue]}</c> tag for extracting values from JSON payloads or routine variables.
/// </summary>
public sealed class JsonGetTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "json_get";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count < 2)
        {
            return TagResult.Incomplete;
        }

        TagResult sourceResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsComplete || context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        TagResult pathResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!pathResult.IsComplete || context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        string rawSource = sourceResult.Value;
        string trimmedSource = rawSource.Trim();
        string jsonPayload = !trimmedSource.StartsWith('{')
            && !trimmedSource.StartsWith('[')
            && context.TryGetVariable(trimmedSource, out string? variablePayload)
            ? variablePayload
            : rawSource;

        return ApiTagHandler.TryExtractJsonPath(jsonPayload, pathResult.Value, out string? extracted)
            ? TagResult.Complete(extracted ?? string.Empty)
            : unevaluatedArgs.Count >= 3
            ? await unevaluatedArgs[2].EvaluateAsync(context, cancellationToken).ConfigureAwait(false)
            : TagResult.Incomplete;
    }
}

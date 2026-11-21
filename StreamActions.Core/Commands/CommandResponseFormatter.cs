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
using System.Globalization;
using System.Text;

namespace StreamActions.Core.Commands;

/// <summary>
/// Resolves localized bot response templates from <see cref="IStreamActionsLocalizer"/> and formats placeholders in a single injection-safe pass.
/// </summary>
internal static class CommandResponseFormatter
{
    /// <summary>
    /// Resolves and formats a localized response template from the <c>core</c> module, falling back to <paramref name="fallbackTemplate"/> when unavailable.
    /// </summary>
    /// <param name="localizer">Optional localizer instance.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="responseKey">The response key (without or with <c>responses.</c> prefix).</param>
    /// <param name="fallbackTemplate">The canonical <c>en-US</c> fallback template.</param>
    /// <param name="senderDisplayName">The sender display name for <c>{sender}</c>.</param>
    /// <param name="positionalArgs">Positional placeholder values for <c>{1}</c>, <c>{2}</c>, and so on.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The formatted response message.</returns>
    internal static async ValueTask<string> FormatCoreResponseAsync(
        IStreamActionsLocalizer? localizer,
        Guid workspaceId,
        string responseKey,
        string fallbackTemplate,
        string senderDisplayName,
        IReadOnlyList<string> positionalArgs,
        CancellationToken cancellationToken = default)
    {
        string? template = null;
        if (localizer is not null)
        {
            template = await localizer
                .GetResponseTemplateAsync(workspaceId, "core", responseKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return FormatTemplate(template ?? fallbackTemplate, senderDisplayName, positionalArgs);
    }

    /// <summary>
    /// Replaces <c>{sender}</c> and 1-based positional <c>{N}</c> tokens in <paramref name="template"/> in a single left-to-right pass.
    /// </summary>
    /// <param name="template">The response template containing placeholders.</param>
    /// <param name="senderDisplayName">The sender display name.</param>
    /// <param name="positionalArgs">The positional argument values.</param>
    /// <returns>The formatted response string.</returns>
    internal static string FormatTemplate(
        string template,
        string senderDisplayName,
        IReadOnlyList<string> positionalArgs)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        StringBuilder result = new(template.Length + 32);
        int index = 0;

        while (index < template.Length)
        {
            char c = template[index];
            if (c == '{')
            {
                int closeIndex = template.IndexOf('}', index + 1);
                if (closeIndex > index + 1)
                {
                    string token = template[(index + 1)..closeIndex];
                    if (string.Equals(token, "sender", StringComparison.OrdinalIgnoreCase))
                    {
                        _ = result.Append(senderDisplayName);
                        index = closeIndex + 1;
                        continue;
                    }

                    if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int position)
                        && position >= 1
                        && position <= positionalArgs.Count)
                    {
                        _ = result.Append(positionalArgs[position - 1]);
                        index = closeIndex + 1;
                        continue;
                    }
                }
            }

            _ = result.Append(c);
            index++;
        }

        return result.ToString();
    }
}

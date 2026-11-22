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

using System.Text.RegularExpressions;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{sender}</c> tag, returning the invoking user's display name (falling back to username).
/// </summary>
public sealed class SenderTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "sender";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string value = !string.IsNullOrWhiteSpace(context.Message.Sender.DisplayName)
            ? context.Message.Sender.DisplayName
            : context.Message.Sender.Username;

        return ValueTask.FromResult(TagResult.FromNonEmpty(value));
    }
}

/// <summary>
/// Handles the <c>{@sender}</c> tag, returning a platform-appropriate mention for the invoking user (<c>&lt;@userId&gt;</c> on Discord or <c>@DisplayName</c> elsewhere).
/// </summary>
public sealed class AtSenderTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "@sender";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(context.PlatformId, "discord", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(context.Message.Sender.PlatformUserId))
        {
            return ValueTask.FromResult(TagResult.Complete($"<@{context.Message.Sender.PlatformUserId.Trim()}>"));
        }

        string raw = !string.IsNullOrWhiteSpace(context.Message.Sender.DisplayName)
            ? context.Message.Sender.DisplayName
            : context.Message.Sender.Username;
        string cleaned = raw.Trim().TrimStart('@');

        return ValueTask.FromResult(string.IsNullOrEmpty(cleaned)
            ? TagResult.Incomplete
            : TagResult.Complete($"@{cleaned}"));
    }
}

/// <summary>
/// Handles the <c>{bare_sender}</c> tag, returning the invoking user's login username without any leading <c>@</c>.
/// </summary>
public sealed class BareSenderTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "bare_sender";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string raw = !string.IsNullOrWhiteSpace(context.Message.Sender.Username)
            ? context.Message.Sender.Username
            : context.Message.Sender.DisplayName;
        string cleaned = raw.Trim().TrimStart('@');

        return ValueTask.FromResult(TagResult.FromNonEmpty(cleaned));
    }
}

/// <summary>
/// Handles the <c>{sender_id}</c> tag, returning the invoking user's platform user identifier.
/// </summary>
public sealed class SenderIdTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "sender_id";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.Message.Sender.PlatformUserId));
    }
}

/// <summary>
/// Handles the <c>{channel}</c> tag, returning the channel name associated with the message.
/// </summary>
public sealed class ChannelTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "channel";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.FromNonEmpty(context.ChannelName));
    }
}

/// <summary>
/// Handles the <c>{target}</c> tag, returning the cleaned first command argument if provided, otherwise falling back to the sender name.
/// </summary>
public sealed class TargetTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "target";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count > 0 && !string.IsNullOrWhiteSpace(context.Arguments[0]))
        {
            string cleanedArg = context.Arguments[0].Trim().TrimStart('@');
            if (!string.IsNullOrEmpty(cleanedArg))
            {
                return ValueTask.FromResult(TagResult.Complete(cleanedArg));
            }
        }

        string sender = !string.IsNullOrWhiteSpace(context.Message.Sender.DisplayName)
            ? context.Message.Sender.DisplayName
            : context.Message.Sender.Username;

        return ValueTask.FromResult(TagResult.FromNonEmpty(sender.Trim().TrimStart('@')));
    }
}

/// <summary>
/// Handles the <c>{clean_user &lt;str&gt;}</c> tag, stripping whitespace, leading <c>@</c> characters, and disallowed username characters.
/// </summary>
public sealed partial class CleanUserTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Matches any character outside the allowed username character set (<c>A-Z</c>, <c>a-z</c>, <c>0-9</c>, <c>_</c>, <c>.</c>, <c>-</c>).
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"[^A-Za-z0-9_.\-]")]
    private static partial Regex DisallowedUsernameCharsRegex();

    /// <inheritdoc />
    public string TagName => "clean_user";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        TagResult argResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!argResult.IsComplete)
        {
            return TagResult.Incomplete;
        }

        string trimmed = argResult.Value.Trim().TrimStart('@');
        string cleaned = DisallowedUsernameCharsRegex().Replace(trimmed, string.Empty);
        return TagResult.FromNonEmpty(cleaned);
    }
}

/// <summary>
/// Handles the <c>{point_to_user}</c> tag, formatting <c>{target} -&gt; </c> for directed replies.
/// </summary>
public sealed class PointToUserTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "point_to_user";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string? candidate = null;
        if (context.Arguments.Count > 0 && !string.IsNullOrWhiteSpace(context.Arguments[0]))
        {
            candidate = context.Arguments[0].Trim().TrimStart('@');
        }

        if (string.IsNullOrEmpty(candidate))
        {
            string sender = !string.IsNullOrWhiteSpace(context.Message.Sender.DisplayName)
                ? context.Message.Sender.DisplayName
                : context.Message.Sender.Username;
            candidate = sender.Trim().TrimStart('@');
        }

        return ValueTask.FromResult(string.IsNullOrEmpty(candidate)
            ? TagResult.Incomplete
            : TagResult.Complete($"{candidate} -> "));
    }
}

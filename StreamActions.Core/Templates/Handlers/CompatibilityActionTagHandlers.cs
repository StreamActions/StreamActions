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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Shared sanitization and argument-parsing helpers for Section 3.8 compatibility action tags.
/// </summary>
internal static partial class CompatibilityActionTagHelper
{
    /// <summary>
    /// Maximum allowed delay duration in seconds for <c>{delaycommand}</c> and <c>{delaysay}</c>.
    /// </summary>
    internal const double _maxDelaySeconds = 86_400d;

    /// <summary>
    /// Default alert display duration in seconds when omitted.
    /// </summary>
    internal const double _defaultAlertDurationSeconds = 5d;

    /// <summary>
    /// Default alert and soundboard playback volume when omitted.
    /// </summary>
    internal const double _defaultVolume = 1d;

    /// <summary>
    /// Matches disallowed CSS and HTML/script breakout patterns (<c>&lt;/style&gt;</c>, <c>@import</c>, <c>javascript:</c>, and <c>&lt;script</c>).
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/>.</returns>
    [GeneratedRegex(@"</\s*style|@\s*import|javascript\s*:|<\s*script", RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeCssOrHtmlRegex();

    /// <summary>
    /// Determines whether <paramref name="value"/> contains disallowed CSS or XSS/HTML injection payloads.
    /// </summary>
    /// <param name="value">The CSS or overlay message string to inspect.</param>
    /// <returns><see langword="true"/> if an unsafe pattern is detected; otherwise, <see langword="false"/>.</returns>
    internal static bool ContainsUnsafeCssOrHtml(string? value) =>
        !string.IsNullOrWhiteSpace(value) && UnsafeCssOrHtmlRegex().IsMatch(value);

    /// <summary>
    /// Evaluates a slice of argument AST nodes and joins them with single spaces.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The argument AST node list.</param>
    /// <param name="startIndex">The starting index into <paramref name="unevaluatedArgs"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The joined message text, or <see langword="null"/> if any argument is incomplete or the joined message is blank.</returns>
    internal static async ValueTask<string?> EvaluateJoinedMessageAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        int startIndex,
        CancellationToken cancellationToken)
    {
        if (startIndex >= unevaluatedArgs.Count)
        {
            return null;
        }

        List<string> parts = new(unevaluatedArgs.Count - startIndex);
        for (int i = startIndex; i < unevaluatedArgs.Count; i++)
        {
            TagResult argResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete)
            {
                return null;
            }

            parts.Add(argResult.Value);
        }

        string joined = string.Join(' ', parts).Trim();
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    /// <summary>
    /// Parses a finite floating-point value within an inclusive range using <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <param name="raw">The raw string value.</param>
    /// <param name="minInclusive">The minimum permitted value.</param>
    /// <param name="maxInclusive">The maximum permitted value.</param>
    /// <param name="parsed">When this method returns <see langword="true"/>, contains the parsed value.</param>
    /// <returns><see langword="true"/> if valid and within bounds; otherwise, <see langword="false"/>.</returns>
    internal static bool TryParseBoundedFiniteDouble(
        string raw,
        double minInclusive,
        double maxInclusive,
        out double parsed) => double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && double.IsFinite(parsed) && parsed >= minInclusive && parsed <= maxInclusive;
}

/// <summary>
/// Handles the <c>{delaycommand &lt;sec&gt; &lt;name&gt; [args...]}</c> tag, scheduling a delayed command via <see cref="IDelayedCommandScheduler"/> while enforcing chain depth, cycle detection, and the per-evaluation delayed action cap.
/// </summary>
public sealed class DelayCommandTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "delaycommand";

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command triggers to lowercase invariant.")]
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

        IDelayedCommandScheduler? scheduler = context.GetService<IDelayedCommandScheduler>();
        if (scheduler is null || context.ChainDepth >= TemplateExecutionLimits.MaxCommandChainDepth)
        {
            return TagResult.Incomplete;
        }

        TagResult delayResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!delayResult.IsComplete
            || !CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(
                delayResult.Value,
                0d,
                CompatibilityActionTagHelper._maxDelaySeconds,
                out double seconds))
        {
            return TagResult.Incomplete;
        }

        TagResult nameResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!nameResult.IsComplete)
        {
            return TagResult.Incomplete;
        }

        string normalizedTrigger = nameResult.Value.Trim().TrimStart('!').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedTrigger) || context.VisitedCommandTriggers.Contains(normalizedTrigger))
        {
            return TagResult.Incomplete;
        }

        List<string> childArgs = new(Math.Max(0, unevaluatedArgs.Count - 2));
        for (int i = 2; i < unevaluatedArgs.Count; i++)
        {
            TagResult argResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            childArgs.Add(argResult.Value);
        }

        if (!context.TryReserveDelayedAction())
        {
            return TagResult.Incomplete;
        }

        string prefix = (context.Message as ICommandInvocation)?.Prefix ?? "!";
        string rawArgs = string.Join(' ', childArgs);
        string rawText = string.IsNullOrEmpty(rawArgs)
            ? $"{prefix}{normalizedTrigger}"
            : $"{prefix}{normalizedTrigger} {rawArgs}";

        int nextDepth = context.ChainDepth + 1;
        HashSet<string> nextVisited = new(context.VisitedCommandTriggers, StringComparer.OrdinalIgnoreCase)
        {
            normalizedTrigger,
        };

        ICommandInvocation childInvocation = BotCmdTagHandler.BuildChildInvocation(
            context,
            prefix,
            normalizedTrigger,
            childArgs.AsReadOnly(),
            rawArgs,
            rawText,
            nextDepth,
            nextVisited);

        await scheduler.ScheduleCommandAsync(childInvocation, TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{delaysay &lt;sec&gt; &lt;msg&gt;}</c> tag, scheduling a delayed chat message via <see cref="ICrossPlatformMessageDispatcher"/> while enforcing the per-evaluation delayed action cap.
/// </summary>
public sealed class DelaySayTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "delaysay";

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

        ICrossPlatformMessageDispatcher? dispatcher = context.GetService<ICrossPlatformMessageDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        TagResult delayResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!delayResult.IsComplete
            || !CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(
                delayResult.Value,
                0d,
                CompatibilityActionTagHelper._maxDelaySeconds,
                out double seconds))
        {
            return TagResult.Incomplete;
        }

        string? message = await CompatibilityActionTagHelper
            .EvaluateJoinedMessageAsync(context, unevaluatedArgs, 1, cancellationToken)
            .ConfigureAwait(false);
        if (message is null)
        {
            return TagResult.Incomplete;
        }

        if (!context.TryReserveDelayedAction())
        {
            return TagResult.Incomplete;
        }

        await dispatcher.ScheduleMessageAsync(
            context.WorkspaceId,
            context.PlatformId,
            context.ChannelName,
            TimeSpan.FromSeconds(seconds),
            message,
            cancellationToken).ConfigureAwait(false);

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{twitchsay &lt;msg&gt;}</c> tag, sending an immediate message to Twitch via <see cref="ICrossPlatformMessageDispatcher"/>.
/// </summary>
public sealed class TwitchSayTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "twitchsay";

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

        ICrossPlatformMessageDispatcher? dispatcher = context.GetService<ICrossPlatformMessageDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        string? message = await CompatibilityActionTagHelper
            .EvaluateJoinedMessageAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        if (message is null)
        {
            return TagResult.Incomplete;
        }

        string? targetChannel = string.IsNullOrWhiteSpace(context.ChannelName) ? null : context.ChannelName;
        await dispatcher.SendCrossPlatformMessageAsync(
            context.WorkspaceId,
            "twitch",
            targetChannel,
            message,
            cancellationToken).ConfigureAwait(false);

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{discordsay &lt;chan&gt; &lt;msg&gt;}</c> tag, sending an immediate message to a Discord channel via <see cref="ICrossPlatformMessageDispatcher"/>.
/// </summary>
public sealed class DiscordSayTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "discordsay";

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

        ICrossPlatformMessageDispatcher? dispatcher = context.GetService<ICrossPlatformMessageDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        TagResult channelResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!channelResult.IsComplete || string.IsNullOrWhiteSpace(channelResult.Value))
        {
            return TagResult.Incomplete;
        }

        string? message = await CompatibilityActionTagHelper
            .EvaluateJoinedMessageAsync(context, unevaluatedArgs, 1, cancellationToken)
            .ConfigureAwait(false);
        if (message is null)
        {
            return TagResult.Incomplete;
        }

        await dispatcher.SendCrossPlatformMessageAsync(
            context.WorkspaceId,
            "discord",
            channelResult.Value.Trim(),
            message,
            cancellationToken).ConfigureAwait(false);

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{alert &lt;file&gt; [sec [vol [css [msg]]]]}</c> tag, dispatching a sanitized media alert via <see cref="IOverlayAlertDispatcher"/>.
/// </summary>
public sealed class AlertTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "alert";

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

        IOverlayAlertDispatcher? dispatcher = context.GetService<IOverlayAlertDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        TagResult fileResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!fileResult.IsComplete || string.IsNullOrWhiteSpace(fileResult.Value))
        {
            return TagResult.Incomplete;
        }

        string file = fileResult.Value.Trim();
        if (CompatibilityActionTagHelper.ContainsUnsafeCssOrHtml(file))
        {
            return TagResult.Incomplete;
        }

        double durationSeconds = CompatibilityActionTagHelper._defaultAlertDurationSeconds;
        if (unevaluatedArgs.Count >= 2)
        {
            TagResult secResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!secResult.IsComplete
                || !CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(secResult.Value, 0.001d, 3600d, out durationSeconds))
            {
                return TagResult.Incomplete;
            }
        }

        double volume = CompatibilityActionTagHelper._defaultVolume;
        if (unevaluatedArgs.Count >= 3)
        {
            TagResult volResult = await unevaluatedArgs[2].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!volResult.IsComplete
                || !CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(volResult.Value, 0d, 100d, out volume))
            {
                return TagResult.Incomplete;
            }
        }

        string? css = null;
        if (unevaluatedArgs.Count >= 4)
        {
            TagResult cssResult = await unevaluatedArgs[3].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!cssResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            css = string.IsNullOrWhiteSpace(cssResult.Value) ? null : cssResult.Value;
            if (CompatibilityActionTagHelper.ContainsUnsafeCssOrHtml(css))
            {
                return TagResult.Incomplete;
            }
        }

        string? message = null;
        if (unevaluatedArgs.Count >= 5)
        {
            TagResult msgResult = await unevaluatedArgs[4].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!msgResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            message = string.IsNullOrWhiteSpace(msgResult.Value) ? null : msgResult.Value;
            if (CompatibilityActionTagHelper.ContainsUnsafeCssOrHtml(message))
            {
                return TagResult.Incomplete;
            }
        }

        await dispatcher.DispatchAlertAsync(
            context.WorkspaceId,
            file,
            durationSeconds,
            volume,
            css,
            message,
            cancellationToken).ConfigureAwait(false);

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{alerttext [-d sec] [-c css] &lt;msg&gt;}</c> tag, dispatching a sanitized text alert via <see cref="IOverlayAlertDispatcher"/>.
/// </summary>
public sealed class AlertTextTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "alerttext";

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

        IOverlayAlertDispatcher? dispatcher = context.GetService<IOverlayAlertDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        double durationSeconds = CompatibilityActionTagHelper._defaultAlertDurationSeconds;
        string? css = null;
        List<string> messageParts = [];

        for (int i = 0; i < unevaluatedArgs.Count; i++)
        {
            TagResult argResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            string token = argResult.Value.Trim();
            if (string.Equals(token, "-d", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= unevaluatedArgs.Count)
                {
                    return TagResult.Incomplete;
                }

                i++;
                TagResult secResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
                if (!secResult.IsComplete
                    || !CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(secResult.Value, 0.001d, 3600d, out durationSeconds))
                {
                    return TagResult.Incomplete;
                }

                continue;
            }

            if (string.Equals(token, "-c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= unevaluatedArgs.Count)
                {
                    return TagResult.Incomplete;
                }

                i++;
                TagResult cssResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
                if (!cssResult.IsComplete)
                {
                    return TagResult.Incomplete;
                }

                css = string.IsNullOrWhiteSpace(cssResult.Value) ? null : cssResult.Value;
                continue;
            }

            messageParts.Add(argResult.Value);
        }

        string message = string.Join(' ', messageParts).Trim();
        if (string.IsNullOrWhiteSpace(message)
            || CompatibilityActionTagHelper.ContainsUnsafeCssOrHtml(css)
            || CompatibilityActionTagHelper.ContainsUnsafeCssOrHtml(message))
        {
            return TagResult.Incomplete;
        }

        await dispatcher.DispatchAlertTextAsync(
            context.WorkspaceId,
            message,
            durationSeconds,
            css,
            cancellationToken).ConfigureAwait(false);

        return TagResult.Complete(string.Empty);
    }
}

/// <summary>
/// Handles the <c>{playsound &lt;hook&gt;}</c> tag, triggering a registered soundboard hook via <see cref="IOverlayAlertDispatcher"/>.
/// </summary>
public sealed class PlaySoundTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "playsound";

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

        IOverlayAlertDispatcher? dispatcher = context.GetService<IOverlayAlertDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        TagResult hookResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!hookResult.IsComplete || string.IsNullOrWhiteSpace(hookResult.Value))
        {
            return TagResult.Incomplete;
        }

        string rawHook = hookResult.Value.Trim();
        string hook = rawHook;
        double volume = CompatibilityActionTagHelper._defaultVolume;

        int pipeIndex = rawHook.IndexOf('|', StringComparison.Ordinal);
        if (pipeIndex >= 0)
        {
            hook = rawHook[..pipeIndex].Trim();
            string rawVol = rawHook[(pipeIndex + 1)..];
            if (!CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(rawVol, 0d, 100d, out volume))
            {
                return TagResult.Incomplete;
            }
        }
        else if (unevaluatedArgs.Count >= 2)
        {
            TagResult volResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!volResult.IsComplete
                || !CompatibilityActionTagHelper.TryParseBoundedFiniteDouble(volResult.Value, 0d, 100d, out volume))
            {
                return TagResult.Incomplete;
            }
        }

        if (string.IsNullOrWhiteSpace(hook) || CompatibilityActionTagHelper.ContainsUnsafeCssOrHtml(hook))
        {
            return TagResult.Incomplete;
        }

        await dispatcher.DispatchSoundAsync(context.WorkspaceId, hook, volume, cancellationToken).ConfigureAwait(false);
        return TagResult.Complete(string.Empty);
    }
}

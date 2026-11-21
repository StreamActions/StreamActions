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
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{botcmd &lt;name&gt; [args...]}</c> tag, dispatching a nested command through <see cref="ICommandDispatcher"/> while enforcing <see cref="TemplateExecutionLimits.MaxCommandChainDepth"/> and cycle prevention.
/// </summary>
public sealed class BotCmdTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Metadata key carrying the serialized sub-command chain depth for downstream dispatchers.
    /// </summary>
    public const string ChainDepthMetadataKey = "streamactions.command_chain_depth";

    /// <summary>
    /// Metadata key carrying comma-separated visited triggers for cycle prevention in downstream dispatchers.
    /// </summary>
    public const string VisitedTriggersMetadataKey = "streamactions.visited_command_triggers";

    /// <inheritdoc />
    public string TagName => "botcmd";

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command triggers to lowercase invariant.")]
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

        ICommandDispatcher? dispatcher = context.GetService<ICommandDispatcher>();
        if (dispatcher is null)
        {
            return TagResult.Incomplete;
        }

        if (context.ChainDepth >= TemplateExecutionLimits.MaxCommandChainDepth)
        {
            return TagResult.Incomplete;
        }

        TagResult nameResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!nameResult.IsComplete || string.IsNullOrWhiteSpace(nameResult.Value))
        {
            return TagResult.Incomplete;
        }

        string normalizedTrigger = nameResult.Value.Trim().ToLowerInvariant();
        if (context.VisitedCommandTriggers.Contains(normalizedTrigger))
        {
            return TagResult.Incomplete;
        }

        List<string> childArgs = new(Math.Max(0, unevaluatedArgs.Count - 1));
        for (int i = 1; i < unevaluatedArgs.Count; i++)
        {
            TagResult argResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            childArgs.Add(argResult.Value);
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

        ICommandInvocation childInvocation = BuildChildInvocation(
            context,
            prefix,
            normalizedTrigger,
            childArgs.AsReadOnly(),
            rawArgs,
            rawText,
            nextDepth,
            nextVisited);

        CommandDispatchResult dispatchResult = await dispatcher.DispatchAsync(childInvocation, cancellationToken).ConfigureAwait(false);
        return dispatchResult.Executed
            ? TagResult.Complete(string.Empty)
            : TagResult.Incomplete;
    }

    /// <summary>
    /// Constructs an <see cref="ICommandInvocation"/> preserving the originating user/channel context and chain tracking metadata.
    /// </summary>
    /// <param name="context">The parent evaluation context.</param>
    /// <param name="prefix">The command prefix.</param>
    /// <param name="trigger">The normalized lowercase trigger.</param>
    /// <param name="arguments">The evaluated child arguments.</param>
    /// <param name="rawArguments">The joined raw argument string.</param>
    /// <param name="rawText">The synthesized raw command text.</param>
    /// <param name="nextDepth">The incremented command chain depth.</param>
    /// <param name="nextVisited">The updated visited trigger set.</param>
    /// <returns>The child <see cref="ICommandInvocation"/>.</returns>
    private static ICommandInvocation BuildChildInvocation(
        ITagEvaluationContext context,
        string prefix,
        string trigger,
        IReadOnlyList<string> arguments,
        string rawArguments,
        string rawText,
        int nextDepth,
        IReadOnlySet<string> nextVisited)
    {
        if (context.Message is IWhisper whisper)
        {
            return new WhisperCommand(
                whisper.WorkspaceId,
                whisper.PlatformId,
                whisper.MessageId,
                whisper.Sender,
                rawText,
                whisper.ReceivedAtUtc,
                prefix,
                trigger,
                arguments,
                rawArguments,
                whisper.TargetPlatformUserId);
        }

        Dictionary<string, string> metadata = context.Message is IChatMessage existingChat
            ? new Dictionary<string, string>(existingChat.Metadata, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        metadata[ChainDepthMetadataKey] = nextDepth.ToString(CultureInfo.InvariantCulture);
        metadata[VisitedTriggersMetadataKey] = string.Join(',', nextVisited);

        IChatChannel channel = (context.Message as IChatMessage)?.Channel
            ?? new ChatChannel(context.WorkspaceId, context.PlatformId, context.ChannelName, context.ChannelName);

        return new ChatCommand(
            context.WorkspaceId,
            context.PlatformId,
            context.Message.MessageId,
            context.Message.Sender,
            channel,
            rawText,
            context.Message.ReceivedAtUtc,
            prefix,
            trigger,
            arguments,
            rawArguments,
            (context.Message as IChatMessage)?.ReplyToMessageId,
            (context.Message as IChatMessage)?.IsAction ?? false,
            metadata.AsReadOnly());
    }
}

/// <summary>
/// Handles the <c>{commands_list [prefix]}</c> tag, listing enabled, non-hidden commands from <see cref="ICommandRepository"/>.
/// </summary>
public sealed class CommandsListTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "commands_list";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        ICommandRepository? repository = context.GetService<ICommandRepository>();
        if (repository is null)
        {
            return TagResult.Incomplete;
        }

        string prefix = (context.Message as ICommandInvocation)?.Prefix ?? "!";
        if (unevaluatedArgs.Count >= 1)
        {
            TagResult prefixResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!prefixResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            prefix = prefixResult.Value;
        }

        IReadOnlyList<CustomCommand> commands = await repository.GetCommandsAsync(context.WorkspaceId, cancellationToken).ConfigureAwait(false);
        string[] visibleTriggers =
        [
            .. commands
                .Where(static command => command.IsEnabled && !command.IsHiddenFromPublicPortal && !string.IsNullOrWhiteSpace(command.Trigger))
                .Select(command => $"{prefix}{command.Trigger.Trim()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static trigger => trigger, StringComparer.OrdinalIgnoreCase),
        ];

        return visibleTriggers.Length == 0
            ? TagResult.Incomplete
            : TagResult.Complete(string.Join(", ", visibleTriggers));
    }
}

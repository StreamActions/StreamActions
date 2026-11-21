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
using StreamActions.Core.Models.Messaging;

namespace StreamActions.Core.Commands;

/// <summary>
/// Provides execution state, normalized sub-command arguments, and reply delivery helpers to an <see cref="IBuiltInCommandHandler"/>.
/// </summary>
public sealed class BuiltInCommandContext
{
    /// <summary>
    /// Optional outbound chat dispatcher used to deliver replies.
    /// </summary>
    private readonly IOutboundChatDispatcher? _outboundDispatcher;

    /// <summary>
    /// Optional workspace store used to resolve channel identifiers when replying to whispers in channel scope.
    /// </summary>
    private readonly IWorkspaceStore? _workspaceStore;

    /// <summary>
    /// Mutable list of reply texts sent or recorded during handler execution.
    /// </summary>
    private readonly List<string> _sentResponses = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltInCommandContext"/> class.
    /// </summary>
    /// <param name="invocation">The parsed inbound command invocation.</param>
    /// <param name="commandId">The canonical command identifier being executed.</param>
    /// <param name="matchedTrigger">The normalized trigger or sub-command trigger that matched.</param>
    /// <param name="isSubCommandInvocation"><see langword="true"/> when invoked via <c>!bot</c> or <c>!&lt;botname&gt;</c>; <see langword="false"/> for top-level invocation.</param>
    /// <param name="arguments">The effective arguments for the built-in command (with the sub-command trigger removed when <paramref name="isSubCommandInvocation"/> is <see langword="true"/>).</param>
    /// <param name="rawArguments">The raw unparsed argument string for the built-in command.</param>
    /// <param name="actorIdentity">The resolved actor identity of the invoking user.</param>
    /// <param name="outboundDispatcher">Optional outbound chat dispatcher for delivering replies.</param>
    /// <param name="workspaceStore">Optional workspace store for channel resolution.</param>
    /// <param name="localizer">Optional localizer for resolving localized response templates.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="services">Optional DI service provider.</param>
    public BuiltInCommandContext(
        ICommandInvocation invocation,
        string commandId,
        string matchedTrigger,
        bool isSubCommandInvocation,
        IReadOnlyList<string> arguments,
        string rawArguments,
        PlatformUserIdentity? actorIdentity = null,
        IOutboundChatDispatcher? outboundDispatcher = null,
        IWorkspaceStore? workspaceStore = null,
        IStreamActionsLocalizer? localizer = null,
        TimeProvider? timeProvider = null,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchedTrigger);
        ArgumentNullException.ThrowIfNull(arguments);

        this.Invocation = invocation;
        this.CommandId = commandId;
        this.MatchedTrigger = matchedTrigger;
        this.IsSubCommandInvocation = isSubCommandInvocation;
        this.Arguments = arguments;
        this.RawArguments = rawArguments ?? string.Empty;
        this.ActorIdentity = actorIdentity ?? invocation.Sender.ToIdentity();
        this._outboundDispatcher = outboundDispatcher;
        this._workspaceStore = workspaceStore;
        this.Localizer = localizer;
        this.TimeProvider = timeProvider ?? TimeProvider.System;
        this.Services = services;
    }

    /// <summary>
    /// Gets the original inbound command invocation.
    /// </summary>
    public ICommandInvocation Invocation { get; }

    /// <summary>
    /// Gets the canonical built-in command identifier.
    /// </summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets the normalized trigger that matched this command.
    /// </summary>
    public string MatchedTrigger { get; }

    /// <summary>
    /// Gets a value indicating whether the command was invoked as a <c>!bot</c> or <c>!&lt;botname&gt;</c> sub-command.
    /// </summary>
    public bool IsSubCommandInvocation { get; }

    /// <summary>
    /// Gets the effective argument list for the command.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the effective raw argument string for the command.
    /// </summary>
    public string RawArguments { get; }

    /// <summary>
    /// Gets the invoking actor identity used for permission evaluation.
    /// </summary>
    public PlatformUserIdentity ActorIdentity { get; }

    /// <summary>
    /// Gets the optional localizer for formatting localized bot responses.
    /// </summary>
    public IStreamActionsLocalizer? Localizer { get; }

    /// <summary>
    /// Gets the time provider associated with this execution.
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the optional service provider associated with this execution.
    /// </summary>
    public IServiceProvider? Services { get; }

    /// <summary>
    /// Gets the ordered list of response texts emitted during this execution.
    /// </summary>
    public IReadOnlyList<string> SentResponses => this._sentResponses.AsReadOnly();

    /// <summary>
    /// Gets or sets the optional explicit <see cref="CommandDispatchResult"/> override set by the handler.
    /// </summary>
    public CommandDispatchResult? Result { get; set; }

    /// <summary>
    /// Queues a reply message back to the originating chat channel or whisper sender via <see cref="IOutboundChatDispatcher"/>.
    /// </summary>
    /// <param name="message">The reply text to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous reply enqueue operation.</returns>
    public async ValueTask SendReplyAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string trimmed = message.Trim();
        this._sentResponses.Add(trimmed);

        if (this._outboundDispatcher is null)
        {
            return;
        }

        OutboundChatMessage outbound;
        if (this.Invocation is IWhisperCommand)
        {
            outbound = new OutboundChatMessage(
                this.Invocation.WorkspaceId,
                this.Invocation.PlatformId,
                string.Empty,
                trimmed,
                OutboundChatPriority.CommandReply,
                OutboundTargetScope.OriginPlatform,
                DuplicateMessagePolicy.PlatformDefault,
                ReplyToMessageId: null,
                IsWhisper: true,
                TargetPlatformUserId: this.Invocation.Sender.PlatformUserId);
        }
        else
        {
            string channelId = (this.Invocation as IChatMessage)?.Channel.ChannelId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(channelId) && this._workspaceStore is not null)
            {
                WorkspacePlatform? platform = await this._workspaceStore
                    .GetPlatformAsync(this.Invocation.WorkspaceId, this.Invocation.PlatformId, cancellationToken)
                    .ConfigureAwait(false);
                channelId = platform?.PlatformChannelId ?? string.Empty;
            }

            outbound = new OutboundChatMessage(
                this.Invocation.WorkspaceId,
                this.Invocation.PlatformId,
                channelId,
                trimmed,
                OutboundChatPriority.CommandReply,
                OutboundTargetScope.OriginPlatform,
                DuplicateMessagePolicy.PlatformDefault,
                ReplyToMessageId: (this.Invocation as IChatMessage)?.MessageId);
        }

        await this._outboundDispatcher.EnqueueAsync(outbound, cancellationToken).ConfigureAwait(false);
    }
}

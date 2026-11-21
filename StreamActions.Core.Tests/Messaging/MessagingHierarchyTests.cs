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

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Counters;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Messaging;

/// <summary>
/// Unit tests covering messaging interfaces, concrete models, enums, metadata, stream status provider, and DI registration.
/// </summary>
public sealed class MessagingHierarchyTests
{
    #region Enums

    [Fact]
    [Trait("Member", "Enums")]
    public void CounterUsageBehaviorIncludesParameterizedAsSix()
    {
        _ = ((int)CounterUsageBehavior.Parameterized).Should().Be(6);
        _ = Enum.IsDefined(CounterUsageBehavior.Parameterized).Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Enums")]
    public void OutboundEnumsDefineExpectedOrderingAndMembers()
    {
        _ = ((int)OutboundChatPriority.ModerationImmediate).Should().BeLessThan((int)OutboundChatPriority.CommandReply);
        _ = ((int)OutboundChatPriority.CommandReply).Should().BeLessThan((int)OutboundChatPriority.EventAnnouncement);
        _ = ((int)OutboundChatPriority.EventAnnouncement).Should().BeLessThan((int)OutboundChatPriority.Timer);

        _ = Enum.GetValues<OutboundTargetScope>().Should().Contain([
            OutboundTargetScope.OriginPlatform,
            OutboundTargetScope.PrimaryPlatformOnly,
            OutboundTargetScope.AllActiveStreamPlatforms,
        ]);

        _ = Enum.GetValues<DuplicateMessagePolicy>().Should().Contain([
            DuplicateMessagePolicy.PlatformDefault,
            DuplicateMessagePolicy.Suppress,
            DuplicateMessagePolicy.Allow,
        ]);
    }

    #endregion

    #region CommandMetadata

    [Fact]
    [Trait("Member", "CommandMetadata")]
    public void CommandMetadataHasExpectedDefaultsForNewProperties()
    {
        CommandMetadata metadata = new();

        _ = metadata.CounterMutateDesignations.Should().Be(PlatformUserDesignations.Moderator);
        _ = metadata.CounterMutatePermissionKey.Should().BeNull();
        _ = metadata.AllowedPlatforms.Should().BeNull();
        _ = metadata.TargetScope.Should().Be(OutboundTargetScope.OriginPlatform);
        _ = metadata.AllowCustomOverride.Should().BeFalse();
    }

    #endregion

    #region IChatPlatform

    [Fact]
    [Trait("Member", "IChatPlatform")]
    public async Task IChatPlatformDefaultInterfaceMembersRemainBackwardCompatible()
    {
        IChatPlatform platform = new MinimalChatPlatform();
        Guid workspaceId = Guid.NewGuid();

        _ = platform.MaxMessageLength.Should().Be(500);
        _ = platform.SupportsDuplicateMessagesAsNonModerator.Should().BeFalse();
        _ = (await platform.IsBotModeratorOrVipAsync(workspaceId, "chan1", TestContext.Current.CancellationToken)).Should().BeFalse();
        await platform.SendWhisperAsync(workspaceId, "user1", "hello", TestContext.Current.CancellationToken);
    }

    #endregion

    #region MessagingModels

    [Fact]
    [Trait("Member", "MessagingHierarchy")]
    public void ConcreteMessagingTypesImplementFullHierarchyAndRemainImmutable()
    {
        Guid workspaceId = Guid.NewGuid();
        DateTimeOffset receivedAt = new(2026, 11, 21, 15, 0, 0, TimeSpan.Zero);
        ChatUser user = new("twitch", "123", "streamer", "Streamer", PlatformUserDesignations.Broadcaster);
        ChatChannel channel = new(workspaceId, "twitch", "chan-1", "streamer");

        ChatMessage chatMessage = new(
            workspaceId,
            "twitch",
            "msg-1",
            user,
            channel,
            "hello chat",
            receivedAt,
            ReplyToMessageId: "parent-1",
            IsAction: true);

        ChatCommand chatCommand = new(
            workspaceId,
            "twitch",
            "msg-2",
            user,
            channel,
            "!ping arg1",
            receivedAt,
            "!",
            "ping",
            ["arg1"],
            "arg1");

        WhisperMessage whisper = new(workspaceId, "twitch", "w-1", user, "secret", receivedAt, "bot-1");
        WhisperCommand whisperCommand = new(
            workspaceId,
            "twitch",
            "w-2",
            user,
            "!help me",
            receivedAt,
            "!",
            "help",
            ["me"],
            "me",
            "bot-1");

        _ = chatMessage.Should().BeAssignableTo<IChatMessage>();
        _ = chatMessage.Should().BeAssignableTo<IMessageEnvelope>();
        _ = chatMessage.Metadata.Should().BeEmpty();
        _ = chatMessage.IsAction.Should().BeTrue();
        _ = chatMessage.ReplyToMessageId.Should().Be("parent-1");

        _ = chatCommand.Should().BeAssignableTo<IChatCommand>();
        _ = chatCommand.Should().BeAssignableTo<IChatMessage>();
        _ = chatCommand.Should().BeAssignableTo<ICommandInvocation>();
        _ = chatCommand.Metadata.Should().BeEmpty();

        _ = whisper.Should().BeAssignableTo<IWhisper>();
        _ = whisper.Should().BeAssignableTo<IMessageEnvelope>();
        _ = whisper.TargetPlatformUserId.Should().Be("bot-1");

        _ = whisperCommand.Should().BeAssignableTo<IWhisperCommand>();
        _ = whisperCommand.Should().BeAssignableTo<IWhisper>();
        _ = whisperCommand.Should().BeAssignableTo<ICommandInvocation>();

        PlatformUserIdentity identity = ((IChatUser)user).ToIdentity();
        _ = identity.PlatformId.Should().Be("twitch");
        _ = identity.PlatformUserId.Should().Be("123");
        _ = identity.Designations.Should().Be(PlatformUserDesignations.Broadcaster);
    }

    [Fact]
    [Trait("Member", "CommandDispatchResult")]
    public void CommandDispatchResultFactoryMethodsPopulateExpectedFlags()
    {
        CommandDispatchResult notFound = CommandDispatchResult.NotFound("missing");
        CommandDispatchResult success = CommandDispatchResult.Success("ping");
        CommandDispatchResult rejected = CommandDispatchResult.Rejected(CommandDispatchStatus.OnGlobalCooldown, "ping", "Wait 5s");

        _ = notFound.Matched.Should().BeFalse();
        _ = notFound.Executed.Should().BeFalse();
        _ = notFound.Status.Should().Be(CommandDispatchStatus.NotFound);

        _ = success.Matched.Should().BeTrue();
        _ = success.Executed.Should().BeTrue();
        _ = success.Trigger.Should().Be("ping");

        _ = rejected.Matched.Should().BeTrue();
        _ = rejected.Executed.Should().BeFalse();
        _ = rejected.Status.Should().Be(CommandDispatchStatus.OnGlobalCooldown);
        _ = rejected.FailureReason.Should().Be("Wait 5s");
    }

    [Fact]
    [Trait("Member", "Events")]
    public void EventsCaptureWorkspaceAndCounterFields()
    {
        Guid workspaceId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 11, 21, 15, 0, 0, TimeSpan.Zero);

        CommandsInvalidatedEvent commandsEvent = new(workspaceId);
        CounterUpdatedEvent counterEvent = new(
            workspaceId,
            "deaths",
            CounterScopeMode.PerGame,
            "Elden Ring",
            4,
            5,
            now);

        _ = commandsEvent.WorkspaceId.Should().Be(workspaceId);
        _ = counterEvent.CounterName.Should().Be("deaths");
        _ = counterEvent.PreviousValue.Should().Be(4);
        _ = counterEvent.CurrentValue.Should().Be(5);
        _ = counterEvent.UpdatedAtUtc.Should().Be(now);
    }

    #endregion

    #region DefaultStreamStatusProvider

    [Fact]
    [Trait("Member", "DefaultStreamStatusProvider")]
    public async Task DefaultStreamStatusProviderDefaultsOfflineAndTracksUpdates()
    {
        DefaultStreamStatusProvider provider = new();
        Guid workspaceId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;

        _ = (await provider.IsStreamLiveAsync(workspaceId, cancellationToken: ct)).Should().BeFalse();
        _ = (await provider.GetCurrentGameCategoryAsync(workspaceId, cancellationToken: ct)).Should().BeNull();
        _ = (await provider.GetActiveStreamPlatformIdsAsync(workspaceId, ct)).Should().BeEmpty();

        provider.SetPlatformStatus(workspaceId, "twitch", isLive: true, "Just Chatting");
        provider.SetPlatformStatus(workspaceId, "youtube", isLive: false, "Gaming");

        _ = (await provider.IsStreamLiveAsync(workspaceId, cancellationToken: ct)).Should().BeTrue();
        _ = (await provider.IsStreamLiveAsync(workspaceId, "twitch", ct)).Should().BeTrue();
        _ = (await provider.IsStreamLiveAsync(workspaceId, "youtube", ct)).Should().BeFalse();
        _ = (await provider.GetCurrentGameCategoryAsync(workspaceId, cancellationToken: ct)).Should().Be("Just Chatting");
        _ = (await provider.GetCurrentGameCategoryAsync(workspaceId, "youtube", ct)).Should().Be("Gaming");
        _ = (await provider.GetActiveStreamPlatformIdsAsync(workspaceId, ct)).Should().Equal("twitch");
    }

    #endregion

    #region AddStreamActionsCore

    [Fact]
    [Trait("Member", "AddStreamActionsCore")]
    public void AddStreamActionsCoreRegistersOutboundDispatcherAsSingletonAndHostedService()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        _ = services.AddStreamActionsCore(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IStreamStatusProvider statusProvider = provider.GetRequiredService<IStreamStatusProvider>();
        IOutboundChatDispatcher dispatcherInterface = provider.GetRequiredService<IOutboundChatDispatcher>();
        OutboundChatDispatcher dispatcherConcrete = provider.GetRequiredService<OutboundChatDispatcher>();
        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();

        _ = statusProvider.Should().BeOfType<DefaultStreamStatusProvider>();
        _ = dispatcherInterface.Should().BeSameAs(dispatcherConcrete);
        _ = hostedServices.Should().ContainSingle(static s => s is OutboundChatDispatcher)
            .Which.Should().BeSameAs(dispatcherConcrete);
    }

    #endregion

    /// <summary>
    /// Minimal <see cref="IChatPlatform"/> implementation verifying default interface member compatibility.
    /// </summary>
    private sealed class MinimalChatPlatform : IChatPlatform
    {
        /// <inheritdoc/>
        public string PlatformId => "minimal";

        /// <inheritdoc/>
        public string DisplayName => "Minimal";

        /// <inheritdoc/>
        public PlatformCapabilities Capabilities => PlatformCapabilities.Chat;

        /// <inheritdoc/>
        public ValueTask ConnectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask DisconnectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask SendMessageAsync(Guid workspaceId, string channelId, string message, string? replyToMessageId = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

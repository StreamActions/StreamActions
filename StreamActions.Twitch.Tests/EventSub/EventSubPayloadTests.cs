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
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.EventSub.Common;
using System.Reflection;
using System.Text.Json;
using Xunit;
using AdBreakBegin = StreamActions.Twitch.EventSub.Channel.AdBreak.Begin;
using AutomodHold = StreamActions.Twitch.EventSub.Automod.Message.Hold;
using AutomodMessageUpdate = StreamActions.Twitch.EventSub.Automod.Message.Update;
using AutomodSettingsUpdate = StreamActions.Twitch.EventSub.Automod.Settings.Update;
using AutomodTermAction = StreamActions.Twitch.EventSub.Automod.Terms.TermAction;
using AutomodTermsUpdate = StreamActions.Twitch.EventSub.Automod.Terms.Update;
using AutoRedemptionAdd = StreamActions.Twitch.EventSub.Channel.ChannelPointsAutomaticRewardRedemption.Add;
using BitsTransactionCreate = StreamActions.Twitch.EventSub.Extension.BitsTransaction.Create;
using BitsUse = StreamActions.Twitch.EventSub.Channel.Bits.Use;
using ChannelBan = StreamActions.Twitch.EventSub.Channel.Ban;
using ChannelCheer = StreamActions.Twitch.EventSub.Channel.Cheer;
using ChannelFollow = StreamActions.Twitch.EventSub.Channel.Follow;
using ChannelModerate = StreamActions.Twitch.EventSub.Channel.Moderate.Moderate;
using ChannelRaid = StreamActions.Twitch.EventSub.Channel.Raid;
using ChannelSubscribe = StreamActions.Twitch.EventSub.Channel.Subscribe;
using ChannelUnban = StreamActions.Twitch.EventSub.Channel.Unban;
using ChannelUpdate = StreamActions.Twitch.EventSub.Channel.Update;
using CharityDonate = StreamActions.Twitch.EventSub.Channel.CharityCampaign.Donate;
using CharityProgress = StreamActions.Twitch.EventSub.Channel.CharityCampaign.CampaignProgress;
using CharityStart = StreamActions.Twitch.EventSub.Channel.CharityCampaign.CampaignStart;
using CharityStop = StreamActions.Twitch.EventSub.Channel.CharityCampaign.CampaignStop;
using ChatClear = StreamActions.Twitch.EventSub.Channel.Chat.Clear;
using ChatClearUserMessages = StreamActions.Twitch.EventSub.Channel.Chat.ClearUserMessages;
using ChatMessage = StreamActions.Twitch.EventSub.Channel.Chat.Message;
using ChatMessageDelete = StreamActions.Twitch.EventSub.Channel.Chat.MessageDelete;
using ChatNoticeType = StreamActions.Twitch.EventSub.Channel.Chat.Notification.NoticeType;
using ChatNotification = StreamActions.Twitch.EventSub.Channel.Chat.Notification;
using ChatSettingsUpdate = StreamActions.Twitch.EventSub.Channel.ChatSettings.Update;
using ChatUserMessageHold = StreamActions.Twitch.EventSub.Channel.Chat.UserMessageHold;
using ChatUserMessageUpdate = StreamActions.Twitch.EventSub.Channel.Chat.UserMessageUpdate;
using ConduitShardDisabled = StreamActions.Twitch.EventSub.Conduit.Shard.Disabled;
using CustomRedemptionAdd = StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomRewardRedemption.Add;
using CustomRedemptionUpdate = StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomRewardRedemption.Update;
using CustomRewardAdd = StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Add;
using CustomRewardRemove = StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Remove;
using CustomRewardUpdate = StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Update;
using DropEntitlementGrant = StreamActions.Twitch.EventSub.Drop.Entitlement.Grant;
using GoalBegin = StreamActions.Twitch.EventSub.Channel.Goal.Begin;
using GoalEnd = StreamActions.Twitch.EventSub.Channel.Goal.GoalEnd;
using GoalProgress = StreamActions.Twitch.EventSub.Channel.Goal.Progress;
using GuestStarGuestUpdate = StreamActions.Twitch.EventSub.Channel.GuestStarGuest.Update;
using GuestStarSessionBegin = StreamActions.Twitch.EventSub.Channel.GuestStarSession.SessionBegin;
using GuestStarSessionEnd = StreamActions.Twitch.EventSub.Channel.GuestStarSession.SessionEnd;
using GuestStarSettingsUpdate = StreamActions.Twitch.EventSub.Channel.GuestStarSettings.Update;
using HypeTrainBegin = StreamActions.Twitch.EventSub.Channel.HypeTrain.Begin;
using HypeTrainEnd = StreamActions.Twitch.EventSub.Channel.HypeTrain.HypeTrainEnd;
using HypeTrainProgress = StreamActions.Twitch.EventSub.Channel.HypeTrain.Progress;
using LowTrustStatusEnum = StreamActions.Twitch.EventSub.Channel.SuspiciousUser.Objects.LowTrustStatus;
using ModeratorAdd = StreamActions.Twitch.EventSub.Channel.Moderator.Add;
using ModeratorRemove = StreamActions.Twitch.EventSub.Channel.Moderator.Remove;
using PollBegin = StreamActions.Twitch.EventSub.Channel.Poll.PollBegin;
using PollEnd = StreamActions.Twitch.EventSub.Channel.Poll.PollEnd;
using PollProgress = StreamActions.Twitch.EventSub.Channel.Poll.PollProgress;
using PollStatusEnum = StreamActions.Twitch.EventSub.Channel.Poll.Objects.PollStatus;
using PowerUpAdd = StreamActions.Twitch.EventSub.Channel.CustomPowerUpRedemption.Add;
using PredictionBegin = StreamActions.Twitch.EventSub.Channel.Prediction.PredictionBegin;
using PredictionEnd = StreamActions.Twitch.EventSub.Channel.Prediction.PredictionEnd;
using PredictionLock = StreamActions.Twitch.EventSub.Channel.Prediction.PredictionLock;
using PredictionProgress = StreamActions.Twitch.EventSub.Channel.Prediction.PredictionProgress;
using PredictionStatusEnum = StreamActions.Twitch.EventSub.Channel.Prediction.Objects.PredictionStatus;
using SharedChatBegin = StreamActions.Twitch.EventSub.Channel.SharedChat.Begin;
using SharedChatEnd = StreamActions.Twitch.EventSub.Channel.SharedChat.SessionEnd;
using SharedChatUpdate = StreamActions.Twitch.EventSub.Channel.SharedChat.Update;
using ShieldModeBegin = StreamActions.Twitch.EventSub.Channel.ShieldMode.Begin;
using ShieldModeEnd = StreamActions.Twitch.EventSub.Channel.ShieldMode.ShieldModeEnd;
using ShoutoutCreate = StreamActions.Twitch.EventSub.Channel.Shoutout.Create;
using ShoutoutReceive = StreamActions.Twitch.EventSub.Channel.Shoutout.Receive;
using StreamOffline = StreamActions.Twitch.EventSub.Stream.Offline;
using StreamOnline = StreamActions.Twitch.EventSub.Stream.Online;
using SubscriptionEnd = StreamActions.Twitch.EventSub.Channel.Subscription.SubscriptionEnd;
using SubscriptionGift = StreamActions.Twitch.EventSub.Channel.Subscription.Gift;
using SubscriptionMessage = StreamActions.Twitch.EventSub.Channel.Subscription.Message;
using SuspiciousUserMessage = StreamActions.Twitch.EventSub.Channel.SuspiciousUser.MessageEvent;
using SuspiciousUserUpdate = StreamActions.Twitch.EventSub.Channel.SuspiciousUser.Update;
using UnbanRequestCreate = StreamActions.Twitch.EventSub.Channel.UnbanRequest.Create;
using UnbanRequestResolve = StreamActions.Twitch.EventSub.Channel.UnbanRequest.Resolve;
using UserAuthGrant = StreamActions.Twitch.EventSub.User.Authorization.Grant;
using UserAuthRevoke = StreamActions.Twitch.EventSub.User.Authorization.Revoke;
using UserUpdate = StreamActions.Twitch.EventSub.User.Update;
using UserWhisperMessage = StreamActions.Twitch.EventSub.User.Whisper.Message;
using VipAdd = StreamActions.Twitch.EventSub.Channel.Vip.Add;
using VipRemove = StreamActions.Twitch.EventSub.Channel.Vip.Remove;
using WarningAcknowledge = StreamActions.Twitch.EventSub.Channel.Warning.Acknowledge;
using WarningSend = StreamActions.Twitch.EventSub.Channel.Warning.Send;

namespace StreamActions.Twitch.Tests.EventSub;

/// <summary>
/// Helper providing EventSub payload type resolution utilities.
/// </summary>
public static class EventSubPayload
{
    /// <summary>
    /// Resolves the concrete event type implementing <see cref="IEventSubType"/> for a given subscription type string and optional version.
    /// </summary>
    /// <param name="subscriptionType">The EventSub subscription type string.</param>
    /// <param name="version">The optional subscription version string.</param>
    /// <returns>The resolved event type, or <see langword="null"/> if not registered or matching.</returns>
    public static Type? GetEventType(string subscriptionType, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(subscriptionType))
        {
            return null;
        }

        foreach (Type type in typeof(StreamActions.Twitch.EventSub.EventSub).Assembly.GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && !type.IsGenericType && typeof(IEventSubType).IsAssignableFrom(type))
            {
                PropertyInfo? typeProp = type.GetProperty(nameof(IEventSubType.Type), BindingFlags.Public | BindingFlags.Static);
                if (typeProp?.GetValue(null) is string registeredType && string.Equals(registeredType, subscriptionType, StringComparison.OrdinalIgnoreCase))
                {
                    if (version is not null)
                    {
                        PropertyInfo? versionProp = type.GetProperty(nameof(IEventSubType.Version), BindingFlags.Public | BindingFlags.Static);
                        if (versionProp?.GetValue(null) is string registeredVersion && !string.Equals(registeredVersion, version, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    return type;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Performs unit tests for EventSub payload deserialization and type mapping across all 70 subscription types.
/// </summary>
public sealed class EventSubPayloadTests
{
    #region GetEventType

    [Fact]
    [Trait("Member", "GetEventType")]
    public void GetEventType_WhenAutomodMessageHoldV2_ReturnsHoldType()
    {
        Type? type = EventSubPayload.GetEventType("automod.message.hold", "2");
        _ = type.Should().Be<AutomodHold>();
    }

    [Fact]
    [Trait("Member", "GetEventType")]
    public void GetEventType_WhenAutomodMessageHoldV1_ReturnsNull()
    {
        Type? type = EventSubPayload.GetEventType("automod.message.hold", "1");
        _ = type.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetEventType")]
    public void GetEventType_WhenChannelChatMessageDelete_ReturnsMessageDeleteType()
    {
        Type? type = EventSubPayload.GetEventType("channel.chat.message_delete");
        _ = type.Should().Be<ChatMessageDelete>();
    }

    [Fact]
    [Trait("Member", "GetEventType")]
    public void GetEventType_WhenChannelGuestStarSessionBeginBeta_ReturnsSessionBeginType()
    {
        Type? type = EventSubPayload.GetEventType("channel.guest_star_session.begin", "beta");
        _ = type.Should().Be<GuestStarSessionBegin>();
    }

    [Fact]
    [Trait("Member", "GetEventType")]
    public void GetEventType_WhenUnknownSubscriptionType_ReturnsNull()
    {
        Type? type = EventSubPayload.GetEventType("unknown.subscription.type");
        _ = type.Should().BeNull();
    }

    [Theory]
    [Trait("Member", "GetEventType")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEventType_WhenNullOrWhitespace_ReturnsNull(string? invalidType)
    {
        Type? type = EventSubPayload.GetEventType(invalidType!);
        _ = type.Should().BeNull();
    }

    #endregion GetEventType

    #region DeserializePayload

    #region Automod Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_AutomodMessageHold_ParsesHoldEvent()
    {
        string json = """
        {
          "subscription": { "type": "automod.message.hold", "version": "2" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "baduser",
            "user_name": "BadUser",
            "message_id": "bad-msg-123",
            "reason": "automod"
          }
        }
        """;

        EventPayload<AutomodHold> payload = DeserializePayload<AutomodHold>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
        _ = payload.Event.UserId.Should().Be("4242");
        _ = payload.Event.MessageId.Should().Be("bad-msg-123");
        _ = payload.Event.Reason.Should().Be(AutomodHold.HoldReason.Automod);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_AutomodMessageUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "automod.message.update", "version": "2" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "baduser",
            "user_name": "BadUser",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "message_id": "bad-msg-123",
            "status": "approved"
          }
        }
        """;

        EventPayload<AutomodMessageUpdate> payload = DeserializePayload<AutomodMessageUpdate>(json);
        _ = payload.Event!.ModeratorUserId.Should().Be("9001");
        _ = payload.Event.Status.Should().Be(AutomodMessageUpdate.MessageStatus.Approved);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_AutomodSettingsUpdate_ParsesSettingsUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "automod.settings.update", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "overall_level": 2
          }
        }
        """;

        EventPayload<AutomodSettingsUpdate> payload = DeserializePayload<AutomodSettingsUpdate>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
        _ = payload.Event.OverallLevel.Should().Be(2);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_AutomodTermsUpdate_ParsesTermsUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "automod.terms.update", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "action": "add_permitted",
            "list": "terms",
            "terms": [ "term1", "term2" ]
          }
        }
        """;

        EventPayload<AutomodTermsUpdate> payload = DeserializePayload<AutomodTermsUpdate>(json);
        _ = payload.Event!.Action.Should().Be(AutomodTermAction.AddPermitted);
        _ = payload.Event.Terms.Should().Contain("term1");
    }

    #endregion Automod Payloads

    #region Channel Core Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelBan_ParsesBanEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.ban", "version": "1" },
          "event": {
            "user_id": "4242",
            "user_login": "baduser",
            "user_name": "BadUser",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "reason": "spam",
            "is_permanent": true
          }
        }
        """;

        EventPayload<ChannelBan> payload = DeserializePayload<ChannelBan>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.Reason.Should().Be("spam");
        _ = payload.Event.IsPermanent.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelCheer_ParsesCheerEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.cheer", "version": "1" },
          "event": {
            "is_anonymous": false,
            "user_id": "4242",
            "user_login": "cheerer",
            "user_name": "Cheerer",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "message": "cheer100",
            "bits": 100
          }
        }
        """;

        EventPayload<ChannelCheer> payload = DeserializePayload<ChannelCheer>(json);
        _ = payload.Event!.Bits.Should().Be(100);
        _ = payload.Event.Message.Should().Be("cheer100");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelFollow_ParsesFollowEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.follow", "version": "2" },
          "event": {
            "user_id": "4242",
            "user_login": "follower",
            "user_name": "Follower",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<ChannelFollow> payload = DeserializePayload<ChannelFollow>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.BroadcasterUserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelRaid_ParsesRaidEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.raid", "version": "1" },
          "event": {
            "from_broadcaster_user_id": "1001",
            "from_broadcaster_user_login": "raider",
            "from_broadcaster_user_name": "Raider",
            "to_broadcaster_user_id": "1337",
            "to_broadcaster_user_login": "streamer",
            "to_broadcaster_user_name": "Streamer",
            "viewers": 50
          }
        }
        """;

        EventPayload<ChannelRaid> payload = DeserializePayload<ChannelRaid>(json);
        _ = payload.Event!.FromBroadcasterUserId.Should().Be("1001");
        _ = payload.Event.ToBroadcasterUserId.Should().Be("1337");
        _ = payload.Event.Viewers.Should().Be(50);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSubscribe_ParsesSubscribeEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.subscribe", "version": "1" },
          "event": {
            "user_id": "4242",
            "user_login": "subscriber",
            "user_name": "Subscriber",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "tier": "1000",
            "is_gift": false
          }
        }
        """;

        EventPayload<ChannelSubscribe> payload = DeserializePayload<ChannelSubscribe>(json);
        _ = payload.Event!.Tier.Should().Be("1000");
        _ = payload.Event.IsGift.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelUnban_ParsesUnbanEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.unban", "version": "1" },
          "event": {
            "user_id": "4242",
            "user_login": "unbanned",
            "user_name": "Unbanned",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod"
          }
        }
        """;

        EventPayload<ChannelUnban> payload = DeserializePayload<ChannelUnban>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.ModeratorUserId.Should().Be("9001");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.update", "version": "2" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Fun Stream",
            "language": "en",
            "category_id": "509658",
            "category_name": "Just Chatting"
          }
        }
        """;

        EventPayload<ChannelUpdate> payload = DeserializePayload<ChannelUpdate>(json);
        _ = payload.Event!.Title.Should().Be("Fun Stream");
        _ = payload.Event.CategoryName.Should().Be("Just Chatting");
    }

    #endregion Channel Core Payloads

    #region Channel AdBreak & Bits Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelAdBreakBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.ad_break.begin", "version": "1" },
          "event": {
            "duration_seconds": 60,
            "started_at": "2023-01-01T00:00:00Z",
            "is_automatic": false,
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<AdBreakBegin> payload = DeserializePayload<AdBreakBegin>(json);
        _ = payload.Event!.DurationSeconds.Should().Be(60);
        _ = payload.Event.IsAutomatic.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelBitsUse_ParsesUseEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.bits.use", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "bitsuser",
            "user_name": "BitsUser",
            "bits": 500
          }
        }
        """;

        EventPayload<BitsUse> payload = DeserializePayload<BitsUse>(json);
        _ = payload.Event!.Bits.Should().Be(500);
        _ = payload.Event.UserId.Should().Be("4242");
    }

    #endregion Channel AdBreak & Bits Payloads

    #region Channel Points Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPointsAutomaticRewardRedemptionAdd_ParsesAddEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.channel_points_automatic_reward_redemption.add", "version": "2" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "pointsuser",
            "user_name": "PointsUser",
            "id": "redemption-123"
          }
        }
        """;

        EventPayload<AutoRedemptionAdd> payload = DeserializePayload<AutoRedemptionAdd>(json);
        _ = payload.Event!.Id.Should().Be("redemption-123");
        _ = payload.Event.UserId.Should().Be("4242");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPointsCustomRewardAdd_ParsesAddEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.channel_points_custom_reward.add", "version": "1" },
          "event": {
            "id": "reward-123",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "is_enabled": true,
            "cost": 100,
            "title": "Hydrate",
            "prompt": "Drink water"
          }
        }
        """;

        EventPayload<CustomRewardAdd> payload = DeserializePayload<CustomRewardAdd>(json);
        _ = payload.Event!.Id.Should().Be("reward-123");
        _ = payload.Event.Title.Should().Be("Hydrate");
        _ = payload.Event.Cost.Should().Be(100);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPointsCustomRewardRemove_ParsesRemoveEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.channel_points_custom_reward.remove", "version": "1" },
          "event": {
            "id": "reward-123",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Hydrate"
          }
        }
        """;

        EventPayload<CustomRewardRemove> payload = DeserializePayload<CustomRewardRemove>(json);
        _ = payload.Event!.Id.Should().Be("reward-123");
        _ = payload.Event.Title.Should().Be("Hydrate");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPointsCustomRewardUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.channel_points_custom_reward.update", "version": "1" },
          "event": {
            "id": "reward-123",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "cost": 200,
            "title": "Hydrate More"
          }
        }
        """;

        EventPayload<CustomRewardUpdate> payload = DeserializePayload<CustomRewardUpdate>(json);
        _ = payload.Event!.Cost.Should().Be(200);
        _ = payload.Event.Title.Should().Be("Hydrate More");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPointsCustomRewardRedemptionAdd_ParsesAddEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.channel_points_custom_reward_redemption.add", "version": "1" },
          "event": {
            "id": "redemption-456",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "pointsuser",
            "user_name": "PointsUser",
            "user_input": "drink up",
            "status": "unfulfilled"
          }
        }
        """;

        EventPayload<CustomRedemptionAdd> payload = DeserializePayload<CustomRedemptionAdd>(json);
        _ = payload.Event!.Id.Should().Be("redemption-456");
        _ = payload.Event.UserInput.Should().Be("drink up");
        _ = payload.Event.Status.Should().Be("unfulfilled");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPointsCustomRewardRedemptionUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.channel_points_custom_reward_redemption.update", "version": "1" },
          "event": {
            "id": "redemption-456",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "pointsuser",
            "user_name": "PointsUser",
            "status": "fulfilled"
          }
        }
        """;

        EventPayload<CustomRedemptionUpdate> payload = DeserializePayload<CustomRedemptionUpdate>(json);
        _ = payload.Event!.Status.Should().Be("fulfilled");
    }

    #endregion Channel Points Payloads

    #region Charity Campaign Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelCharityCampaignProgress_ParsesProgressEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.charity_campaign.progress", "version": "1" },
          "event": {
            "id": "charity-1",
            "broadcaster_id": "1337",
            "broadcaster_login": "streamer",
            "broadcaster_name": "Streamer",
            "charity_name": "St. Jude",
            "charity_description": "Children Hospital"
          }
        }
        """;

        EventPayload<CharityProgress> payload = DeserializePayload<CharityProgress>(json);
        _ = payload.Event!.CharityName.Should().Be("St. Jude");
        _ = payload.Event.BroadcasterId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelCharityCampaignStart_ParsesStartEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.charity_campaign.start", "version": "1" },
          "event": {
            "id": "charity-1",
            "broadcaster_id": "1337",
            "broadcaster_login": "streamer",
            "broadcaster_name": "Streamer",
            "charity_name": "St. Jude",
            "charity_description": "Children Hospital"
          }
        }
        """;

        EventPayload<CharityStart> payload = DeserializePayload<CharityStart>(json);
        _ = payload.Event!.CharityName.Should().Be("St. Jude");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelCharityCampaignStop_ParsesStopEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.charity_campaign.stop", "version": "1" },
          "event": {
            "id": "charity-1",
            "broadcaster_id": "1337",
            "broadcaster_login": "streamer",
            "broadcaster_name": "Streamer",
            "charity_name": "St. Jude",
            "charity_description": "Children Hospital"
          }
        }
        """;

        EventPayload<CharityStop> payload = DeserializePayload<CharityStop>(json);
        _ = payload.Event!.CharityName.Should().Be("St. Jude");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelCharityCampaignDonate_ParsesDonateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.charity_campaign.donate", "version": "1" },
          "event": {
            "id": "charity-1",
            "broadcaster_id": "1337",
            "broadcaster_login": "streamer",
            "broadcaster_name": "Streamer",
            "charity_name": "St. Jude",
            "charity_description": "Children Hospital",
            "user_id": "4242",
            "user_login": "donor",
            "user_name": "Donor"
          }
        }
        """;

        EventPayload<CharityDonate> payload = DeserializePayload<CharityDonate>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.UserName.Should().Be("Donor");
    }

    #endregion Charity Campaign Payloads

    #region Channel Chat Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatClear_ParsesClearEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.clear", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<ChatClear> payload = DeserializePayload<ChatClear>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatClearUserMessages_ParsesClearUserMessagesEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.clear_user_messages", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "target_user_id": "4242",
            "target_user_login": "baduser",
            "target_user_name": "BadUser"
          }
        }
        """;

        EventPayload<ChatClearUserMessages> payload = DeserializePayload<ChatClearUserMessages>(json);
        _ = payload.Event!.TargetUserId.Should().Be("4242");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatMessage_ParsesMessageEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.message", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "chatter_user_id": "4242",
            "chatter_user_login": "chatter",
            "chatter_user_name": "Chatter",
            "message_id": "msg-123",
            "message": { "text": "Hello stream!" }
          }
        }
        """;

        EventPayload<ChatMessage> payload = DeserializePayload<ChatMessage>(json);
        _ = payload.Event!.MessageId.Should().Be("msg-123");
        _ = payload.Event.ChatterUserId.Should().Be("4242");
        _ = payload.Event.ChatMessage!.Text.Should().Be("Hello stream!");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatMessageDelete_ParsesMessageDeleteEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.message_delete", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "target_user_id": "4242",
            "target_user_login": "baduser",
            "target_user_name": "BadUser",
            "message_id": "deleted-msg-123"
          }
        }
        """;

        EventPayload<ChatMessageDelete> payload = DeserializePayload<ChatMessageDelete>(json);
        _ = payload.Event!.MessageId.Should().Be("deleted-msg-123");
        _ = payload.Event.TargetUserId.Should().Be("4242");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatNotification_ParsesNotificationEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.notification", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "chatter_user_id": "4242",
            "chatter_user_login": "chatter",
            "chatter_user_name": "Chatter",
            "notice_type": "sub",
            "system_message": "Subscribed at Tier 1"
          }
        }
        """;

        EventPayload<ChatNotification> payload = DeserializePayload<ChatNotification>(json);
        _ = payload.Event!.TypeOfNotice.Should().Be(ChatNoticeType.Sub);
        _ = payload.Event!.SystemMessage.Should().Be("Subscribed at Tier 1");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatUserMessageHold_ParsesUserMessageHoldEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.user_message_hold", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "helduser",
            "user_name": "HeldUser",
            "message_id": "held-msg-123"
          }
        }
        """;

        EventPayload<ChatUserMessageHold> payload = DeserializePayload<ChatUserMessageHold>(json);
        _ = payload.Event!.MessageId.Should().Be("held-msg-123");
        _ = payload.Event.UserId.Should().Be("4242");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatUserMessageUpdate_ParsesUserMessageUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat.user_message_update", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "helduser",
            "user_name": "HeldUser",
            "message_id": "held-msg-123",
            "status": "approved"
          }
        }
        """;

        EventPayload<ChatUserMessageUpdate> payload = DeserializePayload<ChatUserMessageUpdate>(json);
        _ = payload.Event!.MessageId.Should().Be("held-msg-123");
        _ = payload.Event.Status.Should().Be(ChatUserMessageUpdate.MessageStatus.Approved);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelChatSettingsUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.chat_settings.update", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "slow_mode": true,
            "slow_mode_wait_time_seconds": 10,
            "follower_mode": false,
            "subscriber_mode": false,
            "emote_mode": false,
            "unique_chat_mode": false
          }
        }
        """;

        EventPayload<ChatSettingsUpdate> payload = DeserializePayload<ChatSettingsUpdate>(json);
        _ = payload.Event!.SlowMode.Should().BeTrue();
        _ = payload.Event.SlowModeWaitTimeSeconds.Should().Be(10);
    }

    #endregion Channel Chat Payloads

    #region Channel Goal & PowerUp Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelCustomPowerUpRedemptionAdd_ParsesAddEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.custom_power_up_redemption.add", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "poweruser",
            "user_name": "PowerUser",
            "id": "powerup-123"
          }
        }
        """;

        EventPayload<PowerUpAdd> payload = DeserializePayload<PowerUpAdd>(json);
        _ = payload.Event!.Id.Should().Be("powerup-123");
        _ = payload.Event.UserId.Should().Be("4242");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGoalBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.goal.begin", "version": "1" },
          "event": {
            "id": "goal-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "type": "follow",
            "description": "Reach 1k followers",
            "current_amount": 100,
            "target_amount": 1000
          }
        }
        """;

        EventPayload<GoalBegin> payload = DeserializePayload<GoalBegin>(json);
        _ = payload.Event!.Id.Should().Be("goal-1");
        _ = payload.Event.TargetAmount.Should().Be(1000);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGoalEnd_ParsesEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.goal.end", "version": "1" },
          "event": {
            "id": "goal-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "type": "follow",
            "description": "Reach 1k followers",
            "current_amount": 1000,
            "target_amount": 1000,
            "is_achieved": true
          }
        }
        """;

        EventPayload<GoalEnd> payload = DeserializePayload<GoalEnd>(json);
        _ = payload.Event!.Id.Should().Be("goal-1");
        _ = payload.Event.IsAchieved.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGoalProgress_ParsesProgressEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.goal.progress", "version": "1" },
          "event": {
            "id": "goal-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "type": "follow",
            "description": "Reach 1k followers",
            "current_amount": 500,
            "target_amount": 1000
          }
        }
        """;

        EventPayload<GoalProgress> payload = DeserializePayload<GoalProgress>(json);
        _ = payload.Event!.CurrentAmount.Should().Be(500);
        _ = payload.Event.TargetAmount.Should().Be(1000);
    }

    #endregion Channel Goal & PowerUp Payloads

    #region Channel Guest Star Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGuestStarGuestUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.guest_star_guest.update", "version": "beta" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "session_id": "session-1",
            "guest_user_id": "4242",
            "guest_user_login": "guest",
            "guest_user_name": "Guest",
            "slot_id": "1",
            "state": "live"
          }
        }
        """;

        EventPayload<GuestStarGuestUpdate> payload = DeserializePayload<GuestStarGuestUpdate>(json);
        _ = payload.Event!.SessionId.Should().Be("session-1");
        _ = payload.Event.GuestUserId.Should().Be("4242");
        _ = payload.Event.State.Should().Be("live");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGuestStarSessionBegin_ParsesSessionBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.guest_star_session.begin", "version": "beta" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "session_id": "session-1"
          }
        }
        """;

        EventPayload<GuestStarSessionBegin> payload = DeserializePayload<GuestStarSessionBegin>(json);
        _ = payload.Event!.SessionId.Should().Be("session-1");
        _ = payload.Event.BroadcasterUserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGuestStarSessionEnd_ParsesSessionEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.guest_star_session.end", "version": "beta" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "session_id": "session-1"
          }
        }
        """;

        EventPayload<GuestStarSessionEnd> payload = DeserializePayload<GuestStarSessionEnd>(json);
        _ = payload.Event!.SessionId.Should().Be("session-1");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelGuestStarSettingsUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.guest_star_settings.update", "version": "beta" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "is_moderator_send_live_enabled": true,
            "slot_count": 4,
            "is_browser_source_audio_enabled": true,
            "layout": "tiled"
          }
        }
        """;

        EventPayload<GuestStarSettingsUpdate> payload = DeserializePayload<GuestStarSettingsUpdate>(json);
        _ = payload.Event!.IsModeratorSendLiveEnabled.Should().BeTrue();
        _ = payload.Event.SlotCount.Should().Be(4);
    }

    #endregion Channel Guest Star Payloads

    #region Channel Hype Train Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelHypeTrainBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.hype_train.begin", "version": "2" },
          "event": {
            "id": "ht-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "total": 1000,
            "progress": 500,
            "goal": 1000,
            "level": 1
          }
        }
        """;

        EventPayload<HypeTrainBegin> payload = DeserializePayload<HypeTrainBegin>(json);
        _ = payload.Event!.Id.Should().Be("ht-1");
        _ = payload.Event.Level.Should().Be(1);
        _ = payload.Event.Total.Should().Be(1000);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelHypeTrainEnd_ParsesEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.hype_train.end", "version": "2" },
          "event": {
            "id": "ht-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "level": 2,
            "total": 2500
          }
        }
        """;

        EventPayload<HypeTrainEnd> payload = DeserializePayload<HypeTrainEnd>(json);
        _ = payload.Event!.Id.Should().Be("ht-1");
        _ = payload.Event.Level.Should().Be(2);
        _ = payload.Event.Total.Should().Be(2500);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelHypeTrainProgress_ParsesProgressEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.hype_train.progress", "version": "2" },
          "event": {
            "id": "ht-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "level": 2,
            "total": 1500,
            "progress": 500,
            "goal": 1000
          }
        }
        """;

        EventPayload<HypeTrainProgress> payload = DeserializePayload<HypeTrainProgress>(json);
        _ = payload.Event!.ProgressAmount.Should().Be(500);
        _ = payload.Event!.Total.Should().Be(1500);
    }

    #endregion Channel Hype Train Payloads

    #region Channel Moderate & Moderator Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelModerate_ParsesModerateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.moderate", "version": "2" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "action": "ban"
          }
        }
        """;

        EventPayload<ChannelModerate> payload = DeserializePayload<ChannelModerate>(json);
        _ = payload.Event!.Action.Should().Be("ban");
        _ = payload.Event.ModeratorUserId.Should().Be("9001");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelModeratorAdd_ParsesAddEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.moderator.add", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "9001",
            "user_login": "newmod",
            "user_name": "NewMod"
          }
        }
        """;

        EventPayload<ModeratorAdd> payload = DeserializePayload<ModeratorAdd>(json);
        _ = payload.Event!.UserId.Should().Be("9001");
        _ = payload.Event.UserName.Should().Be("NewMod");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelModeratorRemove_ParsesRemoveEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.moderator.remove", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "9001",
            "user_login": "oldmod",
            "user_name": "OldMod"
          }
        }
        """;

        EventPayload<ModeratorRemove> payload = DeserializePayload<ModeratorRemove>(json);
        _ = payload.Event!.UserId.Should().Be("9001");
        _ = payload.Event.UserName.Should().Be("OldMod");
    }

    #endregion Channel Moderate & Moderator Payloads

    #region Channel Poll Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPollBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.poll.begin", "version": "1" },
          "event": {
            "id": "poll-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Best Game?",
            "choices": [
              { "id": "c1", "title": "Game 1" },
              { "id": "c2", "title": "Game 2" }
            ]
          }
        }
        """;

        EventPayload<PollBegin> payload = DeserializePayload<PollBegin>(json);
        _ = payload.Event!.Id.Should().Be("poll-1");
        _ = payload.Event.Title.Should().Be("Best Game?");
        _ = payload.Event.Choices.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPollEnd_ParsesEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.poll.end", "version": "1" },
          "event": {
            "id": "poll-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Best Game?",
            "status": "completed",
            "choices": [
              { "id": "c1", "title": "Game 1", "votes": 10 }
            ]
          }
        }
        """;

        EventPayload<PollEnd> payload = DeserializePayload<PollEnd>(json);
        _ = payload.Event!.Status.Should().Be(PollStatusEnum.Completed);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPollProgress_ParsesProgressEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.poll.progress", "version": "1" },
          "event": {
            "id": "poll-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Best Game?",
            "choices": [
              { "id": "c1", "title": "Game 1", "votes": 5 }
            ]
          }
        }
        """;

        EventPayload<PollProgress> payload = DeserializePayload<PollProgress>(json);
        _ = payload.Event!.Choices.Should().HaveCount(1);
    }

    #endregion Channel Poll Payloads

    #region Channel Prediction Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPredictionBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.prediction.begin", "version": "1" },
          "event": {
            "id": "pred-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Will we win?",
            "outcomes": [
              { "id": "out-1", "title": "Yes", "color": "blue" }
            ]
          }
        }
        """;

        EventPayload<PredictionBegin> payload = DeserializePayload<PredictionBegin>(json);
        _ = payload.Event!.Id.Should().Be("pred-1");
        _ = payload.Event.Title.Should().Be("Will we win?");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPredictionEnd_ParsesEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.prediction.end", "version": "1" },
          "event": {
            "id": "pred-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Will we win?",
            "winning_outcome_id": "out-1",
            "status": "resolved",
            "outcomes": [
              { "id": "out-1", "title": "Yes" }
            ]
          }
        }
        """;

        EventPayload<PredictionEnd> payload = DeserializePayload<PredictionEnd>(json);
        _ = payload.Event!.WinningOutcomeId.Should().Be("out-1");
        _ = payload.Event.Status.Should().Be(PredictionStatusEnum.Resolved);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPredictionLock_ParsesLockEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.prediction.lock", "version": "1" },
          "event": {
            "id": "pred-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Will we win?",
            "outcomes": [
              { "id": "out-1", "title": "Yes" }
            ]
          }
        }
        """;

        EventPayload<PredictionLock> payload = DeserializePayload<PredictionLock>(json);
        _ = payload.Event!.Id.Should().Be("pred-1");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelPredictionProgress_ParsesProgressEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.prediction.progress", "version": "1" },
          "event": {
            "id": "pred-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "title": "Will we win?",
            "outcomes": [
              { "id": "out-1", "title": "Yes" }
            ]
          }
        }
        """;

        EventPayload<PredictionProgress> payload = DeserializePayload<PredictionProgress>(json);
        _ = payload.Event!.Id.Should().Be("pred-1");
    }

    #endregion Channel Prediction Payloads

    #region Channel SharedChat & ShieldMode Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSharedChatBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shared_chat.begin", "version": "1" },
          "event": {
            "session_id": "sc-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "host_broadcaster_user_id": "1337",
            "host_broadcaster_user_login": "streamer",
            "host_broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<SharedChatBegin> payload = DeserializePayload<SharedChatBegin>(json);
        _ = payload.Event!.SessionId.Should().Be("sc-1");
        _ = payload.Event.HostBroadcasterUserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSharedChatEnd_ParsesEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shared_chat.end", "version": "1" },
          "event": {
            "session_id": "sc-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "host_broadcaster_user_id": "1337",
            "host_broadcaster_user_login": "streamer",
            "host_broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<SharedChatEnd> payload = DeserializePayload<SharedChatEnd>(json);
        _ = payload.Event!.SessionId.Should().Be("sc-1");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSharedChatUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shared_chat.update", "version": "1" },
          "event": {
            "session_id": "sc-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "host_broadcaster_user_id": "1337",
            "host_broadcaster_user_login": "streamer",
            "host_broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<SharedChatUpdate> payload = DeserializePayload<SharedChatUpdate>(json);
        _ = payload.Event!.SessionId.Should().Be("sc-1");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelShieldModeBegin_ParsesBeginEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shield_mode.begin", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod"
          }
        }
        """;

        EventPayload<ShieldModeBegin> payload = DeserializePayload<ShieldModeBegin>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
        _ = payload.Event.ModeratorUserId.Should().Be("9001");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelShieldModeEnd_ParsesEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shield_mode.end", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod"
          }
        }
        """;

        EventPayload<ShieldModeEnd> payload = DeserializePayload<ShieldModeEnd>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
    }

    #endregion Channel SharedChat & ShieldMode Payloads

    #region Channel Shoutout Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelShoutoutCreate_ParsesCreateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shoutout.create", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "to_broadcaster_user_id": "2001",
            "to_broadcaster_user_login": "shouted",
            "to_broadcaster_user_name": "Shouted",
            "viewer_count": 100
          }
        }
        """;

        EventPayload<ShoutoutCreate> payload = DeserializePayload<ShoutoutCreate>(json);
        _ = payload.Event!.ToBroadcasterUserId.Should().Be("2001");
        _ = payload.Event.ViewerCount.Should().Be(100);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelShoutoutReceive_ParsesReceiveEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.shoutout.receive", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "from_broadcaster_user_id": "2001",
            "from_broadcaster_user_login": "shouting",
            "from_broadcaster_user_name": "Shouting",
            "viewer_count": 100
          }
        }
        """;

        EventPayload<ShoutoutReceive> payload = DeserializePayload<ShoutoutReceive>(json);
        _ = payload.Event!.FromBroadcasterUserId.Should().Be("2001");
        _ = payload.Event.ViewerCount.Should().Be(100);
    }

    #endregion Channel Shoutout Payloads

    #region Channel Subscription Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSubscriptionGift_ParsesGiftEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.subscription.gift", "version": "1" },
          "event": {
            "user_id": "4242",
            "user_login": "gifter",
            "user_name": "Gifter",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "total": 5,
            "tier": "1000",
            "is_anonymous": false
          }
        }
        """;

        EventPayload<SubscriptionGift> payload = DeserializePayload<SubscriptionGift>(json);
        _ = payload.Event!.Total.Should().Be(5);
        _ = payload.Event.Tier.Should().Be("1000");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSubscriptionMessage_ParsesMessageEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.subscription.message", "version": "1" },
          "event": {
            "user_id": "4242",
            "user_login": "subber",
            "user_name": "Subber",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "tier": "1000",
            "cumulative_months": 6,
            "duration_months": 1,
            "streak_months": 6
          }
        }
        """;

        EventPayload<SubscriptionMessage> payload = DeserializePayload<SubscriptionMessage>(json);
        _ = payload.Event!.CumulativeMonths.Should().Be(6);
        _ = payload.Event.StreakMonths.Should().Be(6);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSubscriptionEnd_ParsesSubscriptionEndEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.subscription.end", "version": "1" },
          "event": {
            "user_id": "4242",
            "user_login": "subber",
            "user_name": "Subber",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "tier": "1000",
            "is_gift": false
          }
        }
        """;

        EventPayload<SubscriptionEnd> payload = DeserializePayload<SubscriptionEnd>(json);
        _ = payload.Event!.Tier.Should().Be("1000");
        _ = payload.Event.IsGift.Should().BeFalse();
    }

    #endregion Channel Subscription Payloads

    #region Channel SuspiciousUser & UnbanRequest Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSuspiciousUserMessage_ParsesMessageEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.suspicious_user.message", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "sususer",
            "user_name": "SusUser",
            "low_trust_status": "active_monitoring",
            "message_id": "sus-1"
          }
        }
        """;

        EventPayload<SuspiciousUserMessage> payload = DeserializePayload<SuspiciousUserMessage>(json);
        _ = payload.Event!.MessageId.Should().Be("sus-1");
        _ = payload.Event.LowTrustStatus.Should().Be(LowTrustStatusEnum.ActiveMonitoring);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelSuspiciousUserUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.suspicious_user.update", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "user_id": "4242",
            "user_login": "sususer",
            "user_name": "SusUser",
            "low_trust_status": "restricted"
          }
        }
        """;

        EventPayload<SuspiciousUserUpdate> payload = DeserializePayload<SuspiciousUserUpdate>(json);
        _ = payload.Event!.LowTrustStatus.Should().Be(LowTrustStatusEnum.Restricted);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelUnbanRequestCreate_ParsesCreateEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.unban_request.create", "version": "1" },
          "event": {
            "id": "ub-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "banneduser",
            "user_name": "BannedUser",
            "text": "Please unban me"
          }
        }
        """;

        EventPayload<UnbanRequestCreate> payload = DeserializePayload<UnbanRequestCreate>(json);
        _ = payload.Event!.Id.Should().Be("ub-1");
        _ = payload.Event.Text.Should().Be("Please unban me");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelUnbanRequestResolve_ParsesResolveEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.unban_request.resolve", "version": "1" },
          "event": {
            "id": "ub-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_id": "9001",
            "moderator_login": "mod",
            "moderator_name": "Mod",
            "user_id": "4242",
            "user_login": "banneduser",
            "user_name": "BannedUser",
            "resolution_text": "Unbanned",
            "status": "approved"
          }
        }
        """;

        EventPayload<UnbanRequestResolve> payload = DeserializePayload<UnbanRequestResolve>(json);
        _ = payload.Event!.Id.Should().Be("ub-1");
        _ = payload.Event.Status.Should().Be("approved");
    }

    #endregion Channel SuspiciousUser & UnbanRequest Payloads

    #region Channel VIP & Warning Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelVipAdd_ParsesAddEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.vip.add", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "vipuser",
            "user_name": "VipUser"
          }
        }
        """;

        EventPayload<VipAdd> payload = DeserializePayload<VipAdd>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.UserName.Should().Be("VipUser");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelVipRemove_ParsesRemoveEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.vip.remove", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "exvip",
            "user_name": "ExVip"
          }
        }
        """;

        EventPayload<VipRemove> payload = DeserializePayload<VipRemove>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.UserName.Should().Be("ExVip");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelWarningAcknowledge_ParsesAcknowledgeEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.warning.acknowledge", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "warneduser",
            "user_name": "WarnedUser"
          }
        }
        """;

        EventPayload<WarningAcknowledge> payload = DeserializePayload<WarningAcknowledge>(json);
        _ = payload.Event!.UserId.Should().Be("4242");
        _ = payload.Event.UserName.Should().Be("WarnedUser");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ChannelWarningSend_ParsesSendEvent()
    {
        string json = """
        {
          "subscription": { "type": "channel.warning.send", "version": "1" },
          "event": {
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "moderator_user_id": "9001",
            "moderator_user_login": "mod",
            "moderator_user_name": "Mod",
            "user_id": "4242",
            "user_login": "warneduser",
            "user_name": "WarnedUser",
            "reason": "Be nice"
          }
        }
        """;

        EventPayload<WarningSend> payload = DeserializePayload<WarningSend>(json);
        _ = payload.Event!.Reason.Should().Be("Be nice");
        _ = payload.Event.ModeratorUserId.Should().Be("9001");
    }

    #endregion Channel VIP & Warning Payloads

    #region Conduit, Drop, Extension, Stream, User Payloads

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ConduitShardDisabled_ParsesDisabledEvent()
    {
        string json = """
        {
          "subscription": { "type": "conduit.shard.disabled", "version": "1" },
          "event": {
            "conduit_id": "conduit-1",
            "shard_id": "0",
            "status": "disabled"
          }
        }
        """;

        EventPayload<ConduitShardDisabled> payload = DeserializePayload<ConduitShardDisabled>(json);
        _ = payload.Event!.ConduitId.Should().Be("conduit-1");
        _ = payload.Event.ShardId.Should().Be("0");
        _ = payload.Event.Status.Should().Be("disabled");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_DropEntitlementGrant_ParsesGrantEvent()
    {
        string json = """
        {
          "subscription": { "type": "drop.entitlement.grant", "version": "1" },
          "event": {
            "id": "grant-1",
            "data": [
              {
                "id": "ent-1",
                "benefit_id": "b-1",
                "user_id": "4242",
                "user_name": "User",
                "user_login": "user"
              }
            ]
          }
        }
        """;

        EventPayload<DropEntitlementGrant> payload = DeserializePayload<DropEntitlementGrant>(json);
        _ = payload.Event!.Id.Should().Be("grant-1");
        _ = payload.Event.Data.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_ExtensionBitsTransactionCreate_ParsesCreateEvent()
    {
        string json = """
        {
          "subscription": { "type": "extension.bits_transaction.create", "version": "1" },
          "event": {
            "id": "trans-1",
            "extension_client_id": "ext-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "user_id": "4242",
            "user_login": "buyer",
            "user_name": "Buyer",
            "product": {
              "sku": "item_1",
              "cost": { "amount": 100, "type": "bits" },
              "in_development": false,
              "name": "Magic Item"
            }
          }
        }
        """;

        EventPayload<BitsTransactionCreate> payload = DeserializePayload<BitsTransactionCreate>(json);
        _ = payload.Event!.Id.Should().Be("trans-1");
        _ = payload.Event.ExtensionClientId.Should().Be("ext-1");
        _ = payload.Event.Product!.Name.Should().Be("Magic Item");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_StreamOffline_ParsesOfflineEvent()
    {
        string json = """
        {
          "subscription": { "type": "stream.offline", "version": "1" },
          "event": {
            "id": "stream-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer"
          }
        }
        """;

        EventPayload<StreamOffline> payload = DeserializePayload<StreamOffline>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_StreamOnline_ParsesOnlineEvent()
    {
        string json = """
        {
          "subscription": { "type": "stream.online", "version": "1" },
          "event": {
            "id": "stream-1",
            "broadcaster_user_id": "1337",
            "broadcaster_user_login": "streamer",
            "broadcaster_user_name": "Streamer",
            "type": "live",
            "started_at": "2023-01-01T00:00:00Z"
          }
        }
        """;

        EventPayload<StreamOnline> payload = DeserializePayload<StreamOnline>(json);
        _ = payload.Event!.BroadcasterUserId.Should().Be("1337");
        _ = payload.Event.TypeOfStream.Should().Be(StreamOnline.StreamType.Live);
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_UserUpdate_ParsesUpdateEvent()
    {
        string json = """
        {
          "subscription": { "type": "user.update", "version": "1" },
          "event": {
            "user_id": "1337",
            "user_login": "streamer",
            "user_name": "Streamer",
            "email": "user@example.com",
            "email_verified": true,
            "description": "Awesome channel"
          }
        }
        """;

        EventPayload<UserUpdate> payload = DeserializePayload<UserUpdate>(json);
        _ = payload.Event!.UserId.Should().Be("1337");
        _ = payload.Event.Email.Should().Be("user@example.com");
        _ = payload.Event.EmailVerified.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_UserAuthorizationGrant_ParsesGrantEvent()
    {
        string json = """
        {
          "subscription": { "type": "user.authorization.grant", "version": "1" },
          "event": {
            "client_id": "app-client-id",
            "user_id": "1337",
            "user_login": "streamer",
            "user_name": "Streamer"
          }
        }
        """;

        EventPayload<UserAuthGrant> payload = DeserializePayload<UserAuthGrant>(json);
        _ = payload.Event!.ClientId.Should().Be("app-client-id");
        _ = payload.Event.UserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_UserAuthorizationRevoke_ParsesRevokeEvent()
    {
        string json = """
        {
          "subscription": { "type": "user.authorization.revoke", "version": "1" },
          "event": {
            "client_id": "app-client-id",
            "user_id": "1337",
            "user_login": "streamer",
            "user_name": "Streamer"
          }
        }
        """;

        EventPayload<UserAuthRevoke> payload = DeserializePayload<UserAuthRevoke>(json);
        _ = payload.Event!.ClientId.Should().Be("app-client-id");
        _ = payload.Event.UserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "DeserializePayload")]
    public void Deserialize_UserWhisperMessage_ParsesMessageEvent()
    {
        string json = """
        {
          "subscription": { "type": "user.whisper.message", "version": "1" },
          "event": {
            "from_user_id": "4242",
            "from_user_login": "sender",
            "from_user_name": "Sender",
            "to_user_id": "1337",
            "to_user_login": "recipient",
            "to_user_name": "Recipient",
            "whisper_id": "whisp-1",
            "whisper": { "text": "Secret message" }
          }
        }
        """;

        EventPayload<UserWhisperMessage> payload = DeserializePayload<UserWhisperMessage>(json);
        _ = payload.Event!.WhisperId.Should().Be("whisp-1");
        _ = payload.Event.FromUserId.Should().Be("4242");
        _ = payload.Event.ToUserId.Should().Be("1337");
    }

    #endregion Conduit, Drop, Extension, Stream, User Payloads

    #endregion DeserializePayload

    #region Private Helper Methods

    /// <summary>
    /// Deserializes a raw JSON string into an <see cref="EventPayload{TEventType}"/> and validates that the event payload is not null.
    /// </summary>
    /// <typeparam name="TEventType">The event subtype.</typeparam>
    /// <param name="json">The JSON payload string.</param>
    /// <returns>The deserialized <see cref="EventPayload{TEventType}"/> instance.</returns>
    private static EventPayload<TEventType> DeserializePayload<TEventType>(string json) where TEventType : IEventSubType
    {
        EventPayload<TEventType>? payload = JsonSerializer.Deserialize<EventPayload<TEventType>>(json, TwitchApi.SerializerOptions);
        _ = payload.Should().NotBeNull();
        _ = payload.Event.Should().NotBeNull();
        _ = payload.Event.Should().BeOfType<TEventType>();
        return payload;
    }

    #endregion Private Helper Methods
}

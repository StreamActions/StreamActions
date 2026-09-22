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

using StreamActions.Common.Json.Serialization;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// A notification for when an event that appears in chat has occurred.
/// </summary>
public sealed record Notification : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.chat.notification";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The broadcaster user ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The user ID of the user that sent the message.
    /// </summary>
    [JsonPropertyName("chatter_user_id")]
    public string? ChatterUserId { get; init; }

    /// <summary>
    /// The user login of the user that sent the message.
    /// </summary>
    [JsonPropertyName("chatter_user_login")]
    public string? ChatterUserLogin { get; init; }

    /// <summary>
    /// The user name of the user that sent the message.
    /// </summary>
    [JsonPropertyName("chatter_user_name")]
    public string? ChatterUserName { get; init; }

    /// <summary>
    /// Whether or not the chatter is anonymous.
    /// </summary>
    [JsonPropertyName("chatter_is_anonymous")]
    public bool? ChatterIsAnonymous { get; init; }

    /// <summary>
    /// The color of the user's name in the chat room.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>
    /// The color of the user's name in the chat room.
    /// </summary>
    [JsonPropertyName("badges")]
    public IEnumerable<Badge>? Badges { get; init; }

    /// <summary>
    /// The message Twitch shows in the chat room for this notice.
    /// </summary>
    [JsonPropertyName("system_message")]
    public string? SystemMessage { get; init; }

    /// <summary>
    /// A UUID that identifies the message.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    /// The structured chat message.
    /// </summary>
    [JsonPropertyName("message")]
    public Common.ChatMessage.Message? Message { get; init; }

    /// <summary>
    /// The type of notice.
    /// </summary>
    [JsonPropertyName("notice_type")]
    public NoticeType? TypeOfNotice { get; init; }

    /// <summary>
    /// Information about the sub event. Null if notice_type is not sub.
    /// </summary>
    [JsonPropertyName("sub")]
    public Subscription? Sub { get; init; }

    /// <summary>
    /// Information about the resub event. Null if notice_type is not resub.
    /// </summary>
    [JsonPropertyName("resub")]
    public Resub? Resub { get; init; }

    /// <summary>
    /// Information about the gift sub event. Null if notice_type is not sub_gift.
    /// </summary>
    [JsonPropertyName("sub_gift")]
    public SubGift? SubGift { get; init; }

    /// <summary>
    /// Information about the community gift sub event. Null if notice_type is not community_sub_gift.
    /// </summary>
    [JsonPropertyName("community_sub_gift")]
    public CommunitySubGift? CommunitySubGift { get; init; }

    /// <summary>
    /// Information about the community gift paid upgrade event. Null if notice_type is not gift_paid_upgrade.
    /// </summary>
    [JsonPropertyName("gift_paid_upgrade")]
    public GiftPaidUpgrade? GiftPaidUpgrade { get; init; }

    /// <summary>
    /// Information about the Prime gift paid upgrade event. Null if notice_type is not prime_paid_upgrade.
    /// </summary>
    [JsonPropertyName("prime_paid_upgrade")]
    public PrimePaidUpgrade? PrimePaidUpgrade { get; init; }

    /// <summary>
    /// Information about the pay it forward event. Null if notice_type is not pay_it_forward.
    /// </summary>
    [JsonPropertyName("pay_it_forward")]
    public PayItForward? PayItForward { get; init; }

    /// <summary>
    /// Information about the raid event. Null if notice_type is not raid.
    /// </summary>
    [JsonPropertyName("raid")]
    public Raid? Raid { get; init; }

    /// <summary>
    /// Returns an empty payload if notice_type is not unraid, otherwise returns null.
    /// </summary>
    [JsonPropertyName("unraid")]
    public Unraid? Unraid { get; init; }

    /// <summary>
    /// Information about the announcement event. Null if notice_type is not announcement.
    /// </summary>
    [JsonPropertyName("announcement")]
    public Announcement? Announcement { get; init; }

    /// <summary>
    /// Information about the Bits badge tier event. Null if notice_type is not bits_badge_tier.
    /// </summary>
    [JsonPropertyName("bits_badge_tier")]
    public BitsBadgeTier? BitsBadgeTier { get; init; }

    /// <summary>
    /// Information about the announcement event. Null if notice_type is not charity_donation.
    /// </summary>
    [JsonPropertyName("charity_donation")]
    public CharityDonation? CharityDonation { get; init; }

    /// <summary>
    /// Information about the Watch Streak event. Null if notice_type is not watch_streak.
    /// </summary>
    [JsonPropertyName("watch_streak")]
    public WatchStreak? WatchStreak { get; init; }

    /// <summary>
    /// Information about the modiversary event. Null if notice_type is not modiversary.
    /// </summary>
    [JsonPropertyName("modiversary")]
    public Modiversary? Modiversary { get; init; }

    /// <summary>
    /// Optional. The broadcaster user ID of the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_id")]
    public string? SourceBroadcasterUserId { get; init; }

    /// <summary>
    /// Optional. The login of the broadcaster of the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_login")]
    public string? SourceBroadcasterUserLogin { get; init; }

    /// <summary>
    /// Optional. The user name of the broadcaster of the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_name")]
    public string? SourceBroadcasterUserName { get; init; }

    /// <summary>
    /// Optional. The UUID that identifies the source message from the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_message_id")]
    public string? SourceMessageId { get; init; }

    /// <summary>
    /// Optional. The list of chat badges for the chatter in the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_badges")]
    public IEnumerable<Badge>? SourceBadges { get; init; }

    /// <summary>
    /// Optional. Whether the notification is only sent to the source channel.
    /// </summary>
    [JsonPropertyName("is_source_only")]
    public bool? IsSourceOnly { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_sub event.
    /// </summary>
    [JsonPropertyName("shared_chat_sub")]
    public Subscription? SharedChatSub { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_resub event.
    /// </summary>
    [JsonPropertyName("shared_chat_resub")]
    public Resub? SharedChatResub { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_sub_gift event.
    /// </summary>
    [JsonPropertyName("shared_chat_sub_gift")]
    public SubGift? SharedChatSubGift { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_community_sub_gift event.
    /// </summary>
    [JsonPropertyName("shared_chat_community_sub_gift")]
    public CommunitySubGift? SharedChatCommunitySubGift { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_gift_paid_upgrade event.
    /// </summary>
    [JsonPropertyName("shared_chat_gift_paid_upgrade")]
    public GiftPaidUpgrade? SharedChatGiftPaidUpgrade { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_prime_paid_upgrade event.
    /// </summary>
    [JsonPropertyName("shared_chat_prime_paid_upgrade")]
    public PrimePaidUpgrade? SharedChatPrimePaidUpgrade { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_pay_it_forward event.
    /// </summary>
    [JsonPropertyName("shared_chat_pay_it_forward")]
    public PayItForward? SharedChatPayItForward { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_raid event.
    /// </summary>
    [JsonPropertyName("shared_chat_raid")]
    public Raid? SharedChatRaid { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_announcement event.
    /// </summary>
    [JsonPropertyName("shared_chat_announcement")]
    public Announcement? SharedChatAnnouncement { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_modiversary event.
    /// </summary>
    [JsonPropertyName("shared_chat_modiversary")]
    public Modiversary? SharedChatModiversary { get; init; }

    /// <summary>
    /// The type of notice.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<NoticeType>))]
    public enum NoticeType
    {
        /// <summary>
        /// Sub.
        /// </summary>
        [JsonCustomEnum("sub")]
        Sub,

        /// <summary>
        /// Resub.
        /// </summary>
        [JsonCustomEnum("resub")]
        Resub,

        /// <summary>
        /// Sub gift.
        /// </summary>
        [JsonCustomEnum("sub_gift")]
        SubGift,

        /// <summary>
        /// Community sub gift.
        /// </summary>
        [JsonCustomEnum("community_sub_gift")]
        CommunitySubGift,

        /// <summary>
        /// Gift paid upgrade.
        /// </summary>
        [JsonCustomEnum("gift_paid_upgrade")]
        GiftPaidUpgrade,

        /// <summary>
        /// Prime paid upgrade.
        /// </summary>
        [JsonCustomEnum("prime_paid_upgrade")]
        PrimePaidUpgrade,

        /// <summary>
        /// Raid.
        /// </summary>
        [JsonCustomEnum("raid")]
        Raid,

        /// <summary>
        /// Unraid.
        /// </summary>
        [JsonCustomEnum("unraid")]
        Unraid,

        /// <summary>
        /// Pay it forward.
        /// </summary>
        [JsonCustomEnum("pay_it_forward")]
        PayItForward,

        /// <summary>
        /// Announcement.
        /// </summary>
        [JsonCustomEnum("announcement")]
        Announcement,

        /// <summary>
        /// Bits badge tier.
        /// </summary>
        [JsonCustomEnum("bits_badge_tier")]
        BitsBadgeTier,

        /// <summary>
        /// Charity donation.
        /// </summary>
        [JsonCustomEnum("charity_donation")]
        CharityDonation,

        /// <summary>
        /// Watch streak.
        /// </summary>
        [JsonCustomEnum("watch_streak")]
        WatchStreak,

        /// <summary>
        /// Modiversary.
        /// </summary>
        [JsonCustomEnum("modiversary")]
        Modiversary,

        /// <summary>
        /// Shared chat sub.
        /// </summary>
        [JsonCustomEnum("shared_chat_sub")]
        SharedChatSub,

        /// <summary>
        /// Shared chat resub.
        /// </summary>
        [JsonCustomEnum("shared_chat_resub")]
        SharedChatResub,

        /// <summary>
        /// Shared chat sub gift.
        /// </summary>
        [JsonCustomEnum("shared_chat_sub_gift")]
        SharedChatSubGift,

        /// <summary>
        /// Shared chat community sub gift.
        /// </summary>
        [JsonCustomEnum("shared_chat_community_sub_gift")]
        SharedChatCommunitySubGift,

        /// <summary>
        /// Shared chat gift paid upgrade.
        /// </summary>
        [JsonCustomEnum("shared_chat_gift_paid_upgrade")]
        SharedChatGiftPaidUpgrade,

        /// <summary>
        /// Shared chat prime paid upgrade.
        /// </summary>
        [JsonCustomEnum("shared_chat_prime_paid_upgrade")]
        SharedChatPrimePaidUpgrade,

        /// <summary>
        /// Shared chat raid.
        /// </summary>
        [JsonCustomEnum("shared_chat_raid")]
        SharedChatRaid,

        /// <summary>
        /// Shared chat pay it forward.
        /// </summary>
        [JsonCustomEnum("shared_chat_pay_it_forward")]
        SharedChatPayItForward,

        /// <summary>
        /// Shared chat announcement.
        /// </summary>
        [JsonCustomEnum("shared_chat_announcement")]
        SharedChatAnnouncement,

        /// <summary>
        /// Shared chat modiversary.
        /// </summary>
        [JsonCustomEnum("shared_chat_modiversary")]
        SharedChatModiversary,

        /// <summary>
        /// Unknown.
        /// </summary>
        [JsonCustomEnum("unknown")]
        Unknown
    }
}

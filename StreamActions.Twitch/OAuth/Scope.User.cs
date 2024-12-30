/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.OAuth;

public sealed partial record Scope
{
    /// <summary>
    /// Join a specified chat channel as your user and appear as a bot, and perform chat-related actions as your user.
    /// </summary>
    /// <remarks>
    /// This scope is used on a Twitch user to allow access to act on their behalf as a registered bot with the EventSub Chat subscriptions and sending chat messages when using an App Access Token.
    /// </remarks>
    /// <value>user:bot</value>
    public static readonly Scope UserBot = new("user:bot", "Join a specified chat channel as your user and appear as a bot, and perform chat-related actions as your user.");

    /// <summary>
    /// Manage a user object.
    /// </summary>
    /// <value>user:edit</value>
    public static readonly Scope UserEdit = new("user:edit", "Manage a user object.");

    /// <summary>
    /// View and edit a user's broadcasting configuration, including Extension configurations.
    /// </summary>
    /// <value>user:edit:broadcast</value>
    public static readonly Scope UserEditBroadcast = new("user:edit:broadcast", "View and edit a user's broadcasting configuration, including Extension configurations.");

    /// <summary>
    /// Manage the block list of a user.
    /// </summary>
    /// <value>user:manage:blocked_users</value>
    public static readonly Scope UserManageBlockedUsers = new("user:manage:blocked_users", "Manage the block list of a user.");

    /// <summary>
    /// Update the color used for the user's name in chat.
    /// </summary>
    /// <value>user:manage:chat_color</value>
    public static readonly Scope UserManageChatColor = new("user:manage:chat_color", "Update the color used for the user's name in chat.");

    /// <summary>
    /// Receive whispers sent to your user, and send whispers on your user's behalf.
    /// </summary>
    /// <value>user:manage:whispers</value>
    public static readonly Scope UserManageWhispers = new("user:manage:whispers", "Receive whispers sent to your user, and send whispers on your user's behalf.");

    /// <summary>
    /// View the block list of a user.
    /// </summary>
    /// <value>user:read:blocked_users</value>
    public static readonly Scope UserReadBlockedUsers = new("user:read:blocked_users", "View the block list of a user.");

    /// <summary>
    /// View a user's broadcasting configuration, including Extension configurations.
    /// </summary>
    /// <value>user:read:broadcast</value>
    public static readonly Scope UserReadBroadcast = new("user:read:broadcast", "View a user's broadcasting configuration, including Extension configurations.");

    /// <summary>
    /// Receive chatroom messages and informational notifications relating to a channel's chatroom.
    /// </summary>
    /// <value>user:read:chat</value>
    public static readonly Scope UserReadChat = new("user:read:chat", "Receive chatroom messages and informational notifications relating to a channel's chatroom.");

    /// <summary>
    /// View a user's email address.
    /// </summary>
    /// <value>user:read:email</value>
    public static readonly Scope UserReadEmail = new("user:read:email", "View a user's email address.");

    /// <summary>
    /// View emotes available to a user.
    /// </summary>
    /// <value>user:read:emotes</value>
    public static readonly Scope UserReadEmotes = new("user:read:emotes", "View emotes available to a user.");

    /// <summary>
    /// View the list of channels a user follows.
    /// </summary>
    /// <value>user:read:follows</value>
    public static readonly Scope UserReadFollows = new("user:read:follows", "View the list of channels a user follows.");

    /// <summary>
    /// Read the list of channels you have moderator privileges in.
    /// </summary>
    /// <value>user:read:moderated_channels</value>
    public static readonly Scope UserReadModeratedChannels = new("user:read:moderated_channels", "Read the list of channels you have moderator privileges in.");

    /// <summary>
    /// View if an authorized user is subscribed to specific channels.
    /// </summary>
    /// <value>user:read:subscriptions</value>
    public static readonly Scope UserReadSubscriptions = new("user:read:subscriptions", "View if an authorized user is subscribed to specific channels.");

    /// <summary>
    /// Receive whispers sent to your user.
    /// </summary>
    /// <value>user:read:whispers</value>
    public static readonly Scope UserReadWhispers = new("user:read:whispers", "Receive whispers sent to your user.");

    /// <summary>
    /// Send chat messages to a chatroom.
    /// </summary>
    /// <value>user:write:chat</value>
    public static readonly Scope UserWriteChat = new("user:write:chat", "Send chat messages to a chatroom.");
}

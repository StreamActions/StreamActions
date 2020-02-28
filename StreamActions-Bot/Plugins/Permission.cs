/*
 * Copyright © 2019-2020 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using MongoDB.Driver;
using StreamActions.Attributes;
using StreamActions.Database;
using StreamActions.Database.Documents;
using StreamActions.Database.Documents.Permissions;
using StreamActions.Database.Documents.Users;
using StreamActions.Enums;
using StreamActions.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles permissions.
    /// </summary>
    [Guid("93149A5E-54D4-4EC2-AF18-D32602C12BFE")]
    public class Permission : IPlugin
    {
        #region Public Properties

        /// <summary>
        /// A Dictionary of registered permissions. Key is permission name; value is description.
        /// </summary>
        public static Dictionary<string, string> RegisteredPermissions
        {
            get
            {
                Dictionary<string, string> value = new Dictionary<string, string>();

                foreach (KeyValuePair<string, string> kvp in _registeredPermissions)
                {
                    value.Add(kvp.Key, kvp.Value);
                }

                return value;
            }
        }

        public bool AlwaysEnabled => true;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Handles user permissions";

        public Guid PluginId => typeof(Permission).GUID;
        public string PluginName => "Permissions";
        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions-Bot");
        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Determines if the sender of a chat command has the permissions to use the command.
        /// </summary>
        /// <param name="command">The <see cref="ChatCommand"/> to check.</param>
        /// <param name="shouldUseCommandText"><c>true</c> to use the <see cref="ChatCommand.CommandText"/> in the command name to check.</param>
        /// <param name="numArgumentsToUse">And integer indicating the number of arguments from <see cref="ChatCommand.ArgumentsAsList"/> to append to the command name to check.</param>
        /// <returns><c>true</c> if the user has permission; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.</exception>
        public static async Task<bool> Can(ChatCommand command, bool shouldUseCommandText = true, int numArgumentsToUse = 0)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            string cmdText = shouldUseCommandText ? command.CommandText : "";
            if (numArgumentsToUse > 0)
            {
                for (int i = 0; i < numArgumentsToUse && i < command.ArgumentsAsList.Count; i++)
                {
                    if (cmdText.Length > 0)
                    {
                        cmdText += " ";
                    }

                    cmdText += command.ArgumentsAsList[i];
                }
            }

            cmdText = cmdText.ToLowerInvariant();

            IMongoCollection<CommandDocument> commands = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == command.ChatMessage.RoomId && c.Command == cmdText);

            using IAsyncCursor<CommandDocument> cursor = await commands.FindAsync(filter).ConfigureAwait(false);

            return await Can(command.ChatMessage.UserId, (await cursor.FirstAsync().ConfigureAwait(false)).UserLevel, "can_" + cmdText.Replace(' ', '_')).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if the specified user has permission to perform the specified action.
        /// </summary>
        /// <param name="userId">The userId to check.</param>
        /// <param name="userLevels">The <see cref="UserLevels"/> allowed to perform this action. Include <see cref="UserLevels.Custom"/> and <paramref name="permissionName"/> to also check permission groups.</param>
        /// <param name="permissionName">The permission name to check in the permission groups, if the <see cref="UserLevels.Custom"/> flag was set in <paramref name="userLevels"/>.</param>
        /// <returns><c>true</c> if the user has permission; <c>false</c> otherwise.</returns>
        public static async Task<bool> Can(string userId, UserLevels userLevels, string permissionName = null)
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return false;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            UserDocument userDocument = await cursor.SingleAsync().ConfigureAwait(false);

            if (userDocument.BotGlobalPermission == BotGlobal.Banned)
            {
                return false;
            }
            else if (userDocument.BotGlobalPermission == BotGlobal.SuperAdmin || (userDocument.UserLevels & UserLevels.Broadcaster) == UserLevels.Broadcaster
                || (userLevels & UserLevels.Viewer) == UserLevels.Viewer)
            {
                return true;
            }

            if (userLevels != UserLevels.Custom)
            {
                foreach (UserLevels level in Enum.GetValues(typeof(UserLevels)))
                {
                    if ((userDocument.UserLevels & level) == level)
                    {
                        switch (level)
                        {
                            case UserLevels.TwitchStaff:
                                if (userLevels >= UserLevels.TwitchStaff)
                                {
                                    return true;
                                }
                                break;

                            case UserLevels.TwitchAdmin:
                                if (userLevels >= UserLevels.TwitchAdmin)
                                {
                                    return true;
                                }
                                break;

                            case UserLevels.Moderator:
                                if (userLevels >= UserLevels.Moderator)
                                {
                                    return true;
                                }
                                break;

                            case UserLevels.VIP:
                                if ((userLevels & UserLevels.VIP) == UserLevels.VIP)
                                {
                                    return true;
                                }
                                break;

                            case UserLevels.Subscriber:
                                if ((userLevels & UserLevels.Subscriber) == UserLevels.Subscriber)
                                {
                                    return true;
                                }
                                break;
                        }
                    }
                }
            }

            return (userLevels & UserLevels.Custom) == UserLevels.Custom && !string.IsNullOrWhiteSpace(permissionName)
                ? await HasPermission(userDocument.PermissionGroupMembership, permissionName.Replace(' ', '_').ToLowerInvariant()).ConfigureAwait(false)
                : false;
        }

        /// <summary>
        /// Looks up a permission group's Guid.
        /// </summary>
        /// <param name="channelId">The channelId to look in.</param>
        /// <param name="groupName">The permission group name to look for.</param>
        /// <returns>The Guid, if found; <c>Guid.Empty</c> otherwise.</returns>
        public static async Task<Guid> GetGroupGuidAsync(string channelId, string groupName)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.ChannelId == channelId && d.GroupName == groupName);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return Guid.Empty;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            return (await cursor.SingleAsync().ConfigureAwait(false)).Id;
        }

        /// <summary>
        /// Determines if permission is granted, from a set of permission groups.
        /// </summary>
        /// <param name="permissionGroups">A List of group Guid to check.</param>
        /// <param name="permissionName">The permission name to check.</param>
        /// <returns><c>true</c> if at least one group permits the permission and none deny it; <c>false</c> otherwise.</returns>
        public static async Task<bool> HasPermission(List<Guid> permissionGroups, string permissionName)
        {
            if (string.IsNullOrWhiteSpace(permissionName))
            {
                return false;
            }

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => permissionGroups.Contains(d.Id));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return false;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            bool hasPermission = false;
            bool isDenied = false;

            await cursor.ForEachAsync(d =>
            {
                if (d.Permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) && p.IsDenied == false))
                {
                    hasPermission = true;
                }
                else if (d.Permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) && p.IsDenied == true))
                {
                    isDenied = true;
                }
            }).ConfigureAwait(false);

            return hasPermission && !isDenied;
        }

        /// <summary>
        /// Attempts to register a permission.
        /// </summary>
        /// <param name="permissionName">The name to register. All whitespace is replaced with an underscore (<c>_</c>).</param>
        /// <param name="description">The description of the permission.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="permissionName"/> is <c>null</c>.</exception>
        public static bool TryRegisterPermission(string permissionName, string description)
        {
            if (string.IsNullOrWhiteSpace(permissionName))
            {
                throw new ArgumentNullException(nameof(permissionName));
            }

            return _registeredPermissions.TryAdd(permissionName.Replace(' ', '_').ToLowerInvariant(), description);
        }

        /// <summary>
        /// Attempts to unregister a permission and, on success, remove it from all PermissionGroups.
        /// </summary>
        /// <param name="permissionName">The name to unregister.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="permissionName"/> is <c>null</c>.</exception>
        public static bool TryUnregisterPermission(string permissionName)
        {
            if (string.IsNullOrWhiteSpace(permissionName))
            {
                throw new ArgumentNullException(nameof(permissionName));
            }

            bool result = _registeredPermissions.TryRemove(permissionName.Replace(' ', '_').ToLowerInvariant(), out _);

            if (result)
            {
                RemovePermissionFromAllGroups(permissionName.Replace(' ', '_').ToLowerInvariant());
            }

            return result;
        }

        public void Disabled()
        {
        }

        public void Enabled()
        {
        }

        public string GetCursor() => this.PluginId.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Adds or updates a permission to the specified permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="permissionName">The permission to add or update.</param>
        /// <param name="isDenied">Whether the permission is being explicitly denied.</param>
        internal static async void AddOrUpdatePermissionToGroup(Guid groupId, string permissionName, bool isDenied = false)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            if (!permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(new PermissionDocument { PermissionName = permissionName.Replace(' ', '_').ToLowerInvariant(), IsDenied = isDenied });
            }
            else
            {
                permissions.Find(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)).IsDenied = isDenied;
            }

            UpdateDefinition<PermissionGroupDocument> update = Builders<PermissionGroupDocument>.Update.Set(d => d.Permissions, permissions);

            _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new permission group. Fails silently if the group name is already in use (case insensitive).
        /// </summary>
        /// <param name="channelId">The channelId to create the group on.</param>
        /// <param name="groupName">The name of the new group.</param>
        internal static async void AddPermissionGroup(string channelId, string groupName)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            if (!(await GetGroupGuidAsync(channelId, groupName).ConfigureAwait(false)).Equals(Guid.Empty))
            {
                return;
            }

            await collection.InsertOneAsync(new PermissionGroupDocument { ChannelId = channelId, GroupName = groupName }).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a permission group to the specified user's memberships.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="userId">The user to add the membership to.</param>
        internal static async void AddPermissionGroupToUser(Guid groupId, string userId)
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<Guid> permissionGroups = (await cursor.SingleAsync().ConfigureAwait(false)).PermissionGroupMembership;

            if (!permissionGroups.Exists(p => p.Equals(groupId)))
            {
                permissionGroups.Add(groupId);

                UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update.Set(d => d.PermissionGroupMembership, permissionGroups);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes the specified permission from all permission groups.
        /// </summary>
        /// <param name="permissionName">The permission to remove.</param>
        internal static async void RemovePermissionFromAllGroups(string permissionName)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            await cursor.ForEachAsync(d => RemovePermissionFromGroup(d.Id, permissionName.Replace(' ', '_').ToLowerInvariant())).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes the specified permission from the specified permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="permissionName">The permission to remove.</param>
        internal static async void RemovePermissionFromGroup(Guid groupId, string permissionName)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            if (permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)))
            {
                _ = permissions.RemoveAll(d => d.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));

                UpdateDefinition<PermissionGroupDocument> update = Builders<PermissionGroupDocument>.Update.Set(d => d.Permissions, permissions);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deletes a permission group and removes all users from it's membership.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        internal static async void RemovePermissionGroup(Guid groupId)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            RemovePermissionGroupFromAllUsers(groupId);
            _ = await collection.DeleteOneAsync(filter).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes all users from the membership of a permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        internal static async void RemovePermissionGroupFromAllUsers(Guid groupId)
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("permissionGroups");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.PermissionGroupMembership.Contains(groupId));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            await cursor.ForEachAsync(d => RemovePermissionGroupFromUser(groupId, d.Id)).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes a user from the membership of a permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="userId">The userId who is being removed.</param>
        internal static async void RemovePermissionGroupFromUser(Guid groupId, string userId)
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<Guid> permissionGroups = (await cursor.SingleAsync().ConfigureAwait(false)).PermissionGroupMembership;

            if (permissionGroups.Exists(p => p.Equals(groupId)))
            {
                _ = permissionGroups.RemoveAll(d => d.Equals(groupId));

                UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update.Set(d => d.PermissionGroupMembership, permissionGroups);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="RegisteredPermissions"/>.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> _registeredPermissions = new ConcurrentDictionary<string, string>();

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Creates a new permission group in the channel.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void CreateGroup(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 3 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[3], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupCreateUsage", command.ChatMessage.RoomId,
                    new { BotName = Program.Settings.BotLogin },
                    "Creates a new custom permission group. Usage: !{BotName} permissions group create (GroupName)").ConfigureAwait(false));
                return;
            }

            AddPermissionGroup(command.ChatMessage.RoomId, groupName);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupCreateSuccess", command.ChatMessage.RoomId,
                new { Group = groupName },
                "The permission group {Group} has been created.").ConfigureAwait(false));
        }

        /// <summary>
        /// Allows a permission to users of the specified permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void GroupAllowPermission(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(command.ArgumentsAsList[3]) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupAllowUsage", command.ChatMessage.RoomId,
                    new { BotName = Program.Settings.BotLogin },
                    "Allows the specified permission in the custom permission group, can be overridden by a deny. Usage: !{BotName} permissions group allow (PermissionName) (GroupName)").ConfigureAwait(false));
                return;
            }

            AddOrUpdatePermissionToGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), command.ArgumentsAsList[3], false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupAllowSuccess", command.ChatMessage.RoomId,
                new { Group = groupName },
                "The permission {Permission} has been allowed to members of {Group}.").ConfigureAwait(false));
        }

        /// <summary>
        /// Explicitly denies a permission to users of the specified permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void GroupDenyPermission(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(command.ArgumentsAsList[3]) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupDenyUsage", command.ChatMessage.RoomId,
                    new { BotName = Program.Settings.BotLogin },
                    "Explicitly denies the specified permission in the custom permission group, overriding any allows that another group may provide. Usage: !{BotName} permissions group deny (PermissionName) (GroupName)").ConfigureAwait(false));
                return;
            }

            AddOrUpdatePermissionToGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), command.ArgumentsAsList[3], true);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupDenySuccess", command.ChatMessage.RoomId,
                new { Group = groupName },
                "The permission {Permission} has been explicitly denied to members of {Group}.").ConfigureAwait(false));
        }

        /// <summary>
        /// Sets the specified permission to be inherited in the permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void GroupInheritPermission(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(command.ArgumentsAsList[3]) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupInheritUsage", command.ChatMessage.RoomId,
                    new { BotName = Program.Settings.BotLogin },
                    "Implicitly denies the specified permission in the custom permission group, but another group can allow it. Usage: !{BotName} permissions group inherit (PermissionName) (GroupName)").ConfigureAwait(false));
                return;
            }

            RemovePermissionFromGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), command.ArgumentsAsList[3]);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupInheritSuccess", command.ChatMessage.RoomId,
                new { Group = groupName },
                "The permission {Permission} has been explicitly denied to members of {Group}.").ConfigureAwait(false));
        }

        /// <summary>
        /// Lists the current permissions of the specified permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void GroupListPermissions(ChatCommand command)
        {
            string permissionNames = "";
            int numPage = 1;
            string groupName;

            if (int.TryParse(command.ArgumentsAsList[3] ?? "1", NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false), out int page))
            {
                groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;
            }
            else
            {
                page = 1;
                groupName = command.ArgumentsAsList.Count > 3 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[3], StringComparison.Ordinal)) : null;
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupListPermissionsUsage", command.ChatMessage.RoomId,
                    new { BotName = Program.Settings.BotLogin },
                    "Lists the permissions that are allowed or explicitly denied by a custom group. Usage: !{BotName} permissions group listpermissions [page] (GroupName)").ConfigureAwait(false));
                return;
            }

            Guid groupId = await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false);

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            numPage = (int)Math.Ceiling(permissions.Count / 10.0);

            page = Math.Min(numPage, Math.Max(1, page));

            foreach (PermissionDocument permission in permissions.Skip((page - 1) * 10).Take(10))
            {
                if (permissionNames.Length > 0)
                {
                    permissionNames += ", ";
                }

                permissionNames += permission.PermissionName;

                if (permission.IsDenied)
                {
                    permissionNames += " (Denied)";
                }
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupListPermissions", command.ChatMessage.RoomId,
                new { Permissions = permissionNames, Group = groupName, Page = page, NumPage = numPage },
                "Permissions of group {Group} [Page {Page} of {NumPage}]: {Permissions}").ConfigureAwait(false));
        }

        /// <summary>
        /// Lists the permission groups of the channel.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void ListGroups(ChatCommand command)
        {
            string groupNames = "";
            int page = 1;
            int numPage = 1;

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            long numGroups = await collection.CountDocumentsAsync(filter).ConfigureAwait(false);
            numPage = (int)Math.Ceiling(numGroups / 10.0);

            page = Math.Min(numPage, Math.Max(1, int.Parse(command.ArgumentsAsList[3] ?? "1", NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false))));

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionGroupDocument> permissionGroups = await cursor.ToListAsync().ConfigureAwait(false);

            foreach (PermissionGroupDocument permissionGroup in permissionGroups.Skip((page - 1) * 10).Take(10))
            {
                if (groupNames.Length > 0)
                {
                    groupNames += ", ";
                }

                groupNames += permissionGroup.GroupName;
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupList", command.ChatMessage.RoomId,
                new { Groups = groupNames, Page = page, NumPage = numPage },
                "Permission groups [Page {Page} of {NumPage}]: {Groups}").ConfigureAwait(false));
        }

        /// <summary>
        /// Permission group management command. Allows CRUD of groups and their permissions.
        /// </summary>
        /// <param name="sender">The object that invoked the delegate.</param>
        /// <param name="e">The chat command arguments.</param>
        [BotnameChatCommand("permissions", "group", UserLevels.Broadcaster)]
        private async void Permission_OnGroupCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!(await Can(e.Command, false, 2).ConfigureAwait(false)))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList[2] ?? "usage").ToLowerInvariant())
            {
                case "create":
                    this.CreateGroup(e.Command);
                    break;

                case "remove":
                    this.RemoveGroup(e.Command);
                    break;

                case "list":
                    this.ListGroups(e.Command);
                    break;

                case "allow":
                    this.GroupAllowPermission(e.Command);
                    break;

                case "deny":
                    this.GroupDenyPermission(e.Command);
                    break;

                case "inherit":
                    this.GroupInheritPermission(e.Command);
                    break;

                case "listpermissions":
                    this.GroupListPermissions(e.Command);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupUsage", e.Command.ChatMessage.RoomId,
                        new { BotName = Program.Settings.BotLogin },
                        "Manages custom permission groups. Usage: !{BotName} permissions group [create, remove, list, allow, deny, inherit, listpermissions]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Main command. Just provides usage to get to the sub-commands.
        /// </summary>
        /// <param name="sender">The object that invoked the delegate.</param>
        /// <param name="e">The chat command arguments.</param>
        [BotnameChatCommand("permissions", UserLevels.Broadcaster)]
        private async void Permission_OnMainCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!(await Can(e.Command, false, 1).ConfigureAwait(false)))
            {
                return;
            }

            TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "Usage", e.Command.ChatMessage.RoomId,
                new { BotName = Program.Settings.BotLogin },
                "Manages custom permissions. Usage: !{BotName} permissions [group]").ConfigureAwait(false));
        }

        /// <summary>
        /// Removes permission group from the channel.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private async void RemoveGroup(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 3 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[3], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupRemoveUsage", command.ChatMessage.RoomId,
                    new { BotName = Program.Settings.BotLogin },
                    "Deletes a custom permission group and removes all users from it's membership. Usage: !{BotName} permissions group remove (GroupName)").ConfigureAwait(false));
                return;
            }

            RemovePermissionGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false));

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupRemoveSuccess", command.ChatMessage.RoomId,
                new { Group = groupName },
                "The permission group {Group} has been removed.").ConfigureAwait(false));
        }

        #endregion Private Methods
    }
}
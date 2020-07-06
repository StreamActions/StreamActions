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
using StreamActions.Database.Documents.Commands;
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

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == command.ChatMessage.RoomId && !c.IsWhisperCommand && c.Command == cmdText);

            using IAsyncCursor<CommandDocument> cursor = await commands.FindAsync(filter).ConfigureAwait(false);

            return await Can(command.ChatMessage.UserId, command.ChatMessage.RoomId, (await cursor.FirstAsync().ConfigureAwait(false)).UserLevel, "can_" + cmdText.Replace(' ', '_')).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if the specified user has permission to perform the specified action.
        /// </summary>
        /// <param name="chatMessage">The chat message sent from an event.</param>
        /// <param name="userLevels">The <see cref="UserLevels"/> allowed to perform this action. Include <see cref="UserLevels.Custom"/> and <paramref name="permissionName"/> to also check permission groups.</param>
        /// <param name="permissionName">The permission name to check in the permission groups, if the <see cref="UserLevels.Custom"/> flag was set in <paramref name="userLevels"/>.</param>
        /// <returns><c>true</c> if the user has permission; <c>false</c> otherwise.</returns>
        public static async Task<bool> Can(ChatMessage chatMessage, UserLevels userLevels, string permissionName = null)
        {
            if (chatMessage is null)
            {
                throw new ArgumentNullException(nameof(chatMessage));
            }

            return await Can(chatMessage.UserId, chatMessage.RoomId, userLevels, permissionName).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if the specified user has permission to perform the specified action.
        /// </summary>
        /// <param name="userId">The userId to check.</param>
        /// <param name="channelId">The channelId to check.</param>
        /// <param name="userLevels">The <see cref="UserLevels"/> allowed to perform this action. Include <see cref="UserLevels.Custom"/> and <paramref name="permissionName"/> to also check permission groups.</param>
        /// <param name="permissionName">The permission name to check in the permission groups, if the <see cref="UserLevels.Custom"/> flag was set in <paramref name="userLevels"/>.</param>
        /// <returns><c>true</c> if the user has permission; <c>false</c> otherwise.</returns>
        public static async Task<bool> Can(string userId, string channelId, UserLevels userLevels, string permissionName = null)
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return (userLevels & UserLevels.Viewer) == UserLevels.Viewer;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            UserDocument userDocument = await cursor.SingleAsync().ConfigureAwait(false);
            UserLevels userLevel = userDocument.UserLevel.GetValueOrDefault(channelId);

            if (userDocument.BotGlobalPermission == BotGlobal.Banned)
            {
                return false;
            }
            else if (userDocument.BotGlobalPermission == BotGlobal.SuperAdmin || (userLevel & UserLevels.Broadcaster) == UserLevels.Broadcaster
                || (userLevels & UserLevels.Viewer) == UserLevels.Viewer)
            {
                return true;
            }

            foreach (UserLevels level in Enum.GetValues(typeof(UserLevels)))
            {
                if ((userLevel & level) == level)
                {
                    switch (level)
                    {
                        case UserLevels.TwitchStaff:
                            if (userLevels <= UserLevels.TwitchStaff || (userLevels & UserLevels.TwitchStaff) == UserLevels.TwitchStaff)
                            {
                                return true;
                            }
                            break;

                        case UserLevels.TwitchAdmin:
                            if (userLevels <= UserLevels.TwitchAdmin || (userLevels & UserLevels.TwitchAdmin) == UserLevels.TwitchAdmin)
                            {
                                return true;
                            }
                            break;

                        case UserLevels.Moderator:
                            if (userLevels <= UserLevels.Moderator || (userLevels & UserLevels.Moderator) == UserLevels.Moderator)
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

            return !string.IsNullOrWhiteSpace(permissionName)
                && await HasPermission(userDocument.PermissionGroupMembership, permissionName.Replace(' ', '_').ToLowerInvariant()).ConfigureAwait(false);
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
        /// Looks up a user's Id.
        /// </summary>
        /// <param name="userName">The user name to look for.</param>
        /// <returns>The Id, if found; <c>null</c> otherwise.</returns>
        public static async Task<string> GetUserIdAsync(string userName)
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Login.Equals(userName, StringComparison.OrdinalIgnoreCase) || d.DisplayName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return null;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

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
                int i;
                if ((i = d.Permissions.FindIndex(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))) >= 0)
                {
                    hasPermission = true;
                    isDenied = isDenied || d.Permissions[i].IsDenied;
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
                _ = RemovePermissionFromAllGroups(permissionName.Replace(' ', '_').ToLowerInvariant()).ConfigureAwait(false);
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
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> AddOrUpdatePermissionToGroup(Guid groupId, string permissionName, bool isDenied = false)
        {
            if (groupId.Equals(Guid.Empty) || string.IsNullOrWhiteSpace(permissionName))
            {
                return false;
            }

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return false;
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

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Creates a new permission group. Fails silently if the group name is already in use (case insensitive).
        /// </summary>
        /// <param name="channelId">The channelId to create the group on.</param>
        /// <param name="groupName">The name of the new group.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> AddPermissionGroup(string channelId, string groupName)
        {
            if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            if (!(await GetGroupGuidAsync(channelId, groupName).ConfigureAwait(false)).Equals(Guid.Empty))
            {
                return false;
            }

            await collection.InsertOneAsync(new PermissionGroupDocument { ChannelId = channelId, GroupName = groupName }).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Adds a permission group to the specified user's memberships.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="userId">The user to add the membership to.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> AddPermissionGroupToUser(Guid groupId, string userId)
        {
            if (groupId.Equals(Guid.Empty) || string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return false;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<Guid> permissionGroups = (await cursor.SingleAsync().ConfigureAwait(false)).PermissionGroupMembership;

            if (!permissionGroups.Exists(p => p.Equals(groupId)))
            {
                permissionGroups.Add(groupId);

                UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update.Set(d => d.PermissionGroupMembership, permissionGroups);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                return result.IsAcknowledged;
            }

            return true;
        }

        /// <summary>
        /// Removes the specified permission from all permission groups.
        /// </summary>
        /// <param name="permissionName">The permission to remove.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> RemovePermissionFromAllGroups(string permissionName)
        {
            if (string.IsNullOrWhiteSpace(permissionName))
            {
                return false;
            }

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return true;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            await cursor.ForEachAsync(d => RemovePermissionFromGroup(d.Id, permissionName.Replace(' ', '_').ToLowerInvariant())).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Removes the specified permission from the specified permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="permissionName">The permission to remove.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> RemovePermissionFromGroup(Guid groupId, string permissionName)
        {
            if (groupId.Equals(Guid.Empty) || string.IsNullOrWhiteSpace(permissionName))
            {
                return false;
            }

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return false;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            if (permissions.Exists(p => p.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)))
            {
                _ = permissions.RemoveAll(d => d.PermissionName.Equals(permissionName.Replace(' ', '_').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));

                UpdateDefinition<PermissionGroupDocument> update = Builders<PermissionGroupDocument>.Update.Set(d => d.Permissions, permissions);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                return result.IsAcknowledged;
            }

            return true;
        }

        /// <summary>
        /// Deletes a permission group and removes all users from it's membership.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> RemovePermissionGroup(Guid groupId)
        {
            if (groupId.Equals(Guid.Empty))
            {
                return false;
            }

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            _ = await RemovePermissionGroupFromAllUsers(groupId).ConfigureAwait(false);
            DeleteResult result = await collection.DeleteOneAsync(filter).ConfigureAwait(false);

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Removes all users from the membership of a permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> RemovePermissionGroupFromAllUsers(Guid groupId)
        {
            if (groupId.Equals(Guid.Empty))
            {
                return false;
            }

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("permissionGroups");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.PermissionGroupMembership.Contains(groupId));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return true;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            await cursor.ForEachAsync(d => RemovePermissionGroupFromUser(groupId, d.Id)).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Removes a user from the membership of a permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="userId">The userId who is being removed.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise.</returns>
        internal static async Task<bool> RemovePermissionGroupFromUser(Guid groupId, string userId)
        {
            if (groupId.Equals(Guid.Empty) || string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return false;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<Guid> permissionGroups = (await cursor.SingleAsync().ConfigureAwait(false)).PermissionGroupMembership;

            if (permissionGroups.Exists(p => p.Equals(groupId)))
            {
                _ = permissionGroups.RemoveAll(d => d.Equals(groupId));

                UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update.Set(d => d.PermissionGroupMembership, permissionGroups);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                return result.IsAcknowledged;
            }

            return true;
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// The number of items to show per page in list outputs.
        /// </summary>
        private const int _itemsPerPage = 10;

        /// <summary>
        /// <see cref="_itemsPerPage"/> as a double.
        /// </summary>
        private const double _itemsPerPageD = (double)_itemsPerPage;

        /// <summary>
        /// Field that backs <see cref="RegisteredPermissions"/>.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> _registeredPermissions = new ConcurrentDictionary<string, string>();

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Adds a user to a permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void AddUserToGroup(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserAddUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Adds a user to a permission group. Usage: {CommandPrefix}{BotName} permissions user add [UserName] [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await AddPermissionGroupToUser(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), await GetUserIdAsync(command.ArgumentsAsList[3]).ConfigureAwait(false)).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserAddSuccess", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The user {ToUser} has been added to the permission group {Group}").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserAddFail", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to add {ToUser} to the group. The user may not be listed in the database or the group may not exist, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Creates a new permission group in the channel.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void CreateGroup(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 3 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[3], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupCreateUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Creates a new custom permission group. Usage: {CommandPrefix}{BotName} permissions group create [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await AddPermissionGroup(command.ChatMessage.RoomId, groupName).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupCreateSuccess", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The permission group {Group} has been created").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupCreateFail", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to create the group. The name may be invalid or already in use, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Allows a permission to users of the specified permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void GroupAllowPermission(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupAllowUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Allows the specified permission in the custom permission group, can be overridden by a deny. Usage: {CommandPrefix}{BotName} permissions group allow [PermissionName] [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await AddOrUpdatePermissionToGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), command.ArgumentsAsList[3], false).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupAllowSuccess", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The permission {Permission} has been allowed to members of {Group}").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupAllowFail", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to allow the permission. The group may not exist or permission may be invalid, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Explicitly denies a permission to users of the specified permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void GroupDenyPermission(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupDenyUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Explicitly denies the specified permission in the custom permission group, overriding any allows that another group may provide. Usage: {CommandPrefix}{BotName} permissions group deny [PermissionName] [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await AddOrUpdatePermissionToGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), command.ArgumentsAsList[3], true).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupDenySuccess", command.ChatMessage.RoomId,
                new
                {
                    Group = groupName,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The permission {Permission} has been explicitly denied to members of {Group} and will override any allows provided by another group.").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupDenyFail", command.ChatMessage.RoomId,
                new
                {
                    Group = groupName,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Unable to deny the permission. The group may not exist or permission may be invalid, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Sets the specified permission to be inherited in the permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void GroupInheritPermission(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupInheritUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Implicitly denies the specified permission in the custom permission group, but another group can allow it. Usage: {CommandPrefix}{BotName} permissions group inherit [PermissionName] [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await RemovePermissionFromGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), command.ArgumentsAsList[3]).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupInheritSuccess", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The permission {Permission} is now implicitly denied, but can be allowed by another group, to members of {Group}").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupInheritFail", command.ChatMessage.RoomId,
                new
                {
                    Group = groupName,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Unable to inherit the permission. The group may not exist or permission may be invalid, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Lists the current permissions of the specified permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void GroupListPermissions(ChatCommand command)
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
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Lists the permissions that are allowed or explicitly denied by a custom group. Usage: {CommandPrefix}{BotName} permissions group listpermissions [page (optional)] [GroupName]").ConfigureAwait(false));
                return;
            }

            Guid groupId = await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false);

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupListPermissionsFail", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to list the permissions of the group. The group may not exist.").ConfigureAwait(false));
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            if (permissions.Count == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupListPermissionsNoPermissions", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, No permissions have been assigned to {Group}.").ConfigureAwait(false));
                return;
            }

            numPage = (int)Math.Ceiling(permissions.Count / _itemsPerPageD);

            page = Math.Min(numPage, Math.Max(1, page));

            foreach (PermissionDocument permission in permissions.Skip((page - 1) * _itemsPerPage).Take(_itemsPerPage))
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
                new
                {
                    Permissions = permissionNames,
                    Group = groupName,
                    Page = page,
                    NumPage = numPage,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Permissions of group {Group} [Page {Page} of {NumPage}]: {Permissions}").ConfigureAwait(false));
        }

        /// <summary>
        /// Lists the permission groups of the channel.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void ListGroups(ChatCommand command)
        {
            string groupNames = "";
            int page = 1;
            int numPage = 1;

            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            long numGroups = await collection.CountDocumentsAsync(filter).ConfigureAwait(false);
            numPage = (int)Math.Ceiling(numGroups / _itemsPerPageD);

            page = Math.Min(numPage, Math.Max(1, int.Parse(command.ArgumentsAsList[3] ?? "1", NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false))));

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionGroupDocument> permissionGroups = await cursor.ToListAsync().ConfigureAwait(false);

            if (permissionGroups.Count == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupListNoGroups", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, No permission groups currently exist.").ConfigureAwait(false));
                return;
            }

            foreach (PermissionGroupDocument permissionGroup in permissionGroups.Skip((page - 1) * _itemsPerPage).Take(_itemsPerPage))
            {
                if (groupNames.Length > 0)
                {
                    groupNames += ", ";
                }

                groupNames += permissionGroup.GroupName;
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupList", command.ChatMessage.RoomId,
                new
                {
                    Groups = groupNames,
                    Page = page,
                    NumPage = numPage,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Permission groups [Page {Page} of {NumPage}]: {Groups}").ConfigureAwait(false));
        }

        /// <summary>
        /// Lists the groups that a user is a member of.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void ListUsersGroups(ChatCommand command)
        {
            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserListUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Lists the permission groups that a user is a member of. Usage: {CommandPrefix}{BotName} permissions user list [UserName] [page (optional)]").ConfigureAwait(false));
                return;
            }

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Login.Equals(command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase) || d.DisplayName.Equals(command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserListFail", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to list user group memberships. The user may not be listed in the database.").ConfigureAwait(false));
                return;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            UserDocument userDocument = await cursor.SingleAsync().ConfigureAwait(false);

            if (userDocument.PermissionGroupMembership.Count == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserListNoGroups", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, {ToUser} is not a member of any custom permission groups.").ConfigureAwait(false));
                return;
            }

            string groupNames = "";
            int numPage = 1;

            if (!int.TryParse(command.ArgumentsAsList[4] ?? "1", NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false), out int page))
            {
                page = 1;
            }

            IMongoCollection<PermissionGroupDocument> collection2 = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter2 = Builders<PermissionGroupDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId && userDocument.PermissionGroupMembership.Contains(d.Id));

            long numGroups = await collection2.CountDocumentsAsync(filter2).ConfigureAwait(false);
            numPage = (int)Math.Ceiling(numGroups / _itemsPerPageD);

            page = Math.Min(numPage, Math.Max(1, page));

            using IAsyncCursor<PermissionGroupDocument> cursor2 = await collection2.FindAsync(filter2).ConfigureAwait(false);

            List<PermissionGroupDocument> permissionGroups = await cursor2.ToListAsync().ConfigureAwait(false);

            foreach (PermissionGroupDocument permissionGroup in permissionGroups.Skip((page - 1) * _itemsPerPage).Take(_itemsPerPage))
            {
                if (groupNames.Length > 0)
                {
                    groupNames += ", ";
                }

                groupNames += permissionGroup.GroupName;
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserList", command.ChatMessage.RoomId,
                new
                {
                    ToUser = command.ArgumentsAsList[3],
                    Groups = groupNames,
                    Page = page,
                    NumPage = numPage,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Permission groups of {ToUser} [Page {Page} of {NumPage}]: {Groups}").ConfigureAwait(false));
        }

        /// <summary>
        /// Removes permission group from the channel.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void RemoveGroup(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 3 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[3], StringComparison.Ordinal)) : null;

            if (string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupRemoveUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Deletes a custom permission group and removes all users from it's membership. Usage: {CommandPrefix}{BotName} permissions group remove [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await RemovePermissionGroup(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false)).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupRemoveSuccess", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The permission group {Group} has been removed").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupRemoveFail", command.ChatMessage.RoomId,
                    new
                    {
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to remove the group. The group may not exist, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Removes a user from a permission group.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void RemoveUserFromGroup(ChatCommand command)
        {
            string groupName = command.ArgumentsAsList.Count > 4 ? command.ArgumentsAsString.Substring(command.ArgumentsAsString.IndexOf(command.ArgumentsAsList[4], StringComparison.Ordinal)) : null;

            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3) || string.IsNullOrWhiteSpace(groupName))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserRemoveUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Removes a user from a permission group. Usage: {CommandPrefix}{BotName} permissions user remove [UserName] [GroupName]").ConfigureAwait(false));
                return;
            }

            if (await RemovePermissionGroupFromUser(await GetGroupGuidAsync(command.ChatMessage.RoomId, groupName).ConfigureAwait(false), await GetUserIdAsync(command.ArgumentsAsList[3]).ConfigureAwait(false)).ConfigureAwait(false))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserRemoveSuccess", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The user {ToUser} has been removed from the permission group {Group}.").ConfigureAwait(false));
            }
            else
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserRemoveFail", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        Group = groupName,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to remove {ToUser} from the group. The user may not be listed in the database or the group may not exist, or the database may have refused the transaction.").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Lists the effective permissions of the user.
        /// </summary>
        /// <param name="e">The chat command arguments.</param>
        private static async void UserEffectivePermissions(ChatCommand command)
        {
            if (command.ArgumentsAsList.IsNullWhiteSpaceOrOutOfRange(3))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserEffectivePermissionsUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Lists the effective permissions of the user. Usage: {CommandPrefix}{BotName} permissions user effectivepermissions [UserName] [page (optional)]").ConfigureAwait(false));
                return;
            }

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Login.Equals(command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase) || d.DisplayName.Equals(command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserEffectivePermissionsFail", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Unable to list user effective permissions. The user may not be listed in the database.").ConfigureAwait(false));
                return;
            }

            using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            UserDocument userDocument = await cursor.SingleAsync().ConfigureAwait(false);

            if (userDocument.PermissionGroupMembership.Count == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserEffectivePermissionsNoGroups", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, {ToUser} is not a member of any custom permission groups, and therefore only has the permissions of their channel badges.").ConfigureAwait(false));
                return;
            }

            string permissionNames = "";
            int numPage = 1;

            if (!int.TryParse(command.ArgumentsAsList[4] ?? "1", NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false), out int page))
            {
                page = 1;
            }

            IMongoCollection<PermissionGroupDocument> collection2 = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter2 = Builders<PermissionGroupDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId && userDocument.PermissionGroupMembership.Contains(d.Id));

            using IAsyncCursor<PermissionGroupDocument> cursor2 = await collection2.FindAsync(filter2).ConfigureAwait(false);

            List<PermissionGroupDocument> permissionGroups = await cursor2.ToListAsync().ConfigureAwait(false);

            List<PermissionDocument> permissions = new List<PermissionDocument>();

            foreach (PermissionGroupDocument permissionGroup in permissionGroups)
            {
                foreach (PermissionDocument p in permissionGroup.Permissions)
                {
                    PermissionDocument d = permissions.FirstOrDefault(pd => pd.PermissionName == p.PermissionName);

                    if (d is null)
                    {
                        permissions.Add(p);
                    }
                    else
                    {
                        d.IsDenied = d.IsDenied || p.IsDenied;
                    }
                }
            }

            numPage = (int)Math.Ceiling(permissions.Count / _itemsPerPageD);

            page = Math.Min(numPage, Math.Max(1, page));

            if (permissions.Count == 0)
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserEffectivePermissionsNoPermissions", command.ChatMessage.RoomId,
                    new
                    {
                        ToUser = command.ArgumentsAsList[3],
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, {ToUser} is a member of custom permission groups, but no permissions have been assigned to any of them.").ConfigureAwait(false));
                return;
            }

            foreach (PermissionDocument permission in permissions.Skip((page - 1) * _itemsPerPage).Take(_itemsPerPage))
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

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserEffectivePermissionsList", command.ChatMessage.RoomId,
                new
                {
                    ToUser = command.ArgumentsAsList[3],
                    Permissions = permissionNames,
                    Page = page,
                    NumPage = numPage,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Effective Permissions of {ToUser} [Page {Page} of {NumPage}]: {Permissions}").ConfigureAwait(false));
        }

        /// <summary>
        /// Permission group management command. Allows CRUD of groups and their permissions.
        /// </summary>
        /// <param name="sender">The object that invoked the delegate.</param>
        /// <param name="e">The chat command arguments.</param>
        [BotnameChatCommand("permissions", "group", UserLevels.Broadcaster)]
        private async void Permission_OnGroupCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList[2] ?? "usage").ToLowerInvariant())
            {
                case "create":
                    CreateGroup(e.Command);
                    break;

                case "remove":
                    RemoveGroup(e.Command);
                    break;

                case "list":
                    ListGroups(e.Command);
                    break;

                case "allow":
                    GroupAllowPermission(e.Command);
                    break;

                case "deny":
                    GroupDenyPermission(e.Command);
                    break;

                case "inherit":
                    GroupInheritPermission(e.Command);
                    break;

                case "listpermissions":
                    GroupListPermissions(e.Command);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "GroupUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages custom permission groups. Usage: {CommandPrefix}{BotName} permissions group [create, remove, list, allow, deny, inherit, listpermissions]").ConfigureAwait(false));
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
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = e.Command.ChatMessage.Username,
                    Sender = e.Command.ChatMessage.Username,
                    e.Command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Manages custom permissions. Usage: {CommandPrefix}{BotName} permissions [group, user]").ConfigureAwait(false));
        }

        /// <summary>
        /// User permission group membership management command. Allows adding/removing users from permission groups.
        /// </summary>
        /// <param name="sender">The object that invoked the delegate.</param>
        /// <param name="e">The chat command arguments.</param>
        [BotnameChatCommand("permissions", "user", UserLevels.Broadcaster)]
        private async void Permission_OnUserCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList[2] ?? "usage").ToLowerInvariant())
            {
                case "add":
                    AddUserToGroup(e.Command);
                    break;

                case "remove":
                    RemoveUserFromGroup(e.Command);
                    break;

                case "list":
                    ListUsersGroups(e.Command);
                    break;

                case "effectivepermissions":
                    UserEffectivePermissions(e.Command);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("Permission", "UserUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages a user's custom permission group membership. Usage: {CommandPrefix}{BotName} permissions user [add, remove, list, effectivepermissions]").ConfigureAwait(false));
                    break;
            }
        }

        #endregion Private Methods
    }
}
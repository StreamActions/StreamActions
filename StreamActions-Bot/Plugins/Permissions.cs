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
using StreamActions.Database;
using StreamActions.Database.Documents.Permissions;
using StreamActions.Database.Documents.Users;
using StreamActions.Enums;
using StreamActions.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles permissions.
    /// </summary>
    [Guid("93149A5E-54D4-4EC2-AF18-D32602C12BFE")]
    public class Permissions : IPlugin
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

        public Guid PluginId => typeof(Permissions).GUID;

        public string PluginName => "Permissions";

        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions-Bot");

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

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
                ? await HasPermission(userDocument.PermissionGroupMembership, permissionName).ConfigureAwait(false)
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
                if (d.Permissions.Exists(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase) && p.IsDenied == false))
                {
                    hasPermission = true;
                }
                else if (d.Permissions.Exists(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase) && p.IsDenied == true))
                {
                    isDenied = true;
                }
            }).ConfigureAwait(false);

            return hasPermission && !isDenied;
        }

        /// <summary>
        /// Attempts to register a permission.
        /// </summary>
        /// <param name="permissionName">The name to register.</param>
        /// <param name="description">The description of the permission.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise</returns>
        public static bool TryRegisterPermission(string permissionName, string description) => _registeredPermissions.TryAdd(permissionName, description);

        /// <summary>
        /// Attempts to unregister a permission and, on success, remove it from all PermissionGroups.
        /// </summary>
        /// <param name="permissionName">The name to unregister.</param>
        /// <returns><c>true</c> on success; <c>false</c> otherwise</returns>
        public static bool TryUnregisterPermission(string permissionName)
        {
            bool result = _registeredPermissions.TryRemove(permissionName, out _);

            if (result)
            {
                RemovePermissionFromAllGroups(permissionName);
            }

            return result;
        }

        public void Disabled()
        {
        }

        public void Enabled()
        {
        }

        #endregion Public Methods

        #region Internal Methods

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
        /// Adds a permission to the specified permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="permissionName">The permission to add.</param>
        /// <param name="isDenied">Whether the permission is being explicitly denied.</param>
        internal static async void AddPermissionToGroup(Guid groupId, string permissionName, bool isDenied = false)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            if (!permissions.Exists(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(new PermissionDocument { PermissionName = permissionName, IsDenied = isDenied });

                UpdateDefinition<PermissionGroupDocument> update = Builders<PermissionGroupDocument>.Update.Set(d => d.Permissions, permissions);

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

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Permissions.Exists(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase)));

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            await cursor.ForEachAsync(d => RemovePermissionFromGroup(d.Id, permissionName)).ConfigureAwait(false);
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

            if (permissions.Exists(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase)))
            {
                _ = permissions.RemoveAll(d => d.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase));

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

        /// <summary>
        /// Updates the denied state of a permission in a permission group.
        /// </summary>
        /// <param name="groupId">The Guid of the group.</param>
        /// <param name="permissionName">The permission to update.</param>
        /// <param name="isDenied">The new denied state.</param>
        internal static async void UpdatePermissionInGroup(Guid groupId, string permissionName, bool isDenied = false)
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>("permissionGroups");

            FilterDefinition<PermissionGroupDocument> filter = Builders<PermissionGroupDocument>.Filter.Where(d => d.Id == groupId);

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return;
            }

            using IAsyncCursor<PermissionGroupDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            List<PermissionDocument> permissions = (await cursor.SingleAsync().ConfigureAwait(false)).Permissions;

            if (permissions.Exists(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Find(p => p.PermissionName.Equals(permissionName, StringComparison.OrdinalIgnoreCase)).IsDenied = isDenied;

                UpdateDefinition<PermissionGroupDocument> update = Builders<PermissionGroupDocument>.Update.Set(d => d.Permissions, permissions);

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
    }
}
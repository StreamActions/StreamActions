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

using EntityGraphQL.Schema;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamActions.Database.Documents.Commands
{
    /// <summary>
    /// Represents the current cooldown state of a command.
    /// </summary>
    public class CommandCooldownDocument
    {
        #region Public Properties

        /// <summary>
        /// The global cooldown time for this command, in seconds.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint GlobalCooldown { get; set; }

        /// <summary>
        /// Indicates when the global cooldown will expire.
        /// </summary>
        [BsonIgnore]
        public DateTime GlobalCooldownExpires { get; private set; } = DateTime.Now;

        /// <summary>
        /// Indicates the remaining time until the global cooldown expires.
        /// </summary>
        [BsonIgnore]
        public TimeSpan GlobalCooldownTimeRemaining => this.IsOnGlobalCooldown ? this.GlobalCooldownExpires - DateTime.Now : TimeSpan.Zero;

        /// <summary>
        /// The Guid for referencing this command in the database.
        /// </summary>
        [BsonIgnore]
        [GraphQLIgnore]
        public Guid Id { get; internal set; }

        /// <summary>
        /// Indicates whether the command is currently on global cooldown.
        /// </summary>
        [BsonIgnore]
        public bool IsOnGlobalCooldown => DateTime.Now.CompareTo(this.GlobalCooldownExpires) < 0;

        /// <summary>
        /// The user cooldown time for this command, in seconds.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(0)]
        public uint UserCooldown { get; set; }

        /// <summary>
        /// Indicates when the cooldown for a user Id expires.
        /// </summary>
        [BsonIgnore]
        [GraphQLIgnore]
        public Dictionary<string, DateTime> UserCooldownExpires
        {
            get
            {
                Dictionary<string, DateTime> value = new Dictionary<string, DateTime>();

                foreach (CommandUserCooldownDocument d in this.UserCooldowns)
                {
                    value.Add(d.UserId, d.CooldownExpires);
                }

                return value;
            }
        }

        /// <summary>
        /// Contains a list of <see cref="CommandUserCooldownDocument"/>.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<CommandUserCooldownDocument> UserCooldowns { get; } = new List<CommandUserCooldownDocument>();

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Cleans up expired user cooldowns.
        /// </summary>
        /// <param name="shouldCache">Indicates if the cleaned up cooldowns should be cached for the next DB write.</param>
        public void CleanupUserCooldowns(bool shouldCache = true)
        {
            foreach (CommandUserCooldownDocument d in this.UserCooldowns)
            {
                if (DateTime.Now.CompareTo(d.CooldownExpires) >= 0)
                {
                    _ = this.UserCooldowns.Remove(d);

                    if (shouldCache)
                    {
                        this.CacheChange();
                    }
                }
            }
        }

        /// <summary>
        /// Indicates if either the specified user Id or the command itself is currently on cooldown.
        /// </summary>
        /// <param name="userId">The user Id to check.</param>
        /// <returns><c>true</c> if the user or command is on cooldown.</returns>
        public bool IsOnCooldown(string userId) => this.IsOnGlobalCooldown || this.IsUserOnCooldown(userId);

        /// <summary>
        /// Indicates if the specified user Id is currently on cooldown for this command.
        /// </summary>
        /// <param name="userId">The user Id to check.</param>
        /// <returns><c>true</c> if the user is on cooldown.</returns>
        public bool IsUserOnCooldown(string userId)
        {
            if (this.UserCooldowns.Exists(d => d.UserId == userId))
            {
                if (DateTime.Now.CompareTo(this.UserCooldowns.Find(d => d.UserId == userId).CooldownExpires) < 0)
                {
                    return true;
                }
                else
                {
                    _ = this.UserCooldowns.Remove(this.UserCooldowns.Find(d => d.UserId == userId));
                    this.CacheChange();
                }
            }

            return false;
        }

        /// <summary>
        /// Starts both the global cooldown and the user cooldown for the specified times.
        /// </summary>
        /// <param name="globalTime">A TimeSpan representing the amount of time the global cooldown will last.</param>
        /// <param name="userId">The user Id to cooldown.</param>
        /// <param name="userTime">A TimeSpan representing the amount of time the user cooldown will last.</param>
        public void StartCooldown(TimeSpan globalTime, string userId, TimeSpan userTime)
        {
            if (globalTime.TotalMilliseconds > 0)
            {
                this.StartGlobalCooldown(globalTime);
            }

            if (!(userId is null) && userId.Length > 0 && userTime.TotalMilliseconds > 0)
            {
                this.StartUserCooldown(userId, userTime);
            }
        }

        /// <summary>
        /// Starts both the global cooldown and the user cooldown.
        /// </summary>
        /// <param name="userId">The user Id to cooldown.</param>
        public void StartCooldown(string userId) => this.StartCooldown(TimeSpan.FromSeconds(this.GlobalCooldown), userId, TimeSpan.FromSeconds(this.UserCooldown));

        /// <summary>
        /// Starts the global cooldown for the specified time.
        /// </summary>
        /// <param name="time">A TimeSpan representing the amount of time the cooldown will last.</param>
        public void StartGlobalCooldown(TimeSpan time)
        {
            this.GlobalCooldownExpires = DateTime.Now.Add(time);
            this.CacheChange();
        }

        /// <summary>
        /// Starts the global cooldown for <see cref="GlobalCooldown"/> seconds.
        /// </summary>
        public void StartGlobalCooldown() => this.StartGlobalCooldown(TimeSpan.FromSeconds(this.GlobalCooldown));

        /// <summary>
        /// Starts or restarts the cooldown for the specified user Id on this command.
        /// </summary>
        /// <param name="userId">The user Id to place on cooldown.</param>
        /// <param name="time">A TimeSpan representing the amount of time the cooldown will last.</param>
        public void StartUserCooldown(string userId, TimeSpan time)
        {
            if (this.UserCooldowns.Exists(d => d.UserId == userId))
            {
                this.UserCooldowns.Find(d => d.UserId == userId).CooldownExpires = DateTime.Now.Add(time);
            }
            else
            {
                this.UserCooldowns.Add(new CommandUserCooldownDocument { UserId = userId, CooldownExpires = DateTime.Now.Add(time) });
            }

            this.CacheChange();
        }

        /// <summary>
        /// Starts or restarts the cooldown for the specified user Id on this command for <see cref="UserCooldown"/> seconds.
        /// </summary>
        /// <param name="userId">The user Id to place on cooldown.</param>
        public void StartUserCooldown(string userId) => this.StartUserCooldown(userId, TimeSpan.FromSeconds(this.UserCooldown));

        /// <summary>
        /// Indicates the remaining time until the user Ids cooldown expires.
        /// </summary>
        /// <param name="userId">The userId to check.</param>
        /// <returns>A TimeSpan indicating the time left until the user Ids cooldown expires.</returns>
        public TimeSpan UserCooldownTimeRemaining(string userId) => this.IsUserOnCooldown(userId) ? this.UserCooldowns.Find(d => d.UserId == userId).CooldownExpires - DateTime.Now : TimeSpan.Zero;

        /// <summary>
        /// Awaits the expiration of this commands global cooldown.
        /// </summary>
        /// <returns>A Task that can be awaited.</returns>
        public async Task WaitForGlobalCooldownAsync() => await Task.Delay((int)this.GlobalCooldownTimeRemaining.TotalMilliseconds).ConfigureAwait(false);

        /// <summary>
        /// Awaits the expiration of the specified user Ids cooldown.
        /// </summary>
        /// <param name="userId">The user Id to await.</param>
        /// <param name="loop">If <c>true</c> loop until cooldown expires (in case cooldown was refreshed).</param>
        /// <returns>A Task that can be awaited.</returns>
        public async Task WaitForUserCooldownAsync(string userId, bool loop = false)
        {
            if (this.IsUserOnCooldown(userId))
            {
                while (this.UserCooldownTimeRemaining(userId).TotalMilliseconds > 0)
                {
                    await Task.Delay((int)this.UserCooldownTimeRemaining(userId).TotalMilliseconds + 50).ConfigureAwait(false);

                    if (!loop)
                    {
                        break;
                    }
                }

                if (this.UserCooldownTimeRemaining(userId).TotalMilliseconds == 0)
                {
                    _ = this.UserCooldowns.Remove(this.UserCooldowns.Find(d => d.UserId == userId));
                    this.CacheChange();
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void CacheChange() => _ = MiscScheduledTasks._changedCooldowns.TryAdd(this.Id, this);

        #endregion Private Methods
    }
}
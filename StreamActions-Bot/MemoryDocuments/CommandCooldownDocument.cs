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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamActions.MemoryDocuments
{
    /// <summary>
    /// Represents the current cooldown state of a command.
    /// </summary>
    public class CommandCooldownDocument
    {
        #region Public Properties

        /// <summary>
        /// Indicates when the global cooldown will expire.
        /// </summary>
        public DateTime GlobalCooldownExpires { get; private set; } = DateTime.Now;

        /// <summary>
        /// Indicates the remaining time until the global cooldown expires.
        /// </summary>
        public TimeSpan GlobalCooldownTimeRemaining => this.IsOnGlobalCooldown ? this.GlobalCooldownExpires - DateTime.Now : TimeSpan.Zero;

        /// <summary>
        /// Indicates whether the command is currently on global cooldown.
        /// </summary>
        public bool IsOnGlobalCooldown => DateTime.Now.CompareTo(this.GlobalCooldownExpires) < 0;

        /// <summary>
        /// Indicates when the cooldown for a user Id expires.
        /// </summary>
        [GraphQLIgnore]
        public Dictionary<string, DateTime> UserCooldownExpires
        {
            get
            {
                Dictionary<string, DateTime> value = new Dictionary<string, DateTime>();

                foreach (KeyValuePair<string, CommandUserCooldownDocument> kvp in this._userCooldownExpires)
                {
                    value.Add(kvp.Value.UserId, kvp.Value.CooldownExpires);
                }

                return value;
            }
        }

        public List<CommandUserCooldownDocument> UserCooldowns
        {
            get
            {
                List<CommandUserCooldownDocument> value = new List<CommandUserCooldownDocument>();

                foreach (KeyValuePair<string, CommandUserCooldownDocument> kvp in this._userCooldownExpires)
                {
                    value.Add(kvp.Value);
                }

                return value;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Cleans up expired user cooldowns.
        /// </summary>
        public void CleanupUserCooldowns()
        {
            foreach (KeyValuePair<string, CommandUserCooldownDocument> kvp in this._userCooldownExpires)
            {
                if (DateTime.Now.CompareTo(kvp.Value.CooldownExpires) >= 0)
                {
                    _ = this._userCooldownExpires.TryRemove(kvp.Key, out CommandUserCooldownDocument _);
                }
            }
        }

        /// <summary>
        /// Indicates if the specified user Id is currently on cooldown for this command.
        /// </summary>
        /// <param name="userId">The user Id to check.</param>
        /// <returns><c>true</c> if the user is on cooldown.</returns>
        public bool IsUserOnCooldown(string userId)
        {
            if (this._userCooldownExpires.ContainsKey(userId))
            {
                if (DateTime.Now.CompareTo(this._userCooldownExpires[userId]) < 0)
                {
                    return true;
                }
                else
                {
                    _ = this._userCooldownExpires.TryRemove(userId, out _);
                }
            }

            return false;
        }

        /// <summary>
        /// Starts the global cooldown for the specified time.
        /// </summary>
        /// <param name="time">A TimeSpan representing the amount of time the cooldown will last.</param>
        public void StartGlobalCooldown(TimeSpan time) => this.GlobalCooldownExpires = DateTime.Now.Add(time);

        /// <summary>
        /// Starts the global cooldown for the specified time.
        /// </summary>
        /// <param name="seconds">The number of seconds the cooldown will last.</param>
        public void StartGlobalCooldown(int seconds) => this.StartGlobalCooldown(TimeSpan.FromSeconds(seconds));

        /// <summary>
        /// Starts or restarts the cooldown for the specified user Id on this command.
        /// </summary>
        /// <param name="userId">The user Id to place on cooldown.</param>
        /// <param name="time">A TimeSpan representing the amount of time the cooldown will last.</param>
        public void StartUserCooldown(string userId, TimeSpan time)
        {
            _ = this._userCooldownExpires.TryRemove(userId, out _);
            _ = this._userCooldownExpires.TryAdd(userId, new CommandUserCooldownDocument { UserId = userId, CooldownExpires = DateTime.Now.Add(time) });
        }

        /// <summary>
        /// Starts or restarts the cooldown for the specified user Id on this command.
        /// </summary>
        /// <param name="userId">The user Id to place on cooldown.</param>
        /// <param name="seconds">The number of seconds the cooldown will last.</param>
        public void StartUserCooldown(string userId, int seconds) => this.StartUserCooldown(userId, TimeSpan.FromSeconds(seconds));

        /// <summary>
        /// Indicates the remaining time until the user Ids cooldown expires.
        /// </summary>
        /// <param name="userId">The userId to check.</param>
        /// <returns>A TimeSpan indicating the time left until the user Ids cooldown expires.</returns>
        public TimeSpan UserCooldownTimeRemaining(string userId) => this.IsUserOnCooldown(userId) ? this._userCooldownExpires[userId].CooldownExpires - DateTime.Now : TimeSpan.Zero;

        /// <summary>
        /// Awaits the expiration of this commands global cooldown.
        /// </summary>
        /// <returns>A Task that can be awaited.</returns>
        public async Task WaitForGlobalCooldown() => await Task.Delay((int)this.GlobalCooldownTimeRemaining.TotalMilliseconds).ConfigureAwait(false);

        /// <summary>
        /// Awaits the expiration of the specified user Ids cooldown.
        /// </summary>
        /// <param name="userId">The user Id to await.</param>
        /// <returns>A Task that can be awaited.</returns>
        public async Task WaitForUserCooldown(string userId)
        {
            if (this.IsUserOnCooldown(userId))
            {
                await Task.Delay((int)this.UserCooldownTimeRemaining(userId).TotalMilliseconds).ConfigureAwait(false);
                _ = this._userCooldownExpires.TryRemove(userId, out _);
            }
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="UserCooldownExpires"/> property.
        /// </summary>
        private readonly ConcurrentDictionary<string, CommandUserCooldownDocument> _userCooldownExpires = new ConcurrentDictionary<string, CommandUserCooldownDocument>();

        #endregion Private Fields
    }
}
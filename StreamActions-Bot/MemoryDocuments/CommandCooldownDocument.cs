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
using StreamActions.GraphQL.Connections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace StreamActions.MemoryDocuments
{
    /// <summary>
    /// Represents the current cooldown state of a command.
    /// </summary>
    public class CommandCooldownDocument : ICursorable
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The commandId this document applies to.</param>
        /// <param name="globalCooldown">The default global cooldown time to use for this command, in seconds.</param>
        /// <param name="userCooldown">The default user cooldown time to use for this command, in seconds.</param>
        public CommandCooldownDocument(Guid commandId, uint globalCooldown, uint userCooldown)
        {
            this.CommandId = commandId;
            this.GlobalCooldown = globalCooldown;
            this.UserCooldown = userCooldown;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The commandId this document applies to.
        /// </summary>
        public Guid CommandId { get; set; }

        /// <summary>
        /// The global cooldown time for this command, in seconds.
        /// </summary>
        public uint GlobalCooldown { get; set; }

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
        /// The user cooldown time for this command, in seconds.
        /// </summary>
        public uint UserCooldown { get; set; }

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

        public string GetCursor() => this.CommandId.ToString("D", CultureInfo.InvariantCulture);

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
        /// Starts both the global cooldown and the user cooldown for the times specified in the appropriate properties.
        /// </summary>
        /// <param name="userId">The user Id to cooldown.</param>
        public void StartCooldown(string userId) => this.StartCooldown(TimeSpan.FromSeconds(this.GlobalCooldown), userId, TimeSpan.FromSeconds(this.UserCooldown));

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
        /// Starts the global cooldown for the specified time.
        /// </summary>
        /// <param name="time">A TimeSpan representing the amount of time the cooldown will last.</param>
        public void StartGlobalCooldown(TimeSpan time) => this.GlobalCooldownExpires = DateTime.Now.Add(time);

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
            _ = this._userCooldownExpires.TryRemove(userId, out _);
            _ = this._userCooldownExpires.TryAdd(userId, new CommandUserCooldownDocument { UserId = userId, CooldownExpires = DateTime.Now.Add(time) });

            _ = Task.Run(async () => await this.WaitForUserCooldownAsync(userId).ConfigureAwait(false));
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
        public TimeSpan UserCooldownTimeRemaining(string userId) => this.IsUserOnCooldown(userId) ? this._userCooldownExpires[userId].CooldownExpires - DateTime.Now : TimeSpan.Zero;

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
                    _ = this._userCooldownExpires.TryRemove(userId, out _);
                }
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
/*
 * Copyright © 2019-2021 StreamActions Team
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

using StreamActions.Enums;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Represents the result of a <see cref="PluginManager.OnMessageModeration"/> EventHandler.
    /// </summary>
    public class ModerationResult
    {
        #region Public Properties

        /// <summary>
        /// Option to ignore the moderation result from the filter when set to true.
        /// </summary>
        public bool IgnoreOnPermit { get; set; } = false;

        /// <summary>
        /// The message to send to chat after the action is taken.
        /// </summary>
        public string ModerationMessage { get; set; }

        /// <summary>
        /// The reason to attach to the moderation command.
        /// </summary>
        public string ModerationReason { get; set; }

        /// <summary>
        /// The type of punishment to give to the user <see cref="_punishment"/>.
        /// </summary>
        public ModerationPunishment Punishment
        {
            get => this._punishment;
            set => this._punishment = value;
        }

        /// <summary>
        /// Returns true if a moderation action has been indicated.
        /// </summary>
        public bool ShouldModerate => !this.Punishment.Equals(ModerationPunishment.None);

        /// <summary>
        /// The number of seconds to timeout the sender for. Setting this will also set <see cref="ShouldTimeout"/>.
        /// </summary>
        public uint TimeoutSeconds
        {
            get => this._timeoutSeconds;
            set
            {
                if (value > 0)
                {
                    this._punishment = ModerationPunishment.Timeout;
                }

                this._timeoutSeconds = value;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Determines whether <paramref name="b"/> is a harsher ModerationResult than this.
        /// </summary>
        /// <param name="b">A ModerationResult to compare.</param>
        /// <returns><c>true</c> if <paramref name="b"/> is a harsher ModerationResult.</returns>
        public bool IsHarsher(ModerationResult b) => !(b is null) && !this.Punishment.Equals(ModerationPunishment.Ban)
            && (b.Punishment.Equals(ModerationPunishment.Ban)
            || ((b.Punishment.Equals(ModerationPunishment.Timeout) && b.TimeoutSeconds > this.TimeoutSeconds)
            || (!this.Punishment.Equals(ModerationPunishment.Timeout)
            && (!this.Punishment.Equals(ModerationPunishment.Purge)
            && (b.Punishment.Equals(ModerationPunishment.Purge)
            || (!this.Punishment.Equals(ModerationPunishment.Delete)
            && b.Punishment.Equals(ModerationPunishment.Delete)))))));

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Punishment"/> property.
        /// </summary>
        private ModerationPunishment _punishment = ModerationPunishment.None;

        /// <summary>
        /// Field that backs the <see cref="TimeoutSeconds"/> property.
        /// </summary>
        private uint _timeoutSeconds = 0;

        #endregion Private Fields
    }
}
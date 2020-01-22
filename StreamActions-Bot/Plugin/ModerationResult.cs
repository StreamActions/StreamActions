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

namespace StreamActions.Plugin
{
    /// <summary>
    /// Represents the result of a <see cref="PluginManager.OnMessageModeration"/> EventHandler.
    /// </summary>
    public class ModerationResult
    {
        #region Public Properties

        /// <summary>
        /// The message to send to chat after the action is taken.
        /// </summary>
        public string ModerationMessage { get; set; }

        /// <summary>
        /// The reason to attach to the moderation command.
        /// </summary>
        public string ModerationReason { get; set; }

        /// <summary>
        /// Set to true to indicate that the sender should be banned.
        /// </summary>
        public bool ShouldBan
        {
            get => this._shouldBan;
            set
            {
                if (value)
                {
                    this._shouldDelete = false;
                    this._shouldPurge = false;
                    this._shouldTimeout = false;
                }

                this._shouldBan = value;
            }
        }

        /// <summary>
        /// Set to true to indicate that the message should be deleted.
        /// </summary>
        public bool ShouldDelete
        {
            get => this._shouldDelete;
            set
            {
                if (value)
                {
                    this._shouldPurge = false;
                    this._shouldTimeout = false;
                    this._shouldBan = false;
                }

                this._shouldDelete = value;
            }
        }

        /// <summary>
        /// Returns true if a moderation action has been indicated.
        /// </summary>
        public bool ShouldModerate => this.ShouldBan || this.ShouldTimeout || this.ShouldPurge || this.ShouldDelete;

        /// <summary>
        /// Set to true to indicate that the sender should be purged.
        /// </summary>
        public bool ShouldPurge
        {
            get => this._shouldPurge;
            set
            {
                if (value)
                {
                    this._shouldDelete = false;
                    this._shouldTimeout = false;
                    this._shouldBan = false;
                }

                this._shouldPurge = value;
            }
        }

        /// <summary>
        /// Set to true to indicate that the sender should be timed out for <see cref="TimeoutSeconds"/>.
        /// </summary>
        public bool ShouldTimeout
        {
            get => this._shouldTimeout;
            set
            {
                if (value)
                {
                    this._shouldDelete = false;
                    this._shouldPurge = false;
                    this._shouldBan = false;

                    if (this._timeoutSeconds == 0)
                    {
                        this._timeoutSeconds = 1;
                    }
                }

                this._shouldTimeout = value;
            }
        }

        /// <summary>
        /// The number of seconds to timeout the sender for. Setting this will also set <see cref="ShouldTimeout"/>.
        /// </summary>
        public int TimeoutSeconds
        {
            get => this._timeoutSeconds;
            set
            {
                if (value > 0)
                {
                    this._shouldDelete = false;
                    this._shouldPurge = false;
                    this._shouldTimeout = true;
                    this._shouldBan = false;
                }
                else
                {
                    this._shouldTimeout = false;
                    value = 0;
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
        public bool IsHarsher(ModerationResult b) => b is null || this.ShouldBan
                ? false
                : b.ShouldBan
                ? true
                : b.ShouldTimeout && b.TimeoutSeconds > this.TimeoutSeconds
                ? true
                : this.ShouldTimeout
                ? false
                : this.ShouldPurge
                ? false
                : b.ShouldPurge
                ? true
                : this.ShouldDelete
                ? false
                : b.ShouldDelete;

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="ShouldBan"/> property.
        /// </summary>
        private bool _shouldBan = false;

        /// <summary>
        /// Field that backs the <see cref="ShouldDelete"/> property.
        /// </summary>
        private bool _shouldDelete = false;

        /// <summary>
        /// Field that backs the <see cref="ShouldPurge"/> property.
        /// </summary>
        private bool _shouldPurge = false;

        /// <summary>
        /// Field that backs the <see cref="ShouldTimeout"/> property.
        /// </summary>
        private bool _shouldTimeout = false;

        /// <summary>
        /// Field that backs the <see cref="TimeoutSeconds"/> property.
        /// </summary>
        private int _timeoutSeconds = 0;

        #endregion Private Fields
    }
}
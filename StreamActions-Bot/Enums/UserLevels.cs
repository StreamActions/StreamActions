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

using System;

namespace StreamActions.Enums
{
    /// <summary>
    /// An enum set representing all the different levels of permissions a user can have.
    /// This can either be from Twitch or custom made by the user.
    /// </summary>
    [Flags]
    public enum UserLevels
    {
        /// <summary>
        /// Twitch Broadcaster.
        /// </summary>
        Broadcaster = 1,

        /// <summary>
        /// Twitch Staff.
        /// </summary>
        TwitchStaff = 2,

        /// <summary>
        /// Twitch Admin.
        /// </summary>
        TwitchAdmin = 4,

        /// <summary>
        /// Twitch Moderator
        /// </summary>
        Moderator = 8,

        /// <summary>
        /// Twitch VIP.
        /// </summary>
        VIP = 16,

        /// <summary>
        /// Twitch Subscriber.
        /// </summary>
        Subscriber = 32,

        /// <summary>
        /// Twitch default permission, nothing a viewer.
        /// </summary>
        Viewer = 64,

        /// <summary>
        /// Custom level added by the bot user.
        /// </summary>
        Custom = 128
    }
}
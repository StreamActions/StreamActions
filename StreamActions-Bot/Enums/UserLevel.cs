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
    public enum UserLevel
    {
        /// <summary>
        /// Twitch Broadcaster.
        /// </summary>
        Broadcaster,

        /// <summary>
        /// Twitch Staff.
        /// </summary>
        TwitchStaff,

        /// <summary>
        /// Twitch Admin.
        /// </summary>
        TwitchAdmin,

        /// <summary>
        /// Twitch Moderator
        /// </summary>
        Moderator,

        /// <summary>
        /// Twitch VIP.
        /// </summary>
        VIP,

        /// <summary>
        /// Twitch Subscriber.
        /// </summary>
        Subscriber,

        /// <summary>
        /// Twitch default permissions, nothing a viewer.
        /// </summary>
        Viewer,

        /// <summary>
        /// Custom level added by the bot user.
        /// </summary>
        Custom
    }
}
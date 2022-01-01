﻿/*
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

using System;

namespace StreamActions.Enums
{
    /// <summary>
    /// What should a blacklist match for.
    /// </summary>
    [Flags]
    public enum BlocklistMatchTypes
    {
        /// <summary>
        /// Match only the user's name.
        /// </summary>
        Username = 0,

        /// <summary>
        /// Match only the user's message.
        /// </summary>
        Message = 1,

        /// <summary>
        /// Match both the user's name and message.
        /// </summary>
        MessageAndUsername = 2
    }
}
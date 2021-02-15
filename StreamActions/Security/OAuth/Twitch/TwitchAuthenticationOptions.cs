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

using ConcurrentCollections;
using System.Collections.Generic;

namespace StreamActions.Security.OAuth.Twitch
{
    public class TwitchAuthenticationOptions : AspNet.Security.OAuth.Twitch.TwitchAuthenticationOptions
    {
        #region Public Properties

        public new ICollection<string> Scope { get; } = new ConcurrentHashSet<string>();

        #endregion Public Properties

        #region Public Constructors

        public TwitchAuthenticationOptions() : base() => this.Scope.Add("user:email:read");

        #endregion Public Constructors
    }
}
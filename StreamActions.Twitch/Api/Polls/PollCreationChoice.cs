/*
 * Copyright © 2019-2022 StreamActions Team
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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Polls
{
    /// <summary>
    /// Represents a choice in <see cref="PollCreationParameters.Choices"/>.
    /// </summary>
    public record PollCreationChoice
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="title">Text displayed for the choice. Maximum: 25 characters.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="title"/> is longer than 25 characters.</exception>
        public PollCreationChoice(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (title.Length > 25)
            {
                throw new ArgumentOutOfRangeException(nameof(title), title.Length, "Must be 25 characters or less");
            }

            this.Title = title;
        }

        /// <summary>
        /// Text displayed for the choice. Maximum: 25 characters.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; private init; }
    }
}

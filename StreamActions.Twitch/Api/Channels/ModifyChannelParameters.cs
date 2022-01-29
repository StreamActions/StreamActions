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

namespace StreamActions.Twitch.Api.Channels
{
    /// <summary>
    /// The parameters for <see cref="Channel.ModifyChannelInformation(Common.TwitchSession, ModifyChannelParameters)"/>.
    /// </summary>
    public record ModifyChannelParameters
    {
        /// <summary>
        /// Constructor. At least 1 parameter must be a valid, non-null value.
        /// </summary>
        /// <param name="gameId">The current game ID being played on the channel. Use “0” or “” (an empty string) to unset the game. Use <c>null</c> to not change this value.</param>
        /// <param name="broadcasterLanguage">The language of the channel. A language value must be either the ISO 639-1 two-letter code for a supported stream language or “other”. Use <c>null</c> to not change this value.</param>
        /// <param name="title">The title of the stream. Value must not be an empty string. Use <c>null</c> to not change this value.</param>
        /// <param name="delay">Stream delay in seconds. Stream delay is a Twitch Partner feature; trying to set this value for other account types will return a 400 error. Use <c>null</c> to not change this value.</param>
        /// <exception cref="InvalidOperationException">None of the provided parameters was a valid, non-null value.</exception>
        public ModifyChannelParameters(string? gameId, string? broadcasterLanguage, string? title, int? delay)
        {
            if (gameId is null && string.IsNullOrWhiteSpace(broadcasterLanguage) && string.IsNullOrWhiteSpace(title)
                && (delay is null || delay == default(int) || delay < 1))
            {
                throw new InvalidOperationException("At least 1 parameter must have a valid, non-null value.");
            }

            this.GameId = gameId;
            this.BroadcasterLanguage = broadcasterLanguage;
            this.Title = title;
            this.Delay = delay;
        }

        /// <summary>
        /// The current game ID being played on the channel. Use “0” or “” (an empty string) to unset the game. Use <c>null</c> to not change this value.
        /// </summary>
        [JsonPropertyName("game_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GameId { get; private init; }

        /// <summary>
        /// The language of the channel. A language value must be either the ISO 639-1 two-letter code for a supported stream language or “other”. Use <c>null</c> to not change this value.
        /// </summary>
        [JsonPropertyName("broadcaster_language")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BroadcasterLanguage { get; private init; }

        /// <summary>
        /// The title of the stream. Value must not be an empty string. Use <c>null</c> to not change this value.
        /// </summary>
        [JsonPropertyName("title")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; private init; }

        /// <summary>
        /// Stream delay in seconds. Stream delay is a Twitch Partner feature; trying to set this value for other account types will return a 400 error. Use <c>null</c> to not change this value.
        /// </summary>
        [JsonPropertyName("delay")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? Delay { get; private init; }
    }
}

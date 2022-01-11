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

using System.Net;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.API.Common
{
    /// <summary>
    /// Base record for Twitch API responses. Indicates request status code and error message if not success.
    /// </summary>
    public record TwitchResponse
    {
        /// <summary>
        /// The HTTP status code as indicated by the response JSON.
        /// </summary>
        [JsonPropertyName("status")]
        public int Status { get; init; } = 200;

        /// <summary>
        /// The string name of <see cref="Status"/>.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error
        {
            get => this._error == null || this._error.Length == 0 ? Enum.GetName(typeof(HttpStatusCode), this.Status) ?? "Unknown" : this._error;
            init => this._error = value;
        }

        /// <summary>
        /// The message returned with the error, if any.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; init; } = "";

        /// <summary>
        /// Indicates if <see cref="Status"/> is in the 200-299 range.
        /// </summary>
        [JsonIgnore]
        public bool IsSuccessStatusCode => this.Status is >= 200 and <= 299;

        /// <summary>
        /// Indicates if <see cref="Status"/> is in the 400-499 range.
        /// </summary>
        [JsonIgnore]
        public bool IsClientErrorStatusCode => this.Status is >= 400 and <= 499;

        /// <summary>
        /// Indicates if <see cref="Status"/> is in the 500-599 range.
        /// </summary>
        [JsonIgnore]
        public bool IsServerErrorStatusCode => this.Status is >= 500 and <= 599;

        /// <summary>
        /// Backing field for <see cref="Error"/>.
        /// </summary>
        [JsonIgnore]
        private string? _error;
    }
}

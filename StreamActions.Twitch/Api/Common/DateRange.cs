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

using StreamActions.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Common
{
    /// <summary>
    /// A date range in a response object.
    /// </summary>
    public record DateRange
    {
        /// <summary>
        /// Starting date/time for returned reports as a string, in RFC3339 format with the hours, minutes, and seconds zeroed out and the UTC timezone: YYYY-MM-DDT00:00:00Z. This must be on or after January 31, 2018.
        /// </summary>
        [JsonPropertyName("started_at")]
        public string? StartedAtString { get; init; }

        /// <summary>
        /// <see cref="StartedAtString"/> as a <see cref="DateTime"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? StartedAt => this.StartedAtString is not null ? Util.Iso8601ToDateTime(this.StartedAtString) : null;

        /// <summary>
        /// Ending date/time for returned reports as a string, in RFC3339 format with the hours, minutes, and seconds zeroed out and the UTC timezone: YYYY-MM-DDT00:00:00Z. The report covers the entire ending date; e.g., if 2018-05-01T00:00:00Z is specified, the report covers up to 2018-05-01T23:59:59Z.
        /// </summary>
        [JsonPropertyName("ended_at")]
        public string? EndedAtString { get; init; }

        /// <summary>
        /// <see cref="EndedAtString"/> as a <see cref="DateTime"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? EndedAt => this.EndedAtString is not null ? Util.Iso8601ToDateTime(this.EndedAtString) : null;
    }
}

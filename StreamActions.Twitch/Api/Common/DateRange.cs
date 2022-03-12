/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Common
{
    /// <summary>
    /// A date range in a response object.
    /// </summary>
    public record DateRange
    {
        /// <summary>
        /// Starting date/time for returned reports, in RFC3339 format with the hours, minutes, and seconds zeroed out and the UTC timezone: YYYY-MM-DDT00:00:00Z. This must be on or after January 31, 2018.
        /// </summary>
        [JsonPropertyName("started_at")]
        public DateTime? StartedAt { get; init; }

        /// <summary>
        /// Ending date/time for returned reports, in RFC3339 format with the hours, minutes, and seconds zeroed out and the UTC timezone: YYYY-MM-DDT00:00:00Z. The report covers the entire ending date; e.g., if 2018-05-01T00:00:00Z is specified, the report covers up to 2018-05-01T23:59:59Z.
        /// </summary>
        [JsonPropertyName("ended_at")]
        public DateTime? EndedAt { get; init; }
    }
}

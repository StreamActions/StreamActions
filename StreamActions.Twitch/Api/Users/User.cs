/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using System;
using System.Collections.Generic;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamActions.Common.Util;

namespace StreamActions.Twitch.Api.Users
{
    /// <summary>
    /// Represents a Twitch user, providing details like ID, login name, display name, and broadcaster type.
    /// </summary>
    public record User
    {
        /// <summary>
        /// Defines the broadcaster type of a user.
        /// </summary>
        [JsonConverter(typeof(JsonCustomEnumConverter<UserBroadcasterType>))]
        public enum UserBroadcasterType
        {
            /// <summary>
            /// A Twitch Partner.
            /// </summary>
            [JsonStringEnumMember("partner")]
            Partner,
            /// <summary>
            /// A Twitch Affiliate.
            /// </summary>
            [JsonStringEnumMember("affiliate")]
            Affiliate,
            /// <summary>
            /// A normal broadcaster (neither Partner nor Affiliate).
            /// </summary>
            [JsonStringEnumMember("")]
            Normal
        }

        /// <summary>
        /// Defines the user type (e.g., staff, admin).
        /// </summary>
        [JsonConverter(typeof(JsonCustomEnumConverter<UserType>))]
        public enum UserType
        {
            /// <summary>
            /// A Twitch staff member.
            /// </summary>
            [JsonStringEnumMember("staff")]
            Staff,
            /// <summary>
            /// A Twitch administrator.
            /// </summary>
            [JsonStringEnumMember("admin")]
            Admin,
            /// <summary>
            /// A global moderator.
            /// </summary>
            [JsonStringEnumMember("global_mod")]
            GlobalMod,
            /// <summary>
            /// A normal user.
            /// </summary>
            [JsonStringEnumMember("")]
            NormalUser
        }

        /// <summary>
        /// The user's broadcaster type. Possible values are partner, affiliate, or an empty string if normal.
        /// </summary>
        [JsonPropertyName("broadcaster_type")]
        public UserBroadcasterType? BroadcasterType { get; init; }

        /// <summary>
        /// The user’s channel description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>
        /// The user's display name.
        /// </summary>
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        /// <summary>
        /// User’s ID.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>
        /// User’s login name.
        /// </summary>
        [JsonPropertyName("login")]
        public string? Login { get; init; }

        /// <summary>
        /// URL of the user’s offline image.
        /// </summary>
        [JsonPropertyName("offline_image_url")]
        public Uri? OfflineImageUrl { get; init; }

        /// <summary>
        /// URL of the user’s profile image.
        /// </summary>
        [JsonPropertyName("profile_image_url")]
        public Uri? ProfileImageUrl { get; init; }

        /// <summary>
        /// The user's type. Possible values are staff, admin, global_mod, or an empty string if normal.
        /// </summary>
        [JsonPropertyName("type")]
        public UserType? Type { get; init; }

        /// <summary>
        /// Date when the user’s account was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; init; }

        /// <summary>
        /// Gets information about one or more specified Twitch users.
        /// </summary>
        /// <param name="session">The Twitch session.</param>
        /// <param name="ids">A list of user IDs to look up. You may specify a maximum of 100 IDs.</param>
        /// <param name="logins">A list of user login names to look up. You may specify a maximum of 100 login names.</param>
        /// <returns>A task representing the asynchronous operation, with a result of <see cref="JsonApiResponse{T}"/> containing an array of <see cref="User"/> objects.</returns>
        /// <remarks>
        /// <para>If you have the user:read:email scope, the response includes the user's email address. If the user’s account is missing a verified email address, null is returned.</para>
        /// <para>HTTP Response Codes:</para>
        /// <list type="bullet">
        /// <item><term>200 OK</term><description>The request was successful, and the user information is in the body.</description></item>
        /// <item><term>400 Bad Request</term><description>The request was not valid. Common reasons include: One of the query parameters is missing or not valid; or The number of IDs and logins exceeds the maximum limit of 100.</description></item>
        /// <item><term>401 Unauthorized</term><description>The request requires an access token, but the Authorization header is missing an access token or the token is not valid; or the client ID specified in the Client-Id header is not valid.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the combined count of user IDs and login names exceeds 100.</exception>
        public static async Task<ResponseData<User>?> GetUsers(TwitchSession session, IEnumerable<string>? ids = null, IEnumerable<string>? logins = null)
        {
            int idCount = ids?.Count() ?? 0;
            int loginCount = logins?.Count() ?? 0;

            if ((idCount + loginCount) > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(ids) + "," + nameof(logins), (idCount + loginCount), "The total number of supplied IDs and logins cannot exceed 100.").Log(TwitchApi.GetLogger());
            }
            session.RequireToken();
            NameValueCollection queryParameters = new();

            if (ids != null)
            {
                foreach (string idStr in ids)
                {
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        queryParameters.Add("id", idStr);
                    }
                }
            }
            if (logins != null)
            {
                foreach (string loginStr in logins)
                {
                    if (!string.IsNullOrEmpty(loginStr))
                    {
                        queryParameters.Add("login", loginStr);
                    }
                }
            }

            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/users"), queryParameters), session).ConfigureAwait(false);
            return await response.ReadFromJsonAsync<ResponseData<User>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}

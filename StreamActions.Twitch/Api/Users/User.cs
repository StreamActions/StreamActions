/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Users
{
    /// <summary>
    /// Represents a Twitch user, providing details like ID, login name, display name, and broadcaster type.
    /// </summary>
    public sealed record User
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
            [JsonCustomEnum("partner")]
            Partner,
            /// <summary>
            /// A Twitch Affiliate.
            /// </summary>
            [JsonCustomEnum("affiliate")]
            Affiliate,
            /// <summary>
            /// A normal broadcaster (neither Partner nor Affiliate).
            /// </summary>
            [JsonCustomEnum("")]
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
            [JsonCustomEnum("staff")]
            Staff,
            /// <summary>
            /// A Twitch administrator.
            /// </summary>
            [JsonCustomEnum("admin")]
            Admin,
            /// <summary>
            /// A global moderator.
            /// </summary>
            [JsonCustomEnum("global_mod")]
            GlobalMod,
            /// <summary>
            /// A normal user.
            /// </summary>
            [JsonCustomEnum("")]
            NormalUser
        }

        /// <summary>
        /// The user's broadcaster type. Possible values are partner, affiliate, or an empty string if normal.
        /// </summary>
        [JsonPropertyName("broadcaster_type")]
        public UserBroadcasterType? BroadcasterType { get; init; }

        /// <summary>
        /// The user's channel description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>
        /// The user's display name.
        /// </summary>
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        /// <summary>
        /// User's ID.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>
        /// User's login name.
        /// </summary>
        [JsonPropertyName("login")]
        public string? Login { get; init; }

        /// <summary>
        /// URL of the user's offline image.
        /// </summary>
        [JsonPropertyName("offline_image_url")]
        public Uri? OfflineImageUrl { get; init; }

        /// <summary>
        /// URL of the user's profile image.
        /// </summary>
        [JsonPropertyName("profile_image_url")]
        public Uri? ProfileImageUrl { get; init; }

        /// <summary>
        /// The user's type. Possible values are staff, admin, global_mod, or an empty string if normal.
        /// </summary>
        [JsonPropertyName("type")]
        public UserType? Type { get; init; }

        /// <summary>
        /// Date when the user's account was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; init; }

        /// <summary>
        /// The user's verified email address.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This field is only available if the user access token includes the <see cref="Scope.UserReadEmail"/> scope.
        /// </para>
        /// </remarks>
        [JsonPropertyName("email")]
        public string? Email { get; init; }

        /// <summary>
        /// Gets information about one or more specified Twitch users.
        /// </summary>
        /// <param name="session">The Twitch session.</param>
        /// <param name="ids">A list of user IDs to look up. You may specify a combined maximum of 100 IDs and login names.</param>
        /// <param name="logins">A list of user login names to look up. You may specify a combined maximum of 100 IDs and login names.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="User"/> containing the response, or <see langword="null"/> if the request fails.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the combined count of user IDs and login names exceeds 100.</exception>
        /// <remarks>
        /// <para>If you don't specify IDs or login names, the request returns information about the user in the access token if you specify a user access token.</para>
        /// <para>If you have the user:read:email scope, the response includes the user's email address. If the user's account is missing a verified email address, null is returned.</para>
        /// <para>HTTP Response Codes:</para>
        /// <list type="table">
        /// <item>
        /// <term>200 OK</term>
        /// <description>Successfully retrieved the specified users' information.</description>
        /// </item>
        /// <item>
        /// <term>400 Bad Request</term>
        /// <description>The described parameter was missing or invalid.</description>
        /// </item>
        /// <item>
        /// <term>401 Unauthorized</term>
        /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
        /// </item>
        /// </list>
        /// </remarks>
        public static async Task<ResponseData<User>?> GetUsers(TwitchSession session, IEnumerable<string>? ids = null, IEnumerable<string>? logins = null)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
            }

            int idCount = ids?.Count() ?? 0;
            int loginCount = logins?.Count() ?? 0;

            if ((idCount + loginCount) > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(ids) + "," + nameof(logins), idCount + loginCount, "The total number of supplied IDs and logins cannot exceed 100.").Log(TwitchApi.GetLogger());
            }

            session.RequireUserOrAppToken();

            NameValueCollection queryParameters = [];
            if (ids is not null && ids.Any())
            {
                foreach (string idStr in ids)
                {
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        queryParameters.Add("id", idStr);
                    }
                }
            }

            if (logins is not null && logins.Any())
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

        /// <summary>
        /// Updates the specified user's information.
        /// </summary>
        /// <param name="session">The Twitch session.</param>
        /// <param name="description">The string to update the channel's description to. The description is limited to a maximum of 300 characters. To remove the description, specify an empty string.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="User"/> containing the response, or <see langword="null"/> if the request fails.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="description"/> exceeds 300 characters.</exception>
        /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
        /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
        /// <exception cref="TwitchScopeMissingException">Thrown if the <see cref="Scope.UserEdit"/> scope is missing.</exception>
        /// <remarks>
        /// <para>
        /// The user ID in the access token identifies the user whose information you want to update.
        /// </para>
        /// <para>
        /// To include the user's verified email address in the response, the user access token must also include the user:read:email scope.
        /// </para>
        /// <para>
        /// HTTP Response Codes:
        /// <list type="table">
        /// <item>
        /// <term>200 OK</term>
        /// <description>Successfully updated the specified user's information.</description>
        /// </item>
        /// <item>
        /// <term>400 Bad Request</term>
        /// <description>The described parameter was missing or invalid.</description>
        /// </item>
        /// <item>
        /// <term>401 Unauthorized</term>
        /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public static async Task<ResponseData<User>?> UpdateUser(TwitchSession session, string? description = null)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
            }

            if (description?.Length > 300)
            {
                throw new ArgumentOutOfRangeException(nameof(description), description.Length, "The description cannot exceed 300 characters.").Log(TwitchApi.GetLogger());
            }

            session.RequireUserToken(Scope.UserEdit);

            NameValueCollection queryParameters = [];
            if (description is not null)
            {
                queryParameters.Add("description", description);
            }

            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, Util.BuildUri(new("/users"), queryParameters), session).ConfigureAwait(false);
            return await response.ReadFromJsonAsync<ResponseData<User>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}

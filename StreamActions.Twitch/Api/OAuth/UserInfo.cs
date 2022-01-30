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
using StreamActions.Twitch.Api.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.OAuth
{
    /// <summary>
    /// Sends and represents a response for a request for OIDC UserInfo.
    /// </summary>
    public record UserInfo : TwitchResponse
    {
        /// <summary>
        /// Audience: client ID of the application requesting a user's authorization.
        /// </summary>
        [JsonPropertyName("aud")]
        public string? Audience { get; init; }

        /// <summary>
        /// Authorized party: client ID of the application which is being authorized. Currently the same as <see cref="Audience"/>.
        /// </summary>
        [JsonPropertyName("azp")]
        public string? AuthorizedParty { get; init; }

        /// <summary>
        /// Expiration time of the token. This is in UNIX/Epoch format.
        /// </summary>
        [JsonPropertyName("exp")]
        public int? ExpiresUnix { get; init; }

        /// <summary>
        /// <see cref="ExpiresUnix"/> as a <see cref="DateTime"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? Expires => this.ExpiresUnix.HasValue ? DateTime.UnixEpoch.AddSeconds(this.ExpiresUnix.Value) : null;

        /// <summary>
        /// Time when the token was issued. This is in UNIX/Epoch format.
        /// </summary>
        [JsonPropertyName("iat")]
        public int? IssuedUnix { get; init; }

        /// <summary>
        /// <see cref="IssuedUnix"/> as a <see cref="DateTime"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? Issued => this.IssuedUnix.HasValue ? DateTime.UnixEpoch.AddSeconds(this.IssuedUnix.Value) : null;

        /// <summary>
        /// Issuer of the token.
        /// </summary>
        [JsonPropertyName("iss")]
        public string? Issuer { get; init; }

        /// <summary>
        /// User ID of the authorizing user.
        /// </summary>
        [JsonPropertyName("sub")]
        public string? Subject { get; init; }

        /// <summary>
        /// Email address of the authorizing user. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; init; }

        /// <summary>
        /// Email verification state of the authorizing user. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonPropertyName("email_verified")]
        public bool? EmailVerified { get; init; }

        /// <summary>
        /// Profile image URL of the authorizing user. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonPropertyName("picture")]
        public string? Picture { get; init; }

        /// <summary>
        /// <see cref="Picture"/> as a <see cref="Uri"/>. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? PictureUri => this.Picture is not null ? new(this.Picture) : null;

        /// <summary>
        /// Display name of the authorizing user. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonPropertyName("preferred_username")]
        public string? PreferredUsername { get; init; }

        /// <summary>
        /// Date of the last update to the authorizing user's profile. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonPropertyName("updated_at")]
        public string? UpdatedAtString { get; init; }

        /// <summary>
        /// <see cref="UpdatedAtString"/> as a <see cref="DateTime"/>. Only present if the claim was requested for UserInfo.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? UpdatedAt => Util.Iso8601ToDateTime(this.UpdatedAtString ?? "");

        /// <summary>
        /// The UserInfo endpoint allows you to fetch claims originally provided in the authorization request outside of the ID token.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to get user info for.</param>
        /// <param name="baseAddress">The uri to the UserInfo endpoint. <c>null</c> for default.</param>
        /// <returns>A <see cref="UserInfo"/> with the response data.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
        public static async Task<UserInfo?> GetUserInfo(TwitchSession session, string? baseAddress = null)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            baseAddress ??= Token._openIdConnectConfiguration.Value.UserInfoEndpoint;

            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new(baseAddress), session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<UserInfo>().ConfigureAwait(false);
        }
    }
}

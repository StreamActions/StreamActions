/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

using StreamActions.Common.Attributes;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// Sends and represents a response for a request for OIDC UserInfo.
/// </summary>
[ETag("https://dev.twitch.tv/docs/authentication/getting-tokens-oidc", "4fecd89165e854442989c1f5f98d8642a645ff36f81c7797764a58ba29e879e6",
    "2022-10-16T21:22Z", new string[] { "-stripblank", "-strip", "-findfirst", "'<div class=\"main\">'", "-findlast",
        "'<div class=\"subscribe-footer\">'", "-remre", "'cloudcannon[^\"]*'" })]
public sealed record UserInfo : JsonApiResponse
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
    [JsonIgnore]
    public DateTime? Expires => this.ExpiresUnix.HasValue ? DateTime.UnixEpoch.AddSeconds(this.ExpiresUnix.Value) : null;

    /// <summary>
    /// Time when the token was issued. This is in UNIX/Epoch format.
    /// </summary>
    [JsonPropertyName("iat")]
    public int? IssuedUnix { get; init; }

    /// <summary>
    /// <see cref="IssuedUnix"/> as a <see cref="DateTime"/>.
    /// </summary>
    [JsonIgnore]
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
    [JsonIgnore]
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
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// The UserInfo endpoint allows you to fetch claims originally provided in the authorization request outside of the ID token.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to get user info for.</param>
    /// <param name="baseAddress">The uri to the UserInfo endpoint. <see langword="null"/> for default.</param>
    /// <returns>A <see cref="UserInfo"/> with the response data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.OpenID"/>.</exception>
    public static async Task<UserInfo?> GetUserInfo(TwitchSession session, string? baseAddress = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.OpenID);

        baseAddress ??= Token._openIdConnectConfiguration.Value.UserInfoEndpoint;

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new(baseAddress), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<UserInfo>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}

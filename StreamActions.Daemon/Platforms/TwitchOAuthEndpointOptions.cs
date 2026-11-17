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

namespace StreamActions.Daemon.Platforms;

/// <summary>
/// Configurable Twitch OAuth 2.0 and OpenID Connect endpoint URLs, allowing test overrides against local mock servers.
/// </summary>
internal sealed class TwitchOAuthEndpointOptions
{
    /// <summary>
    /// Default Twitch OAuth 2.0 authorization endpoint URL.
    /// </summary>
    public const string DefaultAuthorizeEndpoint = "https://id.twitch.tv/oauth2/authorize";

    /// <summary>
    /// Default Twitch OAuth 2.0 token endpoint URL.
    /// </summary>
    public const string DefaultTokenEndpoint = "https://id.twitch.tv/oauth2/token";

    /// <summary>
    /// Default Twitch OAuth 2.0 token validation endpoint URL.
    /// </summary>
    public const string DefaultValidateEndpoint = "https://id.twitch.tv/oauth2/validate";

    /// <summary>
    /// Default Twitch OAuth 2.0 token revocation endpoint URL.
    /// </summary>
    public const string DefaultRevokeEndpoint = "https://id.twitch.tv/oauth2/revoke";

    /// <summary>
    /// Default Twitch OpenID Connect UserInfo endpoint URL.
    /// </summary>
    public const string DefaultUserInfoEndpoint = "https://id.twitch.tv/oauth2/userinfo";

    /// <summary>
    /// Default Twitch OpenID Connect discovery configuration endpoint URL.
    /// </summary>
    public const string DefaultOpenIdConfigurationEndpoint = "https://id.twitch.tv/oauth2/.well-known/openid-configuration";

    /// <summary>
    /// Gets or sets the Twitch authorization endpoint URL.
    /// </summary>
    public string AuthorizeEndpoint { get; set; } = DefaultAuthorizeEndpoint;

    /// <summary>
    /// Gets or sets the Twitch token endpoint URL.
    /// </summary>
    public string TokenEndpoint { get; set; } = DefaultTokenEndpoint;

    /// <summary>
    /// Gets or sets the Twitch validation endpoint URL.
    /// </summary>
    public string ValidateEndpoint { get; set; } = DefaultValidateEndpoint;

    /// <summary>
    /// Gets or sets the Twitch revocation endpoint URL.
    /// </summary>
    public string RevokeEndpoint { get; set; } = DefaultRevokeEndpoint;

    /// <summary>
    /// Gets or sets the Twitch OIDC UserInfo endpoint URL.
    /// </summary>
    public string UserInfoEndpoint { get; set; } = DefaultUserInfoEndpoint;

    /// <summary>
    /// Gets or sets the Twitch OIDC discovery configuration URL.
    /// </summary>
    public string OpenIdConfigurationEndpoint { get; set; } = DefaultOpenIdConfigurationEndpoint;
}

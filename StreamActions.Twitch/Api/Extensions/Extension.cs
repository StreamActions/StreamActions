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
using StreamActions.Common.Exceptions;
using StreamActions.Common.Extensions;
using StreamActions.Twitch.Api.Common;
using StreamActions.Common.Logger;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Sends and represents a response element for a request for extensions.
/// </summary>
public sealed record Extension
{
    /// <summary>
    /// The name of the user or organization that owns the extension.
    /// </summary>
    [JsonPropertyName("author_name")]
    public string? AuthorName { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the extension has features that use Bits.
    /// </summary>
    [JsonPropertyName("bits_enabled")]
    public bool? BitsEnabled { get; init; }

    /// <summary>
    /// A Boolean value that determines whether a user can install the extension on their channel.
    /// </summary>
    [JsonPropertyName("can_install")]
    public bool? CanInstall { get; init; }

    /// <summary>
    /// The location of where the extension's configuration is stored.
    /// </summary>
    [JsonPropertyName("configuration_location")]
    public string? ConfigurationLocation { get; init; }

    /// <summary>
    /// A longer description of the extension. It appears on the details page.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// A URL to the extension's Terms of Service.
    /// </summary>
    [JsonPropertyName("eula_tos_url")]
    public string? EulaTosUrl { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the extension can communicate with the installed channel's chat.
    /// </summary>
    [JsonPropertyName("has_chat_support")]
    public bool? HasChatSupport { get; init; }

    /// <summary>
    /// A URL to the default icon that's displayed in the Extensions directory.
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; init; }

    /// <summary>
    /// A dictionary that contains URLs to different sizes of the default icon.
    /// </summary>
    [JsonPropertyName("icon_urls")]
    public IReadOnlyDictionary<string, string>? IconUrls { get; init; }

    /// <summary>
    /// The extension's ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The extension's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// A URL to the extension's privacy policy.
    /// </summary>
    [JsonPropertyName("privacy_policy_url")]
    public string? PrivacyPolicyUrl { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the extension wants to explicitly ask viewers to link their Twitch identity.
    /// </summary>
    [JsonPropertyName("request_identity_link")]
    public bool? RequestIdentityLink { get; init; }

    /// <summary>
    /// A list of URLs to screenshots that are shown in the Extensions marketplace.
    /// </summary>
    [JsonPropertyName("screenshot_urls")]
    public IReadOnlyList<string>? ScreenshotUrls { get; init; }

    /// <summary>
    /// The extension's state.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>
    /// Indicates whether the extension can view the user's subscription level on the channel that the extension is installed on.
    /// </summary>
    [JsonPropertyName("subscriptions_support_level")]
    public string? SubscriptionsSupportLevel { get; init; }

    /// <summary>
    /// A short description of the extension that streamers see when hovering over the discovery splash screen in the Extensions manager.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    /// <summary>
    /// The email address that users use to get support for the extension.
    /// </summary>
    [JsonPropertyName("support_email")]
    public string? SupportEmail { get; init; }

    /// <summary>
    /// The extension's version number.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// A brief description displayed on the channel to explain how the extension works.
    /// </summary>
    [JsonPropertyName("viewer_summary")]
    public string? ViewerSummary { get; init; }

    /// <summary>
    /// Describes all views-related information such as how the extension is displayed on mobile devices.
    /// </summary>
    [JsonPropertyName("views")]
    public ExtensionViews? Views { get; init; }

    /// <summary>
    /// Allowlisted configuration URLs for displaying the extension.
    /// </summary>
    [JsonPropertyName("allowlisted_config_urls")]
    public IReadOnlyList<string>? AllowlistedConfigUrls { get; init; }

    /// <summary>
    /// Allowlisted panel URLs for displaying the extension.
    /// </summary>
    [JsonPropertyName("allowlisted_panel_urls")]
    public IReadOnlyList<string>? AllowlistedPanelUrls { get; init; }

    /// <summary>
    /// Gets information about an extension.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Requires a signed JSON Web Token (JWT) created by an Extension Backend Service (EBS).</param>
    /// <param name="extensionId">The ID of the extension to get.</param>
    /// <param name="extensionVersion">The version of the extension to get. If not specified, it returns the latest, released version.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Extension"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="extensionId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.Jwt"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of extensions.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The JWT token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The extension in the extension_id query parameter was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Extension>?> GetExtensions(TwitchSession session, string extensionId, string? extensionVersion = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireJwtToken();

        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentNullException(nameof(extensionId)).Log(TwitchApi.GetLogger());
        }

        NameValueCollection queryParams = new()
        {
            { "extension_id", extensionId }
        };

        if (!string.IsNullOrWhiteSpace(extensionVersion))
        {
            queryParams.Add("extension_version", extensionVersion);
        }

        Uri uri = Util.BuildUri(new("/extensions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Extension>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about a released extension. Returns the extension if its state is Released.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="extensionId">The ID of the extension to get.</param>
    /// <param name="extensionVersion">The version of the extension to get. If not specified, it returns the latest version.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Extension"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="extensionId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/> or <see cref="TwitchToken.TokenType.App"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the extension.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The extension specified in the extension_id query parameter was not found or is not released.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Extension>?> GetReleasedExtensions(TwitchSession session, string extensionId, string? extensionVersion = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentNullException(nameof(extensionId)).Log(TwitchApi.GetLogger());
        }

        NameValueCollection queryParams = new()
        {
            { "extension_id", extensionId }
        };

        if (!string.IsNullOrWhiteSpace(extensionVersion))
        {
            queryParams.Add("extension_version", extensionVersion);
        }

        Uri uri = Util.BuildUri(new("/extensions/released"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Extension>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}

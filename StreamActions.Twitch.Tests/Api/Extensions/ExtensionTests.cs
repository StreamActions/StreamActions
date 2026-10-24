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

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using StreamActions.Common.Exceptions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Extensions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="Extension"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test execution.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetExtensions

    [Fact]
    [Trait("Member", "GetExtensions")]
    public async Task GetExtensions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Extension.GetExtensions(null!, "ext123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetExtensions")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetExtensions_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await Extension.GetExtensions(session, invalidExtensionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "GetExtensions")]
    public async Task GetExtensions_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Extension.GetExtensions(session, "ext123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetExtensions")]
    public async Task GetExtensions_WhenValidRequest_SendsGetAndReturnsExtensionData()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "author_name": "Twitch Developers",
              "bits_enabled": true,
              "can_install": false,
              "configuration_location": "hosted",
              "description": "An extension for testing all the features that we add to extensions",
              "eula_tos_url": "https://example.com/tos",
              "has_chat_support": true,
              "icon_url": "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/logob6c995d8-8b45-48cc-a748-b256e92ac1cd",
              "icon_urls": {
                "100x100": "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/logob6c995d8-8b45-48cc-a748-b256e92ac1cd",
                "24x24": "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/taskbar905b19da-e7e5-4d8f-beb7-f543a861ac1e",
                "300x200": "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/discoveryd9545b2c-5474-46d7-a523-1c3835d862ce"
              },
              "id": "pgn0bjv51epi7eaekt53tovjnc82qo",
              "name": "Official Developers Demo",
              "privacy_policy_url": "https://example.com/privacy",
              "request_identity_link": true,
              "screenshot_urls": [
                "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/screenshotbdec475d-3d2f-4378-b334-941dfddc897a"
              ],
              "state": "Released",
              "subscriptions_support_level": "optional",
              "summary": "Test ALL the extensions features!",
              "support_email": "dx-extensions-test-dev@justin.tv",
              "version": "0.0.9",
              "viewer_summary": "Test ALL the extensions features!",
              "views": {
                "mobile": {
                  "viewer_url": "https://pgn0bjv51epi7eaekt53tovjnc82qo.ext-twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.9/f9a0d8aae0f9dd0b2d6ef3416b96bc79/index.html"
                },
                "panel": {
                  "viewer_url": "https://pgn0bjv51epi7eaekt53tovjnc82qo.ext-twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.9/f9a0d8aae0f9dd0b2d6ef3416b96bc79/index.html",
                  "height": 300,
                  "can_link_external_content": false
                },
                "video_overlay": {
                  "viewer_url": "https://pgn0bjv51epi7eaekt53tovjnc82qo.ext-twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.9/f9a0d8aae0f9dd0b2d6ef3416b96bc79/index.html",
                  "can_link_external_content": false
                },
                "component": {
                  "viewer_url": "https://pgn0bjv51epi7eaekt53tovjnc82qo.ext-twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.9/f9a0d8aae0f9dd0b2d6ef3416b96bc79/index.html",
                  "aspect_ratio_x": 48000,
                  "aspect_ratio_y": 36000,
                  "autoscale": true,
                  "scale_pixels": 1024,
                  "target_height": 5333,
                  "can_link_external_content": false
                }
              },
              "allowlisted_config_urls": [],
              "allowlisted_panel_urls": []
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        ResponseData<Extension>? response = await Extension.GetExtensions(session, "pgn0bjv51epi7eaekt53tovjnc82qo", "0.0.9").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Extension item = response.Data[0];
        _ = item.AuthorName.Should().Be("Twitch Developers");
        _ = item.BitsEnabled.Should().BeTrue();
        _ = item.CanInstall.Should().BeFalse();
        _ = item.ConfigurationLocation.Should().Be("hosted");
        _ = item.Description.Should().Be("An extension for testing all the features that we add to extensions");
        _ = item.EulaTosUrl.Should().Be(new Uri("https://example.com/tos"));
        _ = item.HasChatSupport.Should().BeTrue();
        _ = item.IconUrl.Should().Be(new Uri("https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/logob6c995d8-8b45-48cc-a748-b256e92ac1cd"));
        _ = item.IconUrls.Should().NotBeNull();
        _ = item.IconUrls!["100x100"].Should().Be("https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/logob6c995d8-8b45-48cc-a748-b256e92ac1cd");
        _ = item.Id.Should().Be("pgn0bjv51epi7eaekt53tovjnc82qo");
        _ = item.Name.Should().Be("Official Developers Demo");
        _ = item.PrivacyPolicyUrl.Should().Be(new Uri("https://example.com/privacy"));
        _ = item.RequestIdentityLink.Should().BeTrue();
        _ = item.ScreenshotUrls.Should().HaveCount(1);
        _ = item.State.Should().Be("Released");
        _ = item.SubscriptionsSupportLevel.Should().Be("optional");
        _ = item.Summary.Should().Be("Test ALL the extensions features!");
        _ = item.SupportEmail.Should().Be("dx-extensions-test-dev@justin.tv");
        _ = item.Version.Should().Be("0.0.9");
        _ = item.ViewerSummary.Should().Be("Test ALL the extensions features!");

        _ = item.Views.Should().NotBeNull();
        _ = item.Views!.Mobile!.ViewerUrl!.ToString().Should().Contain("index.html");
        _ = item.Views.Panel!.Height.Should().Be(300);
        _ = item.Views.Panel.CanLinkExternalContent.Should().BeFalse();
        _ = item.Views.VideoOverlay!.CanLinkExternalContent.Should().BeFalse();
        _ = item.Views.Component!.AspectRatioX.Should().Be(48000);
        _ = item.Views.Component.AspectRatioY.Should().Be(36000);
        _ = item.Views.Component.Autoscale.Should().BeTrue();
        _ = item.Views.Component.ScalePixels.Should().Be(1024);
        _ = item.Views.Component.TargetHeight.Should().Be(5333);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/extensions");
        _ = request.QueryString.Should().Contain("extension_id=pgn0bjv51epi7eaekt53tovjnc82qo");
        _ = request.QueryString.Should().Contain("extension_version=0.0.9");
    }

    #endregion GetExtensions

    #region GetReleasedExtensions

    [Fact]
    [Trait("Member", "GetReleasedExtensions")]
    public async Task GetReleasedExtensions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Extension.GetReleasedExtensions(null!, "ext123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetReleasedExtensions")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetReleasedExtensions_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Extension.GetReleasedExtensions(session, invalidExtensionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "GetReleasedExtensions")]
    public async Task GetReleasedExtensions_WhenSessionTokenIsNotUserOrApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await Extension.GetReleasedExtensions(session, "ext123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetReleasedExtensions")]
    public async Task GetReleasedExtensions_WhenValidRequest_SendsGetAndReturnsReleasedExtensionData()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "author_name": "Twitch Developer Experience",
              "bits_enabled": true,
              "can_install": false,
              "configuration_location": "hosted",
              "description": "An extension for testing all the features that we add to extensions",
              "eula_tos_url": "https://example.com/tos",
              "has_chat_support": true,
              "icon_url": "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/logob6c995d8-8b45-48cc-a748-b256e92ac1cd",
              "icon_urls": {
                "100x100": "https://extensions-discovery-images.twitch.tv/pgn0bjv51epi7eaekt53tovjnc82qo/0.0.8/logob6c995d8-8b45-48cc-a748-b256e92ac1cd"
              },
              "id": "pgn0bjv51epi7eaekt53tovjnc82qo",
              "name": "Official Developer Experience Demo",
              "privacy_policy_url": "https://example.com/privacy",
              "request_identity_link": true,
              "screenshot_urls": [],
              "state": "Released",
              "subscriptions_support_level": "optional",
              "summary": "Test ALL the extensions features!",
              "support_email": "dx-extensions-test-dev@justin.tv",
              "version": "0.0.9",
              "viewer_summary": "Test ALL the extensions features!",
              "views": {
                "panel": {
                  "viewer_url": "https://example.com/panel.html",
                  "height": 300,
                  "can_link_external_content": false
                }
              },
              "allowlisted_config_urls": [],
              "allowlisted_panel_urls": []
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions/released", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Extension>? response = await Extension.GetReleasedExtensions(session, "pgn0bjv51epi7eaekt53tovjnc82qo").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Extension item = response.Data[0];
        _ = item.AuthorName.Should().Be("Twitch Developer Experience");
        _ = item.Name.Should().Be("Official Developer Experience Demo");
        _ = item.State.Should().Be("Released");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/extensions/released");
        _ = request.QueryString.Should().Contain("extension_id=pgn0bjv51epi7eaekt53tovjnc82qo");
    }

    #endregion GetReleasedExtensions
}

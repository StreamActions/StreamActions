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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Users;
using StreamActions.Twitch.Api.Users.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Users;

/// <summary>
/// Provides unit tests for the <see cref="UserActiveExtensions"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class UserActiveExtensionsTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UserActiveExtensionsTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public UserActiveExtensionsTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetUserActiveExtensions

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.GetUserActiveExtensions"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserActiveExtensions")]
    public async Task GetUserActiveExtensions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await UserActiveExtensions.GetUserActiveExtensions(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.GetUserActiveExtensions"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserActiveExtensions")]
    public async Task GetUserActiveExtensions_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await UserActiveExtensions.GetUserActiveExtensions(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.GetUserActiveExtensions"/> returns active extensions on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserActiveExtensions")]
    public async Task GetUserActiveExtensions_WhenRequestSucceeds_ReturnsActiveExtensions()
    {
        this._fixture.RegisterEndpoint("/helix/users/extensions", HttpStatusCode.OK, @"{
  ""data"": {
    ""panel"": {
      ""1"": {
        ""active"": true,
        ""id"": ""upl85usdumiojfu"",
        ""version"": ""0.0.1"",
        ""name"": ""SoundAlerts""
      }
    },
    ""overlay"": {
      ""1"": {
        ""active"": true,
        ""id"": ""overlay_id"",
        ""version"": ""0.0.2"",
        ""name"": ""OverlayExtension""
      }
    },
    ""component"": {
      ""1"": {
        ""active"": true,
        ""id"": ""comp_id"",
        ""version"": ""0.0.3"",
        ""name"": ""ComponentExtension"",
        ""x"": 100,
        ""y"": 200
      }
    }
  }
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseDataObject<UserActiveExtensions>? response = await UserActiveExtensions.GetUserActiveExtensions(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();

        UserActiveExtensions activeExts = response.Data;
        _ = activeExts.Panel.Should().NotBeNull();
        _ = activeExts.Panel.Should().ContainKey("1");
        UserActiveExtension panel = activeExts.Panel["1"];
        _ = panel.Active.Should().BeTrue();
        _ = panel.Id.Should().Be("upl85usdumiojfu");
        _ = panel.Version.Should().Be("0.0.1");
        _ = panel.Name.Should().Be("SoundAlerts");

        _ = activeExts.Overlay.Should().NotBeNull();
        _ = activeExts.Overlay.Should().ContainKey("1");
        UserActiveExtension overlay = activeExts.Overlay["1"];
        _ = overlay.Active.Should().BeTrue();
        _ = overlay.Id.Should().Be("overlay_id");
        _ = overlay.Version.Should().Be("0.0.2");
        _ = overlay.Name.Should().Be("OverlayExtension");

        _ = activeExts.Component.Should().NotBeNull();
        _ = activeExts.Component.Should().ContainKey("1");
        UserActiveComponentExtension comp = activeExts.Component["1"];
        _ = comp.Active.Should().BeTrue();
        _ = comp.Id.Should().Be("comp_id");
        _ = comp.Version.Should().Be("0.0.3");
        _ = comp.Name.Should().Be("ComponentExtension");
        _ = comp.X.Should().Be(100);
        _ = comp.Y.Should().Be(200);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/users/extensions");
        _ = request.QueryString.Should().Contain("user_id=141981764");
    }

    #endregion GetUserActiveExtensions

    #region UpdateUserExtensions

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.UpdateUserExtensions"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserExtensions")]
    public async Task UpdateUserExtensions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        UpdateUserExtensionsParameters parameters = new();
        Func<Task> action = async () => await UserActiveExtensions.UpdateUserExtensions(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.UpdateUserExtensions"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserExtensions")]
    public async Task UpdateUserExtensions_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserEditBroadcast);
        Func<Task> action = async () => await UserActiveExtensions.UpdateUserExtensions(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.UpdateUserExtensions"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserExtensions")]
    public async Task UpdateUserExtensions_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        UpdateUserExtensionsParameters parameters = new();

        Func<Task> action = async () => await UserActiveExtensions.UpdateUserExtensions(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.UpdateUserExtensions"/> throws <see cref="TwitchScopeMissingException"/> when UserEditBroadcast scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserExtensions")]
    public async Task UpdateUserExtensions_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        UpdateUserExtensionsParameters parameters = new();

        Func<Task> action = async () => await UserActiveExtensions.UpdateUserExtensions(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserActiveExtensions.UpdateUserExtensions"/> updates user extensions on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserExtensions")]
    public async Task UpdateUserExtensions_WhenRequestSucceeds_ReturnsUpdatedExtensions()
    {
        this._fixture.RegisterEndpoint("/helix/users/extensions", HttpStatusCode.OK, @"{
  ""data"": {
    ""panel"": {
      ""1"": {
        ""active"": true,
        ""id"": ""upl85usdumiojfu"",
        ""version"": ""0.0.1"",
        ""name"": ""SoundAlerts""
      }
    }
  }
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserEditBroadcast);
        UpdateUserExtensionsParameters parameters = new()
        {
            Data = new UserActiveExtensions
            {
                Panel = new Dictionary<string, UserActiveExtension>
                {
                    ["1"] = new UserActiveExtension
                    {
                        Active = true,
                        Id = "upl85usdumiojfu",
                        Version = "0.0.1"
                    }
                }
            }
        };

        ResponseDataObject<UserActiveExtensions>? response = await UserActiveExtensions.UpdateUserExtensions(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Panel.Should().NotBeNull();
        _ = response.Data.Panel.Should().ContainKey("1");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/users/extensions");
        _ = request.Body.Should().Contain("\"data\"");
    }

    #endregion UpdateUserExtensions
}

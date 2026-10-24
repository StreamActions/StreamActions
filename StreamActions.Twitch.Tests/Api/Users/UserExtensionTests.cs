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
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Users;

/// <summary>
/// Provides unit tests for the <see cref="UserExtension"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class UserExtensionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UserExtensionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public UserExtensionTests(TwitchKestrelServerFixture fixture)
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

    #region GetUserExtensions

    /// <summary>
    /// Tests that <see cref="UserExtension.GetUserExtensions"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserExtensions")]
    public async Task GetUserExtensions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await UserExtension.GetUserExtensions(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="UserExtension.GetUserExtensions"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserExtensions")]
    public async Task GetUserExtensions_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await UserExtension.GetUserExtensions(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserExtension.GetUserExtensions"/> throws <see cref="TwitchScopeMissingException"/> when required scopes are missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserExtensions")]
    public async Task GetUserExtensions_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await UserExtension.GetUserExtensions(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="UserExtension.GetUserExtensions"/> returns extensions on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserExtensions")]
    public async Task GetUserExtensions_WhenRequestSucceeds_ReturnsExtensions()
    {
        this._fixture.RegisterEndpoint("/helix/users/extensions/list", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""upl85usdumiojfu"",
      ""version"": ""0.0.1"",
      ""name"": ""SoundAlerts"",
      ""can_activate"": true,
      ""type"": [
        ""panel""
      ]
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBroadcast);
        ResponseData<UserExtension>? response = await UserExtension.GetUserExtensions(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        UserExtension ext = response.Data[0];
        _ = ext.Id.Should().Be("upl85usdumiojfu");
        _ = ext.Version.Should().Be("0.0.1");
        _ = ext.Name.Should().Be("SoundAlerts");
        _ = ext.CanActivate.Should().BeTrue();
        _ = ext.Type.Should().NotBeNull();
        _ = ext.Type.Should().Contain(UserExtension.UserExtensionTypes.Panel);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/users/extensions/list");
    }

    #endregion GetUserExtensions
}

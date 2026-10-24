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
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Users;

/// <summary>
/// Provides unit tests for the <see cref="User"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class UserTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public UserTests(TwitchKestrelServerFixture fixture)
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

    #region GetUsers

    /// <summary>
    /// Tests that <see cref="User.GetUsers"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUsers")]
    public async Task GetUsers_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await User.GetUsers(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="User.GetUsers"/> throws <see cref="ArgumentOutOfRangeException"/> when combined count of ids and logins exceeds 100.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUsers")]
    public async Task GetUsers_WhenCombinedIdsAndLoginsExceed100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> ids = Enumerable.Range(1, 60).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
        List<string> logins = Enumerable.Range(1, 41).Select(i => $"login{i}").ToList();

        Func<Task> action = async () => await User.GetUsers(session, ids: ids, logins: logins).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("ids,logins");
    }

    /// <summary>
    /// Tests that <see cref="User.GetUsers"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUsers")]
    public async Task GetUsers_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await User.GetUsers(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="User.GetUsers"/> returns users on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUsers")]
    public async Task GetUsers_WhenRequestSucceeds_ReturnsUsers()
    {
        this._fixture.RegisterEndpoint("/helix/users", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""141981764"",
      ""login"": ""twitchdev"",
      ""display_name"": ""TwitchDev"",
      ""type"": ""staff"",
      ""broadcaster_type"": ""partner"",
      ""description"": ""Supporting all developers building experiences for Twitch."",
      ""profile_image_url"": ""https://static-cdn.jtvnw.net/jtv_user_pictures/8a6381c7-d0c0-4576-b17c-30f9d6add47f-profile_image-300x300.png"",
      ""offline_image_url"": ""https://static-cdn.jtvnw.net/jtv_user_pictures/3f13ab61-ec78-4fe6-8481-8682cb3b0ac2-channel_offline_image-1920x1080.png"",
      ""created_at"": ""2016-12-14T20:32:28Z"",
      ""email"": ""not-real@email.com""
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        ResponseData<User>? response = await User.GetUsers(session, ids: ["141981764"], logins: ["twitchdev"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        User user = response.Data[0];
        _ = user.Id.Should().Be("141981764");
        _ = user.Login.Should().Be("twitchdev");
        _ = user.DisplayName.Should().Be("TwitchDev");
        _ = user.Type.Should().Be(User.UserType.Staff);
        _ = user.BroadcasterType.Should().Be(User.UserBroadcasterType.Partner);
        _ = user.Description.Should().Be("Supporting all developers building experiences for Twitch.");
        _ = user.ProfileImageUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/jtv_user_pictures/8a6381c7-d0c0-4576-b17c-30f9d6add47f-profile_image-300x300.png"));
        _ = user.OfflineImageUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/jtv_user_pictures/3f13ab61-ec78-4fe6-8481-8682cb3b0ac2-channel_offline_image-1920x1080.png"));
        _ = user.CreatedAt.Should().Be(DateTime.Parse("2016-12-14T20:32:28Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = user.Email.Should().Be("not-real@email.com");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/users");
        _ = request.QueryString.Should().Contain("id=141981764");
        _ = request.QueryString.Should().Contain("login=twitchdev");
    }

    #endregion GetUsers

    #region UpdateUser

    /// <summary>
    /// Tests that <see cref="User.UpdateUser"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUser")]
    public async Task UpdateUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await User.UpdateUser(null!, "new description").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="User.UpdateUser"/> throws <see cref="ArgumentOutOfRangeException"/> when description exceeds 300 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUser")]
    public async Task UpdateUser_WhenDescriptionExceeds300Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserEdit);
        string longDesc = new('a', 301);

        Func<Task> action = async () => await User.UpdateUser(session, longDesc).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("description");
    }

    /// <summary>
    /// Tests that <see cref="User.UpdateUser"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUser")]
    public async Task UpdateUser_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await User.UpdateUser(session, "new description").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="User.UpdateUser"/> throws <see cref="TwitchScopeMissingException"/> when UserEdit scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUser")]
    public async Task UpdateUser_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await User.UpdateUser(session, "new description").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="User.UpdateUser"/> updates user description on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUser")]
    public async Task UpdateUser_WhenRequestSucceeds_ReturnsUpdatedUser()
    {
        this._fixture.RegisterEndpoint("/helix/users", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""141981764"",
      ""login"": ""twitchdev"",
      ""display_name"": ""TwitchDev"",
      ""type"": """",
      ""broadcaster_type"": ""partner"",
      ""description"": ""new description""
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserEdit);
        ResponseData<User>? response = await User.UpdateUser(session, "new description").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        User user = response.Data[0];
        _ = user.Id.Should().Be("141981764");
        _ = user.Description.Should().Be("new description");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/users");
        _ = request.QueryString.Should().Contain("description=new%20description");
    }

    #endregion UpdateUser
}

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

using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="Moderator"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ModeratorTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ModeratorTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ModeratorTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetModerators

    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Moderator.GetModerators(null!, "1234").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetModerators")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetModerators_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        Func<Task> action = async () => await Moderator.GetModerators(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenUserIdExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        List<string> userIds = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];
        Func<Task> action = async () => await Moderator.GetModerators(session, "1234", userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenAfterIsWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        Func<Task> action = async () => await Moderator.GetModerators(session, "1234", after: "   ").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("after");
    }

    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Moderator.GetModerators(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.GetModerators"/> throws <see cref="InvalidOperationException"/> when the token has an empty OAuth string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModerationRead]
            }
        };

        Func<Task> action = async () => await Moderator.GetModerators(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.GetModerators"/> succeeds with an App token.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenAppToken_ReturnsModerators()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/moderators", json);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModerationRead);
        ResponseData<Moderator>? response = await Moderator.GetModerators(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.GetModerators"/> includes multiple user IDs in query parameters.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenMultipleUserIds_IncludesAllInQueryString()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/moderators", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await Moderator.GetModerators(session, "1234", ["111", "222"]).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("user_id=111");
        _ = request.QueryString.Should().Contain("user_id=222");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.GetModerators"/> uses default optional parameters and omits optional parameters from the query string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenDefaultOptionalParameters_OmitsOptionalParametersFromQueryString()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/moderators", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await Moderator.GetModerators(session, "1234").ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().NotContain("user_id");
        _ = request.QueryString.Should().NotContain("after");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.GetModerators"/> clamps first between 1 and 100.
    /// </para>
    /// </summary>
    /// <param name="first">The input first value.</param>
    /// <param name="expectedFirst">The expected clamped first value in query string.</param>
    [Theory]
    [Trait("Member", "GetModerators")]
    [InlineData(0, "1")]
    [InlineData(200, "100")]
    public async Task GetModerators_WhenFirstOutOfRange_ClampsFirst(int first, string expectedFirst)
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/moderators", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await Moderator.GetModerators(session, "1234", first: first).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain($"first={expectedFirst}");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.GetModerators"/> succeeds with alternative scopes.
    /// </para>
    /// </summary>
    /// <param name="scopeName">The name of the alternative scope.</param>
    [Theory]
    [Trait("Member", "GetModerators")]
    [InlineData("channel:manage:moderators")]
    [InlineData("moderator:read:moderators")]
    public async Task GetModerators_WhenAlternativeScopes_ReturnsModerators(string scopeName)
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/moderators", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<Moderator>? response = await Moderator.GetModerators(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Member", "GetModerators")]
    public async Task GetModerators_WhenValidRequest_ReturnsModeratorsList()
    {
        const string json = "{\"data\":[{\"user_id\":\"424596340\",\"user_login\":\"quotrok\",\"user_name\":\"quotrok\"}],\"pagination\":{\"cursor\":\"cursor_mod_1\"}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/moderators", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        ResponseData<Moderator>? response = await Moderator.GetModerators(session, "198704263", ["424596340"], first: 50, after: "cursor_start").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor_mod_1");

        Moderator mod = response.Data[0];
        _ = mod.UserId.Should().Be("424596340");
        _ = mod.UserLogin.Should().Be("quotrok");
        _ = mod.UserName.Should().Be("quotrok");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/moderators");
        _ = request.QueryString.Should().Contain("broadcaster_id=198704263");
        _ = request.QueryString.Should().Contain("user_id=424596340");
        _ = request.QueryString.Should().Contain("first=50");
        _ = request.QueryString.Should().Contain("after=cursor_start");
    }

    #endregion GetModerators

    #region AddChannelModerator

    [Fact]
    [Trait("Member", "AddChannelModerator")]
    public async Task AddChannelModerator_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Moderator.AddChannelModerator(null!, "11111", "44444").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "AddChannelModerator")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddChannelModerator_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageModerators);
        Func<Task> action = async () => await Moderator.AddChannelModerator(session, invalidId!, "44444").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "AddChannelModerator")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddChannelModerator_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageModerators);
        Func<Task> action = async () => await Moderator.AddChannelModerator(session, "11111", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "AddChannelModerator")]
    public async Task AddChannelModerator_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Moderator.AddChannelModerator(session, "11111", "44444").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AddChannelModerator")]
    public async Task AddChannelModerator_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Moderator.AddChannelModerator(session, "11111", "44444").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.AddChannelModerator"/> throws <see cref="InvalidOperationException"/> when the token has an empty OAuth string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "AddChannelModerator")]
    public async Task AddChannelModerator_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ChannelManageModerators]
            }
        };

        Func<Task> action = async () => await Moderator.AddChannelModerator(session, "11111", "44444").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AddChannelModerator")]
    public async Task AddChannelModerator_WhenValidRequest_PostsAndReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/moderation/moderators", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageModerators);
        JsonApiResponse? response = await Moderator.AddChannelModerator(session, "11111", "44444").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/moderators");
        _ = request.QueryString.Should().Contain("broadcaster_id=11111");
        _ = request.QueryString.Should().Contain("user_id=44444");
    }

    #endregion AddChannelModerator

    #region RemoveChannelModerator

    [Fact]
    [Trait("Member", "RemoveChannelModerator")]
    public async Task RemoveChannelModerator_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Moderator.RemoveChannelModerator(null!, "11111", "44444").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "RemoveChannelModerator")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveChannelModerator_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageModerators);
        Func<Task> action = async () => await Moderator.RemoveChannelModerator(session, invalidId!, "44444").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "RemoveChannelModerator")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveChannelModerator_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageModerators);
        Func<Task> action = async () => await Moderator.RemoveChannelModerator(session, "11111", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "RemoveChannelModerator")]
    public async Task RemoveChannelModerator_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Moderator.RemoveChannelModerator(session, "11111", "44444").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "RemoveChannelModerator")]
    public async Task RemoveChannelModerator_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Moderator.RemoveChannelModerator(session, "11111", "44444").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="Moderator.RemoveChannelModerator"/> throws <see cref="InvalidOperationException"/> when the token has an empty OAuth string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "RemoveChannelModerator")]
    public async Task RemoveChannelModerator_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ChannelManageModerators]
            }
        };

        Func<Task> action = async () => await Moderator.RemoveChannelModerator(session, "11111", "44444").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "RemoveChannelModerator")]
    public async Task RemoveChannelModerator_WhenValidRequest_DeletesAndReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/moderators", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageModerators);
        JsonApiResponse? response = await Moderator.RemoveChannelModerator(session, "11111", "44444").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/moderation/moderators");
        _ = request.QueryString.Should().Contain("broadcaster_id=11111");
        _ = request.QueryString.Should().Contain("user_id=44444");
    }

    #endregion RemoveChannelModerator
}

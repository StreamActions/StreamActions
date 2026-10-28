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

using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="BannedUser"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class BannedUserTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BannedUserTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public BannedUserTests(TwitchKestrelServerFixture fixture)
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

    #region GetBannedUsers

    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await BannedUser.GetBannedUsers(null!, "1234").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetBannedUsers")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBannedUsers_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        Func<Task> action = async () => await BannedUser.GetBannedUsers(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenUserIdsExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        List<string> userIds = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];
        Func<Task> action = async () => await BannedUser.GetBannedUsers(session, "1234", userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userIds");
    }

    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenBothAfterAndBeforeSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        Func<Task> action = async () => await BannedUser.GetBannedUsers(session, "1234", after: "cursorA", before: "cursorB").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await BannedUser.GetBannedUsers(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> throws <see cref="InvalidOperationException"/> when the token has an empty OAuth string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModerationRead]
            }
        };

        Func<Task> action = async () => await BannedUser.GetBannedUsers(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> succeeds with an App token.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenAppToken_ReturnsBannedUsers()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModerationRead);
        ResponseData<BannedUser>? response = await BannedUser.GetBannedUsers(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> includes multiple user IDs in query parameters.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenMultipleUserIds_IncludesAllInQueryString()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await BannedUser.GetBannedUsers(session, "1234", ["111", "222"]).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("user_id=111");
        _ = request.QueryString.Should().Contain("user_id=222");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> includes the before cursor in query parameters.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenBeforeCursorProvided_IncludesBeforeInQueryString()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await BannedUser.GetBannedUsers(session, "1234", before: "cursor_before").ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("before=cursor_before");
        _ = request.QueryString.Should().NotContain("after");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> uses default parameters and omits optional parameters from the query string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenDefaultParameters_OmitsOptionalQueryParameters()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await BannedUser.GetBannedUsers(session, "1234").ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().NotContain("user_id");
        _ = request.QueryString.Should().NotContain("after");
        _ = request.QueryString.Should().NotContain("before");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> clamps the first parameter between 1 and 100.
    /// </para>
    /// </summary>
    /// <param name="first">The input value for first.</param>
    /// <param name="expectedFirst">The expected clamped first value in query string.</param>
    [Theory]
    [Trait("Member", "GetBannedUsers")]
    [InlineData(0, "1")]
    [InlineData(101, "100")]
    public async Task GetBannedUsers_WhenFirstOutOfRange_ClampsFirst(int first, string expectedFirst)
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        _ = await BannedUser.GetBannedUsers(session, "1234", first: first).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain($"first={expectedFirst}");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> succeeds with alternative scopes.
    /// </para>
    /// </summary>
    /// <param name="scopeName">The name of the alternative scope.</param>
    [Theory]
    [Trait("Member", "GetBannedUsers")]
    [InlineData("moderator:manage:banned_users")]
    [InlineData("moderator:read:banned_users")]
    public async Task GetBannedUsers_WhenAlternativeScopes_ReturnsBannedUsers(string scopeName)
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<BannedUser>? response = await BannedUser.GetBannedUsers(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="BannedUser.GetBannedUsers"/> deserializes permanent bans with a null ExpiresAt.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenPermanentBan_DeserializesNullExpiresAt()
    {
        const string json = "{\"data\":[{\"user_id\":\"423374343\",\"user_login\":\"glowillig\",\"user_name\":\"glowillig\",\"expires_at\":null,\"created_at\":\"2022-03-15T01:30:28Z\",\"reason\":\"Permanent ban.\",\"moderator_id\":\"141981764\",\"moderator_login\":\"twitchdev\",\"moderator_name\":\"TwitchDev\"}],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        ResponseData<BannedUser>? response = await BannedUser.GetBannedUsers(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Data[0].ExpiresAt.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetBannedUsers")]
    public async Task GetBannedUsers_WhenValidRequest_GetsBannedUsersList()
    {
        const string json = "{\"data\":[{\"user_id\":\"423374343\",\"user_login\":\"glowillig\",\"user_name\":\"glowillig\",\"expires_at\":\"2022-03-15T02:00:28Z\",\"created_at\":\"2022-03-15T01:30:28Z\",\"reason\":\"Does not like pineapple on pizza.\",\"moderator_id\":\"141981764\",\"moderator_login\":\"twitchdev\",\"moderator_name\":\"TwitchDev\"}],\"pagination\":{\"cursor\":\"test_cursor_123\"}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/banned", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        ResponseData<BannedUser>? response = await BannedUser.GetBannedUsers(session, "198704263", ["423374343"], first: 50, after: "prev_cursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("test_cursor_123");

        BannedUser user = response.Data[0];
        _ = user.UserId.Should().Be("423374343");
        _ = user.UserLogin.Should().Be("glowillig");
        _ = user.UserName.Should().Be("glowillig");
        _ = user.ExpiresAt.Should().Be(DateTime.Parse("2022-03-15T02:00:28Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = user.CreatedAt.Should().Be(DateTime.Parse("2022-03-15T01:30:28Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = user.Reason.Should().Be("Does not like pineapple on pizza.");
        _ = user.ModeratorId.Should().Be("141981764");
        _ = user.ModeratorLogin.Should().Be("twitchdev");
        _ = user.ModeratorName.Should().Be("TwitchDev");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/banned");
        _ = request.QueryString.Should().Contain("broadcaster_id=198704263");
        _ = request.QueryString.Should().Contain("user_id=423374343");
        _ = request.QueryString.Should().Contain("first=50");
        _ = request.QueryString.Should().Contain("after=prev_cursor");
    }

    #endregion GetBannedUsers
}

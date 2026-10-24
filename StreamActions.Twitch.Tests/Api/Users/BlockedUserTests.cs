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
using StreamActions.Twitch.Api.Users;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Users;

/// <summary>
/// Provides unit tests for the <see cref="BlockedUser"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class BlockedUserTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockedUserTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public BlockedUserTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetBlockedUsers

    /// <summary>
    /// Tests that <see cref="BlockedUser.GetBlockedUsers"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBlockedUsers")]
    public async Task GetBlockedUsers_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await BlockedUser.GetBlockedUsers(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.GetBlockedUsers"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetBlockedUsers")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBlockedUsers_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBlockedUsers);
        Func<Task> action = async () => await BlockedUser.GetBlockedUsers(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.GetBlockedUsers"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBlockedUsers")]
    public async Task GetBlockedUsers_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await BlockedUser.GetBlockedUsers(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.GetBlockedUsers"/> throws <see cref="TwitchScopeMissingException"/> when UserReadBlockedUsers scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBlockedUsers")]
    public async Task GetBlockedUsers_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await BlockedUser.GetBlockedUsers(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.GetBlockedUsers"/> returns blocked users on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBlockedUsers")]
    public async Task GetBlockedUsers_WhenRequestSucceeds_ReturnsBlockedUsers()
    {
        this._fixture.RegisterEndpoint("/helix/users/blocks", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""user_id"": ""135093069"",
      ""user_login"": ""smittysmithers"",
      ""display_name"": ""SmittySmithers""
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursorBlock""
  }
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadBlockedUsers);
        ResponseData<BlockedUser>? response = await BlockedUser.GetBlockedUsers(session, "141981764", 20, "afterCursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        BlockedUser blocked = response.Data[0];
        _ = blocked.UserId.Should().Be("135093069");
        _ = blocked.UserLogin.Should().Be("smittysmithers");
        _ = blocked.DisplayName.Should().Be("SmittySmithers");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/users/blocks");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=afterCursor");
    }

    #endregion GetBlockedUsers

    #region BlockUser

    /// <summary>
    /// Tests that <see cref="BlockedUser.BlockUser"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "BlockUser")]
    public async Task BlockUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await BlockedUser.BlockUser(null!, "135093069").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.BlockUser"/> throws <see cref="ArgumentNullException"/> when targetUserId is null or whitespace.
    /// </summary>
    /// <param name="invalidTargetUserId">The invalid target user ID.</param>
    [Theory]
    [Trait("Member", "BlockUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlockUser_WhenTargetUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTargetUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageBlockedUsers);
        Func<Task> action = async () => await BlockedUser.BlockUser(session, invalidTargetUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("targetUserId");
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.BlockUser"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "BlockUser")]
    public async Task BlockUser_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await BlockedUser.BlockUser(session, "135093069").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.BlockUser"/> throws <see cref="TwitchScopeMissingException"/> when UserManageBlockedUsers scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "BlockUser")]
    public async Task BlockUser_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await BlockedUser.BlockUser(session, "135093069").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.BlockUser"/> blocks user on a successful 204 No Content response.
    /// </summary>
    [Fact]
    [Trait("Member", "BlockUser")]
    public async Task BlockUser_WhenRequestSucceeds_ReturnsNoContent()
    {
        this._fixture.RegisterEndpoint("/helix/users/blocks", HttpStatusCode.NoContent, string.Empty);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageBlockedUsers);
        JsonApiResponse? response = await BlockedUser.BlockUser(
            session,
            "135093069",
            BlockedUser.BlockSourceContext.Chat,
            BlockedUser.BlockReason.Spam).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/users/blocks");
        _ = request.QueryString.Should().Contain("target_user_id=135093069");
        _ = request.QueryString.Should().Contain("source_context=chat");
        _ = request.QueryString.Should().Contain("reason=spam");
    }

    #endregion BlockUser

    #region UnblockUser

    /// <summary>
    /// Tests that <see cref="BlockedUser.UnblockUser"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UnblockUser")]
    public async Task UnblockUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await BlockedUser.UnblockUser(null!, "135093069").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.UnblockUser"/> throws <see cref="ArgumentNullException"/> when targetUserId is null or whitespace.
    /// </summary>
    /// <param name="invalidTargetUserId">The invalid target user ID.</param>
    [Theory]
    [Trait("Member", "UnblockUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnblockUser_WhenTargetUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTargetUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageBlockedUsers);
        Func<Task> action = async () => await BlockedUser.UnblockUser(session, invalidTargetUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("targetUserId");
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.UnblockUser"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "UnblockUser")]
    public async Task UnblockUser_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await BlockedUser.UnblockUser(session, "135093069").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.UnblockUser"/> throws <see cref="TwitchScopeMissingException"/> when UserManageBlockedUsers scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UnblockUser")]
    public async Task UnblockUser_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await BlockedUser.UnblockUser(session, "135093069").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="BlockedUser.UnblockUser"/> unblocks user on a successful 204 No Content response.
    /// </summary>
    [Fact]
    [Trait("Member", "UnblockUser")]
    public async Task UnblockUser_WhenRequestSucceeds_ReturnsNoContent()
    {
        this._fixture.RegisterEndpoint("/helix/users/blocks", HttpStatusCode.NoContent, string.Empty);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageBlockedUsers);
        JsonApiResponse? response = await BlockedUser.UnblockUser(session, "135093069").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/users/blocks");
        _ = request.QueryString.Should().Contain("target_user_id=135093069");
    }

    #endregion UnblockUser
}

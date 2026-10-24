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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="VIP"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class VIPTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="VIPTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public VIPTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetVIPs

    [Fact]
    [Trait("Member", "GetVIPs")]
    public async Task GetVIPs_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await VIP.GetVIPs(null!, "1234").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetVIPs")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetVIPs_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadVips);
        Func<Task> action = async () => await VIP.GetVIPs(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetVIPs")]
    public async Task GetVIPs_WhenUserIdsCountExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadVips);
        List<string> userIds = Enumerable.Range(1, 101).Select(static i => i.ToString(CultureInfo.InvariantCulture)).ToList();
        Func<Task> action = async () => await VIP.GetVIPs(session, "1234", userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Theory]
    [Trait("Member", "GetVIPs")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetVIPs_WhenAfterIsWhitespace_ThrowsArgumentNullException(string whitespaceAfter)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadVips);
        Func<Task> action = async () => await VIP.GetVIPs(session, "1234", after: whitespaceAfter).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("after");
    }

    [Fact]
    [Trait("Member", "GetVIPs")]
    public async Task GetVIPs_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ChannelReadVips]
            }
        };

        Func<Task> action = async () => await VIP.GetVIPs(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetVIPs")]
    public async Task GetVIPs_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await VIP.GetVIPs(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "GetVIPs")]
    [InlineData("channel:read:vips")]
    [InlineData("channel:manage:vips")]
    [InlineData("moderator:read:vips")]
    public async Task GetVIPs_WhenValidUserRequest_ReturnsVIPs(string scopeName)
    {
        const string json = "{\"data\":[{\"user_id\":\"111\",\"user_login\":\"vip1\",\"user_name\":\"VIP1\"},{\"user_id\":\"222\",\"user_login\":\"vip2\",\"user_name\":\"VIP2\"}],\"pagination\":{\"cursor\":\"cursor123\"}}";
        this._fixture.RegisterResponse("GET", "/channels/vips", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<VIP>? response = await VIP.GetVIPs(
            session,
            "1234",
            userId: ["111", "222"],
            first: 50,
            after: "cursor123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(2);

        VIP item = response.Data![0];
        _ = item.UserId.Should().Be("111");
        _ = item.UserLogin.Should().Be("vip1");
        _ = item.UserName.Should().Be("VIP1");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channels/vips");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("user_id=111");
        _ = request.QueryString.Should().Contain("user_id=222");
        _ = request.QueryString.Should().Contain("first=50");
        _ = request.QueryString.Should().Contain("after=cursor123");
    }

    [Fact]
    [Trait("Member", "GetVIPs")]
    public async Task GetVIPs_WhenAppToken_ReturnsVIPs()
    {
        const string json = "{\"data\":[],\"pagination\":{}}";
        this._fixture.RegisterResponse("GET", "/helix/channels/vips", json);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelReadVips);
        ResponseData<VIP>? response = await VIP.GetVIPs(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().BeEmpty();
    }

    #endregion GetVIPs

    #region AddChannelVIP

    [Fact]
    [Trait("Member", "AddChannelVIP")]
    public async Task AddChannelVIP_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await VIP.AddChannelVIP(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "AddChannelVIP")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddChannelVIP_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVips);
        Func<Task> action = async () => await VIP.AddChannelVIP(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "AddChannelVIP")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddChannelVIP_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVips);
        Func<Task> action = async () => await VIP.AddChannelVIP(session, "1234", invalidUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "AddChannelVIP")]
    public async Task AddChannelVIP_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManageVips);
        Func<Task> action = async () => await VIP.AddChannelVIP(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AddChannelVIP")]
    public async Task AddChannelVIP_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadVips);
        Func<Task> action = async () => await VIP.AddChannelVIP(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AddChannelVIP")]
    public async Task AddChannelVIP_WhenValidRequest_PostsAndReturnsNoContent()
    {
        this._fixture.RegisterResponse("POST", "/helix/channels/vips", "", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVips);
        JsonApiResponse? response = await VIP.AddChannelVIP(session, "1234", "5678").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/channels/vips");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("user_id=5678");
    }

    #endregion AddChannelVIP

    #region RemoveChannelVIP

    [Fact]
    [Trait("Member", "RemoveChannelVIP")]
    public async Task RemoveChannelVIP_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await VIP.RemoveChannelVIP(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "RemoveChannelVIP")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveChannelVIP_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVips);
        Func<Task> action = async () => await VIP.RemoveChannelVIP(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "RemoveChannelVIP")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveChannelVIP_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVips);
        Func<Task> action = async () => await VIP.RemoveChannelVIP(session, "1234", invalidUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "RemoveChannelVIP")]
    public async Task RemoveChannelVIP_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManageVips);
        Func<Task> action = async () => await VIP.RemoveChannelVIP(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "RemoveChannelVIP")]
    public async Task RemoveChannelVIP_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadVips);
        Func<Task> action = async () => await VIP.RemoveChannelVIP(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "RemoveChannelVIP")]
    public async Task RemoveChannelVIP_WhenValidRequest_DeletesAndReturnsNoContent()
    {
        this._fixture.RegisterResponse("DELETE", "/helix/channels/vips", "", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageVips);
        JsonApiResponse? response = await VIP.RemoveChannelVIP(session, "1234", "5678").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/channels/vips");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("user_id=5678");
    }

    #endregion RemoveChannelVIP
}

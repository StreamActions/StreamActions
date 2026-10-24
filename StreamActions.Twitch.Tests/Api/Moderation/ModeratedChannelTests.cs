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
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="ModeratedChannel"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ModeratedChannelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ModeratedChannelTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ModeratedChannelTests(TwitchKestrelServerFixture fixture)
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

    #region GetModeratedChannels

    [Fact]
    [Trait("Member", "GetModeratedChannels")]
    public async Task GetModeratedChannels_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ModeratedChannel.GetModeratedChannels(null!, "931931").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetModeratedChannels")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetModeratedChannels_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadModeratedChannels);
        Func<Task> action = async () => await ModeratedChannel.GetModeratedChannels(session, invalidUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "GetModeratedChannels")]
    public async Task GetModeratedChannels_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await ModeratedChannel.GetModeratedChannels(session, "931931").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetModeratedChannels")]
    public async Task GetModeratedChannels_WhenValidRequest_ReturnsChannelsList()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"12345\",\"broadcaster_login\":\"grateful_broadcaster\",\"broadcaster_name\":\"Grateful_Broadcaster\"},{\"broadcaster_id\":\"98765\",\"broadcaster_login\":\"bashfulgamer\",\"broadcaster_name\":\"BashfulGamer\"}],\"pagination\":{\"cursor\":\"cursor_abc_123\"}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/channels", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadModeratedChannels);
        ResponseData<ModeratedChannel>? response = await ModeratedChannel.GetModeratedChannels(session, "931931", after: "cursor_prev", first: 20).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(2);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination!.Cursor.Should().Be("cursor_abc_123");

        ModeratedChannel first = response.Data!.ElementAt(0);
        _ = first.BroadcasterId.Should().Be("12345");
        _ = first.BroadcasterLogin.Should().Be("grateful_broadcaster");
        _ = first.BroadcasterName.Should().Be("Grateful_Broadcaster");

        ModeratedChannel second = response.Data!.ElementAt(1);
        _ = second.BroadcasterId.Should().Be("98765");
        _ = second.BroadcasterLogin.Should().Be("bashfulgamer");
        _ = second.BroadcasterName.Should().Be("BashfulGamer");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/channels");
        _ = request.QueryString.Should().Contain("user_id=931931");
        _ = request.QueryString.Should().Contain("after=cursor_prev");
        _ = request.QueryString.Should().Contain("first=20");
    }

    #endregion GetModeratedChannels
}

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
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Channels;

/// <summary>
/// Provides unit and integration tests for <see cref="FollowedChannel"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class FollowedChannelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FollowedChannelTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public FollowedChannelTests(TwitchKestrelServerFixture fixture)
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

    #region GetFollowedChannels

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedChannels")]
    public async Task GetFollowedChannels_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await FollowedChannel.GetFollowedChannels(null!, "123456").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> throws <see cref="ArgumentNullException"/> when userId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid user ID to test.</param>
    [Theory]
    [Trait("Member", "GetFollowedChannels")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFollowedChannels_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadFollows);
        Func<Task> action = async () => await FollowedChannel.GetFollowedChannels(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> throws <see cref="ArgumentNullException"/> when broadcasterId is empty or whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedChannels")]
    public async Task GetFollowedChannels_WhenBroadcasterIdIsWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadFollows);
        Func<Task> action = async () => await FollowedChannel.GetFollowedChannels(session, "123456", broadcasterId: "   ").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> throws <see cref="ArgumentNullException"/> when after is empty or whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedChannels")]
    public async Task GetFollowedChannels_WhenAfterIsWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadFollows);
        Func<Task> action = async () => await FollowedChannel.GetFollowedChannels(session, "123456", after: "   ").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("after");
    }

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedChannels")]
    public async Task GetFollowedChannels_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await FollowedChannel.GetFollowedChannels(session, "123456").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedChannels")]
    public async Task GetFollowedChannels_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await FollowedChannel.GetFollowedChannels(session, "123456").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="FollowedChannel.GetFollowedChannels"/> sends a GET request and returns followed channels list.
    /// </summary>
    [Fact]
    [Trait("Member", "GetFollowedChannels")]
    public async Task GetFollowedChannels_WhenValidParameters_ReturnsFollowedChannelsList()
    {
        DateTime followedAt = new(2022, 5, 24, 22, 22, 8, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/channels/followed", new
        {
            total = 8,
            data = new[]
            {
                new
                {
                    broadcaster_id = "654321",
                    broadcaster_login = "basketweaver101",
                    broadcaster_name = "BasketWeaver101",
                    followed_at = followedAt
                }
            },
            pagination = new { }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadFollows);
        ResponseData<FollowedChannel>? response = await FollowedChannel.GetFollowedChannels(session, "123456", first: 20).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Total.Should().Be(8);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        FollowedChannel channel = response.Data[0];
        _ = channel.BroadcasterId.Should().Be("654321");
        _ = channel.BroadcasterLogin.Should().Be("basketweaver101");
        _ = channel.BroadcasterName.Should().Be("BasketWeaver101");
        _ = channel.FollowedAt.Should().Be(followedAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channels/followed");
        _ = request.QueryString.Should().Contain("user_id=123456");
        _ = request.QueryString.Should().Contain("first=20");
    }

    #endregion GetFollowedChannels
}

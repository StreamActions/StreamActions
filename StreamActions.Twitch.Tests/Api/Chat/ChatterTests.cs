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
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="Chatter"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChatterTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatterTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChatterTests(TwitchKestrelServerFixture fixture)
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

    #region GetChatters

    /// <summary>
    /// Tests that <see cref="Chatter.GetChatters"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChatters")]
    public async Task GetChatters_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Chatter.GetChatters(null!, "12345", "67890").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Chatter.GetChatters"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetChatters")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChatters_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatters);
        Func<Task> action = async () => await Chatter.GetChatters(session, invalidId!, "67890").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Chatter.GetChatters"/> throws <see cref="ArgumentNullException"/> when moderatorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid moderator ID.</param>
    [Theory]
    [Trait("Member", "GetChatters")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChatters_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatters);
        Func<Task> action = async () => await Chatter.GetChatters(session, "12345", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    /// <summary>
    /// Tests that <see cref="Chatter.GetChatters"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChatters")]
    public async Task GetChatters_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await Chatter.GetChatters(session, "12345", "67890").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Chatter.GetChatters"/> retrieves chatters and handles pagination parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChatters")]
    public async Task GetChatters_WhenCalledWithValidParameters_ReturnsChatters()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/chatters", new
        {
            data = new[]
            {
                new
                {
                    user_id = "128393667",
                    user_login = "cooler_user",
                    user_name = "Cooler_User"
                }
            },
            pagination = new
            {
                cursor = "eyJiIjpudWxsLCJhIjp7IkN1cnNvciI6Ik1qQXpNREE9In19"
            },
            total = 8
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatters);
        ResponseData<Chatter>? response = await Chatter.GetChatters(session, "12345", "67890", first: 50, after: "cursor123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Chatter chatter = response.Data[0];
        _ = chatter.UserId.Should().Be("128393667");
        _ = chatter.UserLogin.Should().Be("cooler_user");
        _ = chatter.UserName.Should().Be("Cooler_User");
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("eyJiIjpudWxsLCJhIjp7IkN1cnNvciI6Ik1qQXpNREE9In19");
        _ = response.Total.Should().Be(8);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/chatters");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
        _ = request.Query["first"].ToString().Should().Be("50");
        _ = request.Query["after"].ToString().Should().Be("cursor123");
    }

    #endregion GetChatters
}

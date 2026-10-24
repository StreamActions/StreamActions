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
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="Shoutout"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ShoutoutTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoutoutTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ShoutoutTests(TwitchKestrelServerFixture fixture)
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

    #region SendShoutout

    /// <summary>
    /// Tests that <see cref="Shoutout.SendShoutout"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SendShoutout")]
    public async Task SendShoutout_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Shoutout.SendShoutout(null!, "123", "456", "789").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Shoutout.SendShoutout"/> throws <see cref="ArgumentNullException"/> when fromBroadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "SendShoutout")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendShoutout_WhenFromBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShoutouts);
        Func<Task> action = async () => await Shoutout.SendShoutout(session, invalidId!, "456", "789").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("fromBroadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Shoutout.SendShoutout"/> throws <see cref="ArgumentNullException"/> when toBroadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid target broadcaster ID.</param>
    [Theory]
    [Trait("Member", "SendShoutout")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendShoutout_WhenToBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShoutouts);
        Func<Task> action = async () => await Shoutout.SendShoutout(session, "123", invalidId!, "789").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("toBroadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Shoutout.SendShoutout"/> throws <see cref="ArgumentNullException"/> when moderatorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid moderator ID.</param>
    [Theory]
    [Trait("Member", "SendShoutout")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendShoutout_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShoutouts);
        Func<Task> action = async () => await Shoutout.SendShoutout(session, "123", "456", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    /// <summary>
    /// Tests that <see cref="Shoutout.SendShoutout"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "SendShoutout")]
    public async Task SendShoutout_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await Shoutout.SendShoutout(session, "123", "456", "789").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Shoutout.SendShoutout"/> sends POST request and returns 204 No Content.
    /// </summary>
    [Fact]
    [Trait("Member", "SendShoutout")]
    public async Task SendShoutout_WhenCalledWithValidParameters_SendsPostRequestAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/chat/shoutouts", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageShoutouts);
        JsonApiResponse? response = await Shoutout.SendShoutout(session, "12345", "67890", "11111").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/chat/shoutouts");
        _ = request.Query["from_broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["to_broadcaster_id"].ToString().Should().Be("67890");
        _ = request.Query["moderator_id"].ToString().Should().Be("11111");
    }

    #endregion SendShoutout
}

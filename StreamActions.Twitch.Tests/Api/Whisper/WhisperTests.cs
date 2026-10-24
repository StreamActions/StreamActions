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
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using WhisperApi = StreamActions.Twitch.Api.Whisper.Whisper;

namespace StreamActions.Twitch.Tests.Api.Whisper;

/// <summary>
/// Provides unit tests for the <see cref="WhisperApi"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class WhisperTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public WhisperTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the test server fixture.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region SendWhisper

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SendWhisper")]
    public async Task SendWhisper_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        WhisperApi message = new() { Message = "hello" };
        Func<Task> action = async () => await WhisperApi.SendWhisper(null!, "141981764", "527115020", message).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "SendWhisper")]
    public async Task SendWhisper_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        WhisperApi message = new() { Message = "hello" };

        Func<Task> action = async () => await WhisperApi.SendWhisper(session, "141981764", "527115020", message).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> throws <see cref="TwitchScopeMissingException"/> when UserManageWhispers scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "SendWhisper")]
    public async Task SendWhisper_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        WhisperApi message = new() { Message = "hello" };

        Func<Task> action = async () => await WhisperApi.SendWhisper(session, "141981764", "527115020", message).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> throws <see cref="ArgumentNullException"/> when fromUserId is null or whitespace.
    /// </summary>
    /// <param name="invalidFromUserId">The invalid sender user ID.</param>
    [Theory]
    [Trait("Member", "SendWhisper")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendWhisper_WhenFromUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidFromUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageWhispers);
        WhisperApi message = new() { Message = "hello" };

        Func<Task> action = async () => await WhisperApi.SendWhisper(session, invalidFromUserId!, "527115020", message).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("fromUserId");
    }

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> throws <see cref="ArgumentNullException"/> when toUserId is null or whitespace.
    /// </summary>
    /// <param name="invalidToUserId">The invalid recipient user ID.</param>
    [Theory]
    [Trait("Member", "SendWhisper")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendWhisper_WhenToUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidToUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageWhispers);
        WhisperApi message = new() { Message = "hello" };

        Func<Task> action = async () => await WhisperApi.SendWhisper(session, "141981764", invalidToUserId!, message).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("toUserId");
    }

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> throws <see cref="ArgumentNullException"/> when message is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "SendWhisper")]
    public async Task SendWhisper_WhenMessageIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageWhispers);

        Func<Task> actionNull = async () => await WhisperApi.SendWhisper(session, "141981764", "527115020", null!).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("message");

        Func<Task> actionEmpty = async () => await WhisperApi.SendWhisper(session, "141981764", "527115020", new() { Message = "" }).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("message");

        Func<Task> actionWhitespace = async () => await WhisperApi.SendWhisper(session, "141981764", "527115020", new() { Message = "   " }).ConfigureAwait(true);
        _ = (await actionWhitespace.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("message");
    }

    /// <summary>
    /// Tests that <see cref="WhisperApi.SendWhisper"/> sends whisper on a successful 204 No Content response.
    /// </summary>
    [Fact]
    [Trait("Member", "SendWhisper")]
    public async Task SendWhisper_WhenRequestSucceeds_ReturnsNoContent()
    {
        this._fixture.RegisterEndpoint("/helix/whispers", HttpStatusCode.NoContent, string.Empty);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageWhispers);
        WhisperApi message = new() { Message = "hello whisper" };

        JsonApiResponse? response = await WhisperApi.SendWhisper(session, "141981764", "527115020", message).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/whispers");
        _ = request.QueryString.Should().Contain("from_user_id=141981764");
        _ = request.QueryString.Should().Contain("to_user_id=527115020");
        _ = request.Body.Should().Contain("\"message\":\"hello whisper\"");
    }

    #endregion SendWhisper
}

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
using Microsoft.AspNetCore.Http;
using StreamActions.Common.Exceptions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Extensions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="ExtensionChatMessage"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionChatMessageTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionChatMessageTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionChatMessageTests(TwitchKestrelServerFixture fixture)
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

    #region SendExtensionChatMessage

    [Fact]
    [Trait("Member", "SendExtensionChatMessage")]
    public async Task SendExtensionChatMessage_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        SendExtensionChatMessageParameters parameters = new()
        {
            Text = "Hello",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.9"
        };

        Func<Task> action = async () => await ExtensionChatMessage.SendExtensionChatMessage(null!, "237757755", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "SendExtensionChatMessage")]
    public async Task SendExtensionChatMessage_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionChatMessage.SendExtensionChatMessage(session, "237757755", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "SendExtensionChatMessage")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendExtensionChatMessage_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        SendExtensionChatMessageParameters parameters = new()
        {
            Text = "Hello",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.9"
        };

        Func<Task> action = async () => await ExtensionChatMessage.SendExtensionChatMessage(session, invalidBroadcasterId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "SendExtensionChatMessage")]
    public async Task SendExtensionChatMessage_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        SendExtensionChatMessageParameters parameters = new()
        {
            Text = "Hello",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.9"
        };

        Func<Task> action = async () => await ExtensionChatMessage.SendExtensionChatMessage(session, "237757755", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "SendExtensionChatMessage")]
    public async Task SendExtensionChatMessage_WhenValidRequest_PostsMessageAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/extensions/chat", StatusCodes.Status204NoContent);

        SendExtensionChatMessageParameters parameters = new()
        {
            Text = "Hello",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.9"
        };

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        JsonApiResponse? response = await ExtensionChatMessage.SendExtensionChatMessage(session, "237757755", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/extensions/chat");
        _ = request.QueryString.Should().Contain("broadcaster_id=237757755");
        _ = request.Body.Should().Contain("\"text\":\"Hello\"");
        _ = request.Body.Should().Contain("\"extension_id\":\"uo6dggojyb8d6soh92zknwmi5ej1q2\"");
    }

    #endregion SendExtensionChatMessage
}

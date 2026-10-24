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
/// Performs unit and integration testing on <see cref="ExtensionPubSubMessage"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionPubSubMessageTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionPubSubMessageTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionPubSubMessageTests(TwitchKestrelServerFixture fixture)
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

    #region SendExtensionPubSubMessage

    [Fact]
    [Trait("Member", "SendExtensionPubSubMessage")]
    public async Task SendExtensionPubSubMessage_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        SendExtensionPubSubMessageParameters parameters = new()
        {
            Message = "hello world!",
            BroadcasterId = "141981764",
            Target = ["broadcast"]
        };

        Func<Task> action = async () => await ExtensionPubSubMessage.SendExtensionPubSubMessage(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "SendExtensionPubSubMessage")]
    public async Task SendExtensionPubSubMessage_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionPubSubMessage.SendExtensionPubSubMessage(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "SendExtensionPubSubMessage")]
    public async Task SendExtensionPubSubMessage_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        SendExtensionPubSubMessageParameters parameters = new()
        {
            Message = "hello world!",
            BroadcasterId = "141981764",
            Target = ["broadcast"]
        };

        Func<Task> action = async () => await ExtensionPubSubMessage.SendExtensionPubSubMessage(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "SendExtensionPubSubMessage")]
    public async Task SendExtensionPubSubMessage_WhenValidRequest_SendsPostAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/extensions/pubsub", StatusCodes.Status204NoContent);

        SendExtensionPubSubMessageParameters parameters = new()
        {
            Message = "hello world!",
            BroadcasterId = "141981764",
            Target = ["broadcast"]
        };

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        JsonApiResponse? response = await ExtensionPubSubMessage.SendExtensionPubSubMessage(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/extensions/pubsub");
        _ = request.Body.Should().Contain("\"message\":\"hello world!\"");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"141981764\"");
    }

    #endregion SendExtensionPubSubMessage
}

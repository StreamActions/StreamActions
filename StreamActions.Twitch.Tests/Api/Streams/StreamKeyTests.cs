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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Streams;

/// <summary>
/// Provides unit tests for the <see cref="StreamKey"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class StreamKeyTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamKeyTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public StreamKeyTests(TwitchKestrelServerFixture fixture)
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

    #region GetStreamKey

    /// <summary>
    /// Tests that <see cref="StreamKey.GetStreamKey"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamKey")]
    public async Task GetStreamKey_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await StreamKey.GetStreamKey(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="StreamKey.GetStreamKey"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetStreamKey")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStreamKey_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadStreamKey);
        Func<Task> action = async () => await StreamKey.GetStreamKey(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="StreamKey.GetStreamKey"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamKey")]
    public async Task GetStreamKey_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await StreamKey.GetStreamKey(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="StreamKey.GetStreamKey"/> throws <see cref="TwitchScopeMissingException"/> when ChannelReadStreamKey scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamKey")]
    public async Task GetStreamKey_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await StreamKey.GetStreamKey(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="StreamKey.GetStreamKey"/> returns the stream key on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetStreamKey")]
    public async Task GetStreamKey_WhenRequestSucceeds_ReturnsStreamKey()
    {
        this._fixture.RegisterEndpoint("/helix/streams/key", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""stream_key"": ""live_44322889_a74da6ae628b49f7d7376d8177f390""
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadStreamKey);
        ResponseData<StreamKey>? response = await StreamKey.GetStreamKey(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        StreamKey streamKey = response.Data[0];
        _ = streamKey.Key.Should().Be("live_44322889_a74da6ae628b49f7d7376d8177f390");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/streams/key");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    #endregion GetStreamKey
}

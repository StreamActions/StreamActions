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
using StreamActions.Twitch.Api.Ads;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Ads;

/// <summary>
/// Provides unit and integration tests for <see cref="Commercial"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class CommercialTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CommercialTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public CommercialTests(TwitchKestrelServerFixture fixture)
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

    #region StartCommercial

    /// <summary>
    /// Tests that <see cref="Commercial.StartCommercial"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "StartCommercial")]
    public async Task StartCommercial_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        StartCommercialParameters parameters = new() { BroadcasterId = "141981764", Length = 60 };
        Func<Task> action = async () => await Commercial.StartCommercial(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Commercial.StartCommercial"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "StartCommercial")]
    public async Task StartCommercial_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelEditCommercial);
        Func<Task> action = async () => await Commercial.StartCommercial(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="Commercial.StartCommercial"/> throws <see cref="ArgumentNullException"/> when BroadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "StartCommercial")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartCommercial_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelEditCommercial);
        StartCommercialParameters parameters = new() { BroadcasterId = invalidId, Length = 60 };
        Func<Task> action = async () => await Commercial.StartCommercial(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("BroadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Commercial.StartCommercial"/> throws <see cref="ArgumentNullException"/> when Length is null.
    /// </summary>
    [Fact]
    [Trait("Member", "StartCommercial")]
    public async Task StartCommercial_WhenLengthIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelEditCommercial);
        StartCommercialParameters parameters = new() { BroadcasterId = "141981764", Length = null };
        Func<Task> action = async () => await Commercial.StartCommercial(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Length");
    }

    /// <summary>
    /// Tests that <see cref="Commercial.StartCommercial"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "StartCommercial")]
    public async Task StartCommercial_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        StartCommercialParameters parameters = new() { BroadcasterId = "141981764", Length = 60 };
        Func<Task> action = async () => await Commercial.StartCommercial(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Commercial.StartCommercial"/> sends a POST request with body and returns commercial response with calculated properties.
    /// </summary>
    [Fact]
    [Trait("Member", "StartCommercial")]
    public async Task StartCommercial_WhenValidParameters_PostsCommercialAndReturnsCommercialWithCalculatedProperties()
    {
        this._fixture.RegisterJsonResponse("POST", "/helix/channels/commercial", new
        {
            data = new[]
            {
                new
                {
                    length = 60,
                    message = "",
                    retry_after = 480
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelEditCommercial);
        StartCommercialParameters parameters = new() { BroadcasterId = "141981764", Length = 60 };
        ResponseData<Commercial>? response = await Commercial.StartCommercial(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Commercial commercial = response.Data[0];
        _ = commercial.Length.Should().Be(60);
        _ = commercial.Message.Should().Be(string.Empty);
        _ = commercial.RetryAfterSeconds.Should().Be(480);
        _ = commercial.RetryAfter.Should().Be(TimeSpan.FromSeconds(480));
        _ = commercial.RetryAfterDateTime.Should().NotBeNull();
        _ = commercial.RetryAfterDateTime.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(480), TimeSpan.FromSeconds(5));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/channels/commercial");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"141981764\"");
        _ = request.Body.Should().Contain("\"length\":60");
    }

    #endregion StartCommercial
}

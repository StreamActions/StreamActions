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
/// Performs unit and integration testing on <see cref="ExtensionConfigurationSegment"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionConfigurationSegmentTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionConfigurationSegmentTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionConfigurationSegmentTests(TwitchKestrelServerFixture fixture)
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

    #region GetExtensionConfigurationSegment

    [Fact]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    public async Task GetExtensionConfigurationSegment_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(null!, "ext123", ["global"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetExtensionConfigurationSegment_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, invalidExtensionId!, ["global"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    public async Task GetExtensionConfigurationSegment_WhenSegmentIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, "ext123", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("segment");
    }

    [Fact]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    public async Task GetExtensionConfigurationSegment_WhenSegmentIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, "ext123", []).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("segment");
    }

    [Theory]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    [InlineData("broadcaster")]
    [InlineData("developer")]
    public async Task GetExtensionConfigurationSegment_WhenBroadcasterOrDeveloperWithoutBroadcasterId_ThrowsArgumentNullException(string segmentType)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, "ext123", [segmentType], null).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    public async Task GetExtensionConfigurationSegment_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, "ext123", ["global"]).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    public async Task GetExtensionConfigurationSegment_WhenValidGlobalRequest_ReturnsConfigurationData()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "segment": "global",
              "content": "hello config!",
              "version": "0.0.1"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions/configurations", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        ResponseData<ExtensionConfigurationSegment>? response = await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, "uo6dggojyb8d6soh92zknwmi5ej1q2", ["global"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        ExtensionConfigurationSegment segment = response.Data[0];
        _ = segment.Segment.Should().Be("global");
        _ = segment.Content.Should().Be("hello config!");
        _ = segment.Version.Should().Be("0.0.1");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/extensions/configurations");
        _ = request.QueryString.Should().Contain("extension_id=uo6dggojyb8d6soh92zknwmi5ej1q2");
        _ = request.QueryString.Should().Contain("segment=global");
    }

    [Fact]
    [Trait("Member", "GetExtensionConfigurationSegment")]
    public async Task GetExtensionConfigurationSegment_WhenValidBroadcasterRequest_IncludesBroadcasterId()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "segment": "broadcaster",
              "broadcaster_id": "141981764",
              "content": "custom broadcaster config",
              "version": "0.0.2"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions/configurations", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        ResponseData<ExtensionConfigurationSegment>? response = await ExtensionConfigurationSegment.GetExtensionConfigurationSegment(session, "uo6dggojyb8d6soh92zknwmi5ej1q2", ["broadcaster"], "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data![0].BroadcasterId.Should().Be("141981764");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    #endregion GetExtensionConfigurationSegment

    #region SetExtensionConfigurationSegment

    [Fact]
    [Trait("Member", "SetExtensionConfigurationSegment")]
    public async Task SetExtensionConfigurationSegment_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        SetExtensionConfigurationSegmentParameters parameters = new()
        {
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            Segment = "global",
            Version = "0.0.1",
            Content = "hello config!"
        };

        Func<Task> action = async () => await ExtensionConfigurationSegment.SetExtensionConfigurationSegment(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "SetExtensionConfigurationSegment")]
    public async Task SetExtensionConfigurationSegment_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionConfigurationSegment.SetExtensionConfigurationSegment(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "SetExtensionConfigurationSegment")]
    public async Task SetExtensionConfigurationSegment_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        SetExtensionConfigurationSegmentParameters parameters = new()
        {
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            Segment = "global",
            Version = "0.0.1",
            Content = "hello config!"
        };

        Func<Task> action = async () => await ExtensionConfigurationSegment.SetExtensionConfigurationSegment(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "SetExtensionConfigurationSegment")]
    public async Task SetExtensionConfigurationSegment_WhenValidRequest_SendsPutAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PUT", "/helix/extensions/configurations", StatusCodes.Status204NoContent);

        SetExtensionConfigurationSegmentParameters parameters = new()
        {
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            Segment = "global",
            Version = "0.0.1",
            Content = "hello config!"
        };

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        JsonApiResponse? response = await ExtensionConfigurationSegment.SetExtensionConfigurationSegment(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/extensions/configurations");
        _ = request.Body.Should().Contain("\"segment\":\"global\"");
        _ = request.Body.Should().Contain("\"content\":\"hello config!\"");
    }

    #endregion SetExtensionConfigurationSegment
}

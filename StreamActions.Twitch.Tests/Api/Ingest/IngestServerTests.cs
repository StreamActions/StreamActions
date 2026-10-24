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
using StreamActions.Twitch.Api.Ingest;
using StreamActions.Twitch.Api.Ingest.Objects;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Ingest;

/// <summary>
/// Performs unit and integration testing on <see cref="IngestServer"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class IngestServerTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestServerTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public IngestServerTests(TwitchKestrelServerFixture fixture)
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

    #region GetIngestServers

    [Fact]
    [Trait("Member", "GetIngestServers")]
    public async Task GetIngestServers_WhenValidRequest_SendsGetAndReturnsIngestServers()
    {
        const string jsonResponse = """
        {
          "ingests": [
            {
              "_id": 0,
              "availability": 1.0,
              "default": false,
              "name": "US East: Atlanta, GA",
              "url_template": "rtmp://atl.contribute.video.net/app/{stream_key}",
              "priority": 0
            },
            {
              "_id": 1,
              "availability": 1.0,
              "default": false,
              "name": "US East: Ashburn, VA (5)",
              "url_template": "rtmp://iad05.contribute.video.net/app/{stream_key}",
              "priority": 1
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/ingests", jsonResponse);

        IngestResponse? response = await IngestServer.GetIngestServers($"{this._fixture.BaseUrl}/ingests").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Ingests.Should().NotBeNull();
        _ = response.Ingests.Count.Should().Be(2);

        IngestServer server = response.Ingests[0];
        _ = server.Id.Should().Be(0);
        _ = server.Availability.Should().Be(1.0f);
        _ = server.IsDefault.Should().BeFalse();
        _ = server.Name.Should().Be("US East: Atlanta, GA");
        _ = server.UrlTemplate.Should().Be("rtmp://atl.contribute.video.net/app/{stream_key}");
        _ = server.Priority.Should().Be(0);

        string? resolvedUrl = server.UrlTemplate?.Replace("{stream_key}", "live_12345_abcdef", StringComparison.Ordinal);
        _ = resolvedUrl.Should().Be("rtmp://atl.contribute.video.net/app/live_12345_abcdef");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/ingests");
    }

    [Fact]
    [Trait("Member", "GetIngestServers")]
    public async Task GetIngestServers_WhenEmptyList_ReturnsEmptyIngests()
    {
        const string jsonResponse = """
        {
          "ingests": []
        }
        """;

        this._fixture.RegisterResponse("GET", "/ingests", jsonResponse);

        IngestResponse? response = await IngestServer.GetIngestServers($"{this._fixture.BaseUrl}/ingests").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Ingests.Should().NotBeNull();
        _ = response.Ingests.Should().BeEmpty();
    }

    #endregion GetIngestServers
}

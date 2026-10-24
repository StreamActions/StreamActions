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

using StreamActions.Twitch.Api.Extensions;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="ExtensionLiveChannel"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionLiveChannelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionLiveChannelTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionLiveChannelTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test execution.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetExtensionLiveChannels

    [Fact]
    [Trait("Member", "GetExtensionLiveChannels")]
    public async Task GetExtensionLiveChannels_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionLiveChannel.GetExtensionLiveChannels(null!, "ext123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetExtensionLiveChannels")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetExtensionLiveChannels_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionLiveChannel.GetExtensionLiveChannels(session, invalidExtensionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "GetExtensionLiveChannels")]
    public async Task GetExtensionLiveChannels_WhenSessionTokenIsNotUserOrApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionLiveChannel.GetExtensionLiveChannels(session, "ext123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetExtensionLiveChannels")]
    public async Task GetExtensionLiveChannels_WhenValidRequest_SendsGetAndReturnsChannels()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "broadcaster_id": "252766116",
              "broadcaster_name": "swoosh_xii",
              "game_name": "Tom Clancy's Rainbow Six Siege",
              "game_id": "460630",
              "title": "[PS4] ITA/ENG UNRANKED CHILLIN' (SUB 1/15) - !instagram !donation !sens !team !youtube"
            },
            {
              "broadcaster_id": "264525686",
              "broadcaster_name": "gaddem_",
              "game_name": "For Honor",
              "game_id": "490382",
              "title": "any Ktzchen ? - 680 Rep + > Kompetitive Kitten"
            }
          ],
          "pagination": {
            "cursor": "cursor123"
          }
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions/live", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ExtensionLiveChannel>? response = await ExtensionLiveChannel.GetExtensionLiveChannels(session, "uo6dggojyb8d6soh92zknwmi5ej1q2", 20, "after_cursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(2);

        ExtensionLiveChannel first = response.Data[0];
        _ = first.BroadcasterId.Should().Be("252766116");
        _ = first.BroadcasterName.Should().Be("swoosh_xii");
        _ = first.GameName.Should().Be("Tom Clancy's Rainbow Six Siege");
        _ = first.GameId.Should().Be("460630");
        _ = first.Title.Should().Contain("ITA/ENG");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/extensions/live");
        _ = request.QueryString.Should().Contain("extension_id=uo6dggojyb8d6soh92zknwmi5ej1q2");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=after_cursor");
    }

    #endregion GetExtensionLiveChannels
}

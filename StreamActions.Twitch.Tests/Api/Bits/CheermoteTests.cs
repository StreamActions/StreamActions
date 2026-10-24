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

using StreamActions.Twitch.Api.Bits;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Bits;

/// <summary>
/// Provides unit and integration tests for <see cref="Cheermote"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class CheermoteTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CheermoteTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public CheermoteTests(TwitchKestrelServerFixture fixture)
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

    #region GetCheermotes

    /// <summary>
    /// Tests that <see cref="Cheermote.GetCheermotes"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCheermotes")]
    public async Task GetCheermotes_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Cheermote.GetCheermotes(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Cheermote.GetCheermotes"/> sends a GET request and returns a list of cheermotes.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCheermotes")]
    public async Task GetCheermotes_WhenValidParameters_ReturnsCheermotesList()
    {
        DateTime lastUpdated = new(2018, 5, 22, 0, 6, 4, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/bits/cheermotes", new
        {
            data = new[]
            {
                new
                {
                    prefix = "Cheer",
                    tiers = new[]
                    {
                        new
                        {
                            min_bits = 1,
                            id = "1",
                            color = "#979797",
                            images = new
                            {
                                dark = new
                                {
                                    animated = new Dictionary<string, string>
                                    {
                                        { "1", "https://example.com/1.gif" }
                                    }
                                }
                            },
                            can_cheer = true,
                            show_in_bits_card = true
                        }
                    },
                    type = "global_first_party",
                    order = 1,
                    last_updated = lastUpdated,
                    is_charitable = false
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Cheermote>? response = await Cheermote.GetCheermotes(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Cheermote cheermote = response.Data[0];
        _ = cheermote.Prefix.Should().Be("Cheer");
        _ = cheermote.Type.Should().Be(Cheermote.CheermoteType.GlobalFirstParty);
        _ = cheermote.Order.Should().Be(1);
        _ = cheermote.LastUpdated.Should().Be(lastUpdated);
        _ = cheermote.IsCharitable.Should().BeFalse();
        _ = cheermote.Tiers.Should().NotBeNull();
        _ = cheermote.Tiers.Count.Should().Be(1);
        _ = cheermote.Tiers[0].Id.Should().Be("1");
        _ = cheermote.Tiers[0].MinBits.Should().Be(1);
        _ = cheermote.Tiers[0].CanCheer.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/cheermotes");
    }

    /// <summary>
    /// Tests that <see cref="Cheermote.GetCheermotes"/> includes the broadcaster_id query parameter when specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCheermotes")]
    public async Task GetCheermotes_WhenBroadcasterIdProvided_IncludesBroadcasterIdInQuery()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/bits/cheermotes", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Cheermote>? response = await Cheermote.GetCheermotes(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/cheermotes");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    #endregion GetCheermotes
}

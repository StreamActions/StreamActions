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
using StreamActions.Twitch.Api.Bits.Objects;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Drawing;

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
                                        { "1", "https://example.com/dark/animated/1.gif" },
                                        { "1.5", "https://example.com/dark/animated/1.5.gif" },
                                        { "2", "https://example.com/dark/animated/2.gif" },
                                        { "3", "https://example.com/dark/animated/3.gif" },
                                        { "4", "https://example.com/dark/animated/4.gif" }
                                    },
                                    @static = new Dictionary<string, string>
                                    {
                                        { "1", "https://example.com/dark/static/1.png" },
                                        { "1.5", "https://example.com/dark/static/1.5.png" },
                                        { "2", "https://example.com/dark/static/2.png" },
                                        { "3", "https://example.com/dark/static/3.png" },
                                        { "4", "https://example.com/dark/static/4.png" }
                                    }
                                },
                                light = new
                                {
                                    animated = new Dictionary<string, string>
                                    {
                                        { "1", "https://example.com/light/animated/1.gif" },
                                        { "1.5", "https://example.com/light/animated/1.5.gif" },
                                        { "2", "https://example.com/light/animated/2.gif" },
                                        { "3", "https://example.com/light/animated/3.gif" },
                                        { "4", "https://example.com/light/animated/4.gif" }
                                    },
                                    @static = new Dictionary<string, string>
                                    {
                                        { "1", "https://example.com/light/static/1.png" },
                                        { "1.5", "https://example.com/light/static/1.5.png" },
                                        { "2", "https://example.com/light/static/2.png" },
                                        { "3", "https://example.com/light/static/3.png" },
                                        { "4", "https://example.com/light/static/4.png" }
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

        CheermoteTier tier = cheermote.Tiers[0];
        _ = tier.Id.Should().Be("1");
        _ = tier.MinBits.Should().Be(1);
        _ = tier.CanCheer.Should().BeTrue();
        _ = tier.ColorString.Should().Be("#979797");
        _ = tier.Color.Should().Be(Color.FromArgb(151, 151, 151));
        _ = tier.ShowInBitsCard.Should().BeTrue();

        _ = tier.Images.Should().NotBeNull();
        _ = tier.Images.Dark.Should().NotBeNull();
        _ = tier.Images.Dark.Animated.Should().NotBeNull();
        _ = tier.Images.Dark.Animated.Uri1X.Should().Be(new Uri("https://example.com/dark/animated/1.gif"));
        _ = tier.Images.Dark.Animated.Uri1P5X.Should().Be(new Uri("https://example.com/dark/animated/1.5.gif"));
        _ = tier.Images.Dark.Animated.Uri2X.Should().Be(new Uri("https://example.com/dark/animated/2.gif"));
        _ = tier.Images.Dark.Animated.Uri3X.Should().Be(new Uri("https://example.com/dark/animated/3.gif"));
        _ = tier.Images.Dark.Animated.Uri4X.Should().Be(new Uri("https://example.com/dark/animated/4.gif"));

        _ = tier.Images.Dark.Static.Should().NotBeNull();
        _ = tier.Images.Dark.Static.Uri1X.Should().Be(new Uri("https://example.com/dark/static/1.png"));
        _ = tier.Images.Dark.Static.Uri1P5X.Should().Be(new Uri("https://example.com/dark/static/1.5.png"));
        _ = tier.Images.Dark.Static.Uri2X.Should().Be(new Uri("https://example.com/dark/static/2.png"));
        _ = tier.Images.Dark.Static.Uri3X.Should().Be(new Uri("https://example.com/dark/static/3.png"));
        _ = tier.Images.Dark.Static.Uri4X.Should().Be(new Uri("https://example.com/dark/static/4.png"));

        _ = tier.Images.Light.Should().NotBeNull();
        _ = tier.Images.Light.Animated.Should().NotBeNull();
        _ = tier.Images.Light.Animated.Uri1X.Should().Be(new Uri("https://example.com/light/animated/1.gif"));
        _ = tier.Images.Light.Animated.Uri1P5X.Should().Be(new Uri("https://example.com/light/animated/1.5.gif"));
        _ = tier.Images.Light.Animated.Uri2X.Should().Be(new Uri("https://example.com/light/animated/2.gif"));
        _ = tier.Images.Light.Animated.Uri3X.Should().Be(new Uri("https://example.com/light/animated/3.gif"));
        _ = tier.Images.Light.Animated.Uri4X.Should().Be(new Uri("https://example.com/light/animated/4.gif"));

        _ = tier.Images.Light.Static.Should().NotBeNull();
        _ = tier.Images.Light.Static.Uri1X.Should().Be(new Uri("https://example.com/light/static/1.png"));
        _ = tier.Images.Light.Static.Uri1P5X.Should().Be(new Uri("https://example.com/light/static/1.5.png"));
        _ = tier.Images.Light.Static.Uri2X.Should().Be(new Uri("https://example.com/light/static/2.png"));
        _ = tier.Images.Light.Static.Uri3X.Should().Be(new Uri("https://example.com/light/static/3.png"));
        _ = tier.Images.Light.Static.Uri4X.Should().Be(new Uri("https://example.com/light/static/4.png"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/cheermotes");
        _ = request.QueryString.Should().NotContain("broadcaster_id");
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

    #region Properties

    /// <summary>
    /// Tests that <see cref="CheermoteTier.Color"/> returns <see langword="null"/> when <see cref="CheermoteTier.ColorString"/> is null or invalid hex.
    /// </summary>
    /// <param name="colorString">The invalid color string to test.</param>
    [Theory]
    [Trait("Member", "Color")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("123456")]
    public void Color_WhenColorStringIsNullOrInvalid_ReturnsNull(string? colorString)
    {
        CheermoteTier tier = new() { ColorString = colorString };
        _ = tier.Color.Should().BeNull();
    }

    #endregion Properties
}


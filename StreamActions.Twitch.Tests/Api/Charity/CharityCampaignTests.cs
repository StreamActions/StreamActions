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

using StreamActions.Twitch.Api.Charity;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Charity;

/// <summary>
/// Provides unit and integration tests for <see cref="CharityCampaign"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class CharityCampaignTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CharityCampaignTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public CharityCampaignTests(TwitchKestrelServerFixture fixture)
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

    #region GetCharityCampaigns

    /// <summary>
    /// Tests that <see cref="CharityCampaign.GetCharityCampaigns"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaigns")]
    public async Task GetCharityCampaigns_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await CharityCampaign.GetCharityCampaigns(null!, "123456").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="CharityCampaign.GetCharityCampaigns"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetCharityCampaigns")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCharityCampaigns_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadCharity);
        Func<Task> action = async () => await CharityCampaign.GetCharityCampaigns(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="CharityCampaign.GetCharityCampaigns"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaigns")]
    public async Task GetCharityCampaigns_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await CharityCampaign.GetCharityCampaigns(session, "123456").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CharityCampaign.GetCharityCampaigns"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaigns")]
    public async Task GetCharityCampaigns_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await CharityCampaign.GetCharityCampaigns(session, "123456").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CharityCampaign.GetCharityCampaigns"/> sends a GET request and returns charity campaign details.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaigns")]
    public async Task GetCharityCampaigns_WhenValidParameters_ReturnsCampaignsListAndAmounts()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/charity/campaigns", new
        {
            data = new[]
            {
                new
                {
                    id = "123-abc-456-def",
                    broadcaster_id = "123456",
                    broadcaster_name = "SunnySideUp",
                    broadcaster_login = "sunnysideup",
                    charity_name = "Example name",
                    charity_description = "Example description",
                    charity_logo = "https://abc.cloudfront.net/100.png",
                    charity_website = "https://www.example.com",
                    current_amount = new
                    {
                        value = 86000L,
                        decimal_places = 2,
                        currency = "USD"
                    },
                    target_amount = new
                    {
                        value = 1500000L,
                        decimal_places = 2,
                        currency = "USD"
                    }
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadCharity);
        ResponseData<CharityCampaign>? response = await CharityCampaign.GetCharityCampaigns(session, "123456").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        CharityCampaign campaign = response.Data[0];
        _ = campaign.Id.Should().Be("123-abc-456-def");
        _ = campaign.BroadcasterId.Should().Be("123456");
        _ = campaign.BroadcasterName.Should().Be("SunnySideUp");
        _ = campaign.BroadcasterLogin.Should().Be("sunnysideup");
        _ = campaign.CharityName.Should().Be("Example name");
        _ = campaign.CharityDescription.Should().Be("Example description");
        _ = campaign.CharityLogo.Should().Be(new Uri("https://abc.cloudfront.net/100.png"));
        _ = campaign.CharityWebsite.Should().Be(new Uri("https://www.example.com"));

        _ = campaign.CurrentAmount.Should().NotBeNull();
        _ = campaign.CurrentAmount.Value.Should().Be(86000L);
        _ = campaign.CurrentAmount.DecimalPlaces.Should().Be(2);
        _ = campaign.CurrentAmount.Currency.Should().Be("USD");

        _ = campaign.TargetAmount.Should().NotBeNull();
        _ = campaign.TargetAmount.Value.Should().Be(1500000L);
        _ = campaign.TargetAmount.DecimalPlaces.Should().Be(2);
        _ = campaign.TargetAmount.Currency.Should().Be("USD");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/charity/campaigns");
        _ = request.QueryString.Should().Contain("broadcaster_id=123456");
    }

    #endregion GetCharityCampaigns
}

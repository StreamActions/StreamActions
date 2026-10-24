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
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Charity;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Charity;

/// <summary>
/// Provides unit and integration tests for <see cref="CharityDonation"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class CharityDonationTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CharityDonationTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public CharityDonationTests(TwitchKestrelServerFixture fixture)
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

    #region GetCharityCampaignDonations

    /// <summary>
    /// Tests that <see cref="CharityDonation.GetCharityCampaignDonations"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaignDonations")]
    public async Task GetCharityCampaignDonations_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await CharityDonation.GetCharityCampaignDonations(null!, "123456").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="CharityDonation.GetCharityCampaignDonations"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetCharityCampaignDonations")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCharityCampaignDonations_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadCharity);
        Func<Task> action = async () => await CharityDonation.GetCharityCampaignDonations(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="CharityDonation.GetCharityCampaignDonations"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaignDonations")]
    public async Task GetCharityCampaignDonations_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await CharityDonation.GetCharityCampaignDonations(session, "123456").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CharityDonation.GetCharityCampaignDonations"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaignDonations")]
    public async Task GetCharityCampaignDonations_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await CharityDonation.GetCharityCampaignDonations(session, "123456").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CharityDonation.GetCharityCampaignDonations"/> sends a GET request and returns donations list with amounts.
    /// </summary>
    [Fact]
    [Trait("Member", "GetCharityCampaignDonations")]
    public async Task GetCharityCampaignDonations_WhenValidParameters_ReturnsDonationsListAndAmounts()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/charity/donations", new
        {
            data = new[]
            {
                new
                {
                    id = "a1b2c3",
                    campaign_id = "123-abc-456-def",
                    user_id = "5678",
                    user_login = "cool_user",
                    user_name = "Cool_User",
                    amount = new
                    {
                        value = 500L,
                        decimal_places = 2,
                        currency = "USD"
                    }
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadCharity);
        ResponseData<CharityDonation>? response = await CharityDonation.GetCharityCampaignDonations(session, "123456", first: 20).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        CharityDonation donation = response.Data[0];
        _ = donation.Id.Should().Be("a1b2c3");
        _ = donation.CampaignId.Should().Be("123-abc-456-def");
        _ = donation.UserId.Should().Be("5678");
        _ = donation.UserLogin.Should().Be("cool_user");
        _ = donation.UserName.Should().Be("Cool_User");
        _ = donation.Amount.Should().NotBeNull();
        _ = donation.Amount!.Value.Should().Be(500L);
        _ = donation.Amount.DecimalPlaces.Should().Be(2);
        _ = donation.Amount.Currency.Should().Be("USD");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/charity/donations");
        _ = request.QueryString.Should().Contain("broadcaster_id=123456");
        _ = request.QueryString.Should().Contain("first=20");
    }

    #endregion GetCharityCampaignDonations
}

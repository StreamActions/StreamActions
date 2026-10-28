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

using StreamActions.Twitch.Api.Entitlements;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Entitlements;

/// <summary>
/// Provides unit tests for the <see cref="DropEntitlement"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class DropEntitlementTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// Sample entitlement IDs array for drop entitlement update response.
    /// </summary>
    private static readonly string[] _sampleEntitlementIds = ["entitlement123"];

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DropEntitlementTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public DropEntitlementTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetDropsEntitlements

    /// <summary>
    /// Tests that <see cref="DropEntitlement.GetDropsEntitlements"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetDropsEntitlements")]
    public async Task GetDropsEntitlements_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await DropEntitlement.GetDropsEntitlements(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.GetDropsEntitlements"/> throws <see cref="ArgumentOutOfRangeException"/> when ids exceeds 100 elements.
    /// </summary>
    [Fact]
    [Trait("Member", "GetDropsEntitlements")]
    public async Task GetDropsEntitlements_WhenIdsCountExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> ids = [.. Enumerable.Range(1, 101).Select(i => $"id{i}")];

        Func<Task> action = async () => await DropEntitlement.GetDropsEntitlements(session, ids: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("ids");
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.GetDropsEntitlements"/> returns drop entitlements when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetDropsEntitlements")]
    public async Task GetDropsEntitlements_WhenCalledWithValidParameters_ReturnsDropEntitlements()
    {
        DateTime grantTime = new(2023, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        DateTime updateTime = new(2023, 1, 15, 12, 30, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/entitlements/drops", new
        {
            data = new[]
            {
                new
                {
                    id = "entitlement123",
                    benefit_id = "benefit456",
                    timestamp = grantTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    user_id = "user789",
                    game_id = "game321",
                    fulfillment_status = "CLAIMED",
                    last_updated = updateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                }
            },
            pagination = new
            {
                cursor = "cursor_next"
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<DropEntitlement>? response = await DropEntitlement.GetDropsEntitlements(
            session,
            ids: ["entitlement123"],
            userId: "user789",
            gameId: "game321",
            fulfillmentStatus: DropEntitlement.FulfillmentStatuses.Claimed,
            first: 50,
            after: "cursor_prev").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        DropEntitlement entitlement = response.Data[0];
        _ = entitlement.Id.Should().Be("entitlement123");
        _ = entitlement.BenefitId.Should().Be("benefit456");
        _ = entitlement.Timestamp.Should().Be(grantTime);
        _ = entitlement.UserId.Should().Be("user789");
        _ = entitlement.GameId.Should().Be("game321");
        _ = entitlement.FulfillmentStatus.Should().Be(DropEntitlement.FulfillmentStatuses.Claimed);
        _ = entitlement.LastUpdated.Should().Be(updateTime);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor_next");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/entitlements/drops");
        _ = request.Query["id"].ToString().Should().Be("entitlement123");
        _ = request.Query["user_id"].ToString().Should().Be("user789");
        _ = request.Query["game_id"].ToString().Should().Be("game321");
        _ = request.Query["fulfillment_status"].ToString().Should().Be("CLAIMED");
        _ = request.Query["first"].ToString().Should().Be("50");
        _ = request.Query["after"].ToString().Should().Be("cursor_prev");
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.GetDropsEntitlements"/> uses default first value of 20 and omits optional filter query parameters when not specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetDropsEntitlements")]
    public async Task GetDropsEntitlements_WhenCalledWithDefaultParameters_SendsDefaultFirstAndOmitsOptionalFilters()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/entitlements/drops", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<DropEntitlement>? response = await DropEntitlement.GetDropsEntitlements(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Query["first"].ToString().Should().Be("20");
        _ = request.Query.ContainsKey("id").Should().BeFalse();
        _ = request.Query.ContainsKey("user_id").Should().BeFalse();
        _ = request.Query.ContainsKey("game_id").Should().BeFalse();
        _ = request.Query.ContainsKey("fulfillment_status").Should().BeFalse();
        _ = request.Query.ContainsKey("after").Should().BeFalse();
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.GetDropsEntitlements"/> clamps the first parameter between 1 and 1000.
    /// </summary>
    /// <param name="inputFirst">The requested first value.</param>
    /// <param name="expectedFirst">The expected clamped query value.</param>
    [Theory]
    [Trait("Member", "GetDropsEntitlements")]
    [InlineData(-10, "1")]
    [InlineData(0, "1")]
    [InlineData(1001, "1000")]
    [InlineData(5000, "1000")]
    public async Task GetDropsEntitlements_WhenFirstOutOfRange_ClampsFirstParameter(int inputFirst, string expectedFirst)
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/entitlements/drops", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        _ = await DropEntitlement.GetDropsEntitlements(session, first: inputFirst).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Query["first"].ToString().Should().Be(expectedFirst);
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.GetDropsEntitlements"/> succeeds when called with a user session.
    /// </summary>
    [Fact]
    [Trait("Member", "GetDropsEntitlements")]
    public async Task GetDropsEntitlements_WhenCalledWithUserSession_ReturnsDropEntitlements()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/entitlements/drops", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<DropEntitlement>? response = await DropEntitlement.GetDropsEntitlements(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    #endregion GetDropsEntitlements

    #region UpdateDropsEntitlements

    /// <summary>
    /// Tests that <see cref="DropEntitlement.UpdateDropsEntitlements"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateDropsEntitlements")]
    public async Task UpdateDropsEntitlements_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        UpdateDropsEntitlementsParameters parameters = new()
        {
            EntitlementIds = ["entitlement123"],
            FulfillmentStatus = DropEntitlement.FulfillmentStatuses.Fulfilled
        };

        Func<Task> action = async () => await DropEntitlement.UpdateDropsEntitlements(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.UpdateDropsEntitlements"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateDropsEntitlements")]
    public async Task UpdateDropsEntitlements_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await DropEntitlement.UpdateDropsEntitlements(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.UpdateDropsEntitlements"/> updates drop entitlements when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateDropsEntitlements")]
    public async Task UpdateDropsEntitlements_WhenCalledWithValidParameters_ReturnsUpdatedStatus()
    {
        this._fixture.RegisterJsonResponse("PATCH", "/helix/entitlements/drops", new
        {
            data = new[]
            {
                new
                {
                    status = "SUCCESS",
                    ids = _sampleEntitlementIds
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        UpdateDropsEntitlementsParameters parameters = new()
        {
            EntitlementIds = ["entitlement123"],
            FulfillmentStatus = DropEntitlement.FulfillmentStatuses.Fulfilled
        };

        ResponseData<UpdateDropsEntitlementsStatus>? response = await DropEntitlement.UpdateDropsEntitlements(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        UpdateDropsEntitlementsStatus status = response.Data[0];
        _ = status.Status.Should().Be(UpdateDropsEntitlementsStatus.Statuses.Success);
        _ = status.Ids.Should().NotBeNull();
        _ = status.Ids.Count.Should().Be(1);
        _ = status.Ids[0].Should().Be("entitlement123");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/entitlements/drops");
        _ = request.Body.Should().Contain("\"fulfillment_status\":\"FULFILLED\"");
        _ = request.Body.Should().Contain("\"entitlement_ids\":[\"entitlement123\"]");
    }

    /// <summary>
    /// Tests that <see cref="DropEntitlement.UpdateDropsEntitlements"/> properly deserializes all status enum values.
    /// </summary>
    /// <param name="statusString">The API status string in the response.</param>
    /// <param name="expectedStatus">The expected deserialized enum value.</param>
    [Theory]
    [Trait("Member", "UpdateDropsEntitlements")]
    [InlineData("SUCCESS", UpdateDropsEntitlementsStatus.Statuses.Success)]
    [InlineData("INVALID_ID", UpdateDropsEntitlementsStatus.Statuses.InvalidId)]
    [InlineData("NOT_FOUND", UpdateDropsEntitlementsStatus.Statuses.NotFound)]
    [InlineData("UNAUTHORIZED", UpdateDropsEntitlementsStatus.Statuses.Unauthorized)]
    [InlineData("UPDATE_FAILED", UpdateDropsEntitlementsStatus.Statuses.UpdateFailed)]
    public async Task UpdateDropsEntitlements_WhenResponseContainsDifferentStatuses_DeserializesAllEnumValues(string statusString, UpdateDropsEntitlementsStatus.Statuses expectedStatus)
    {
        this._fixture.RegisterJsonResponse("PATCH", "/helix/entitlements/drops", new
        {
            data = new[]
            {
                new
                {
                    status = statusString,
                    ids = _sampleEntitlementIds
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        UpdateDropsEntitlementsParameters parameters = new()
        {
            EntitlementIds = ["entitlement123"],
            FulfillmentStatus = DropEntitlement.FulfillmentStatuses.Fulfilled
        };

        ResponseData<UpdateDropsEntitlementsStatus>? response = await DropEntitlement.UpdateDropsEntitlements(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);
        _ = response.Data[0].Status.Should().Be(expectedStatus);
    }

    #endregion UpdateDropsEntitlements
}

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
using StreamActions.Twitch.Api.Extensions.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="ExtensionTransaction"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionTransactionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionTransactionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionTransactionTests(TwitchKestrelServerFixture fixture)
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

    #region GetExtensionTransactions

    [Fact]
    [Trait("Member", "GetExtensionTransactions")]
    public async Task GetExtensionTransactions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionTransaction.GetExtensionTransactions(null!, "ext123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetExtensionTransactions")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetExtensionTransactions_WhenExtensionIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidExtensionId)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionTransaction.GetExtensionTransactions(session, invalidExtensionId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("extensionId");
    }

    [Fact]
    [Trait("Member", "GetExtensionTransactions")]
    public async Task GetExtensionTransactions_WhenSessionTokenIsNotApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await ExtensionTransaction.GetExtensionTransactions(session, "ext123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetExtensionTransactions")]
    public async Task GetExtensionTransactions_WhenMoreThan100IdsProvided_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> ids = Enumerable.Range(1, 101).Select(i => $"id_{i}").ToList();

        Func<Task> action = async () => await ExtensionTransaction.GetExtensionTransactions(session, "ext123", ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    [Fact]
    [Trait("Member", "GetExtensionTransactions")]
    public async Task GetExtensionTransactions_WhenIdAndAfterBothProvided_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionTransaction.GetExtensionTransactions(session, "ext123", ["tx123"], after: "cursor").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("Can not use after at the same time as id");
    }

    [Fact]
    [Trait("Member", "GetExtensionTransactions")]
    public async Task GetExtensionTransactions_WhenValidRequest_SendsGetAndReturnsTransactions()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "id": "74c52265-e214-48a6-91b9-23b6014e8041",
              "timestamp": "2019-01-28T04:15:53.325Z",
              "broadcaster_id": "439964613",
              "broadcaster_login": "chikuseuma",
              "broadcaster_name": "chikuseuma",
              "user_id": "424596340",
              "user_login": "quotrok",
              "user_name": "quotrok",
              "product_type": "BITS_IN_EXTENSION",
              "product_data": {
                "domain": "twitch.ext.uo6dggojyb8d6soh92zknwmi5ej1q2",
                "sku": "testSku100",
                "cost": {
                  "amount": 100,
                  "type": "bits"
                },
                "inDevelopment": false,
                "displayName": "Test Product 100",
                "expiration": "2021-05-18T09:10:13.397Z",
                "broadcast": false
              }
            }
          ],
          "pagination": {
            "cursor": "cursorString"
          }
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/extensions/transactions", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ExtensionTransaction>? response = await ExtensionTransaction.GetExtensionTransactions(session, "uo6dggojyb8d6soh92zknwmi5ej1q2", ["74c52265-e214-48a6-91b9-23b6014e8041"], 20).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        ExtensionTransaction transaction = response.Data[0];
        _ = transaction.Id.Should().Be("74c52265-e214-48a6-91b9-23b6014e8041");
        _ = transaction.Timestamp.Should().Be(DateTime.Parse("2019-01-28T04:15:53.325Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = transaction.BroadcasterId.Should().Be("439964613");
        _ = transaction.BroadcasterLogin.Should().Be("chikuseuma");
        _ = transaction.BroadcasterName.Should().Be("chikuseuma");
        _ = transaction.UserId.Should().Be("424596340");
        _ = transaction.UserLogin.Should().Be("quotrok");
        _ = transaction.UserName.Should().Be("quotrok");
        _ = transaction.ProductType.Should().Be(ExtensionTransaction.ExtensionProductType.BitsInExtension);

        _ = transaction.ProductData.Should().NotBeNull();
        _ = transaction.ProductData!.Domain.Should().Be("twitch.ext.uo6dggojyb8d6soh92zknwmi5ej1q2");
        _ = transaction.ProductData.Sku.Should().Be("testSku100");
        _ = transaction.ProductData.Cost.Should().NotBeNull();
        _ = transaction.ProductData.Cost!.Amount.Should().Be(100);
        _ = transaction.ProductData.Cost.Type.Should().Be(ExtensionProductCost.ExtensionProductCostType.Bits);
        _ = transaction.ProductData.InDevelopment.Should().BeFalse();
        _ = transaction.ProductData.DisplayName.Should().Be("Test Product 100");
        _ = transaction.ProductData.Broadcast.Should().BeFalse();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/extensions/transactions");
        _ = request.QueryString.Should().Contain("extension_id=uo6dggojyb8d6soh92zknwmi5ej1q2");
        _ = request.QueryString.Should().Contain("id=74c52265-e214-48a6-91b9-23b6014e8041");
        _ = request.QueryString.Should().Contain("first=20");
    }

    #endregion GetExtensionTransactions
}

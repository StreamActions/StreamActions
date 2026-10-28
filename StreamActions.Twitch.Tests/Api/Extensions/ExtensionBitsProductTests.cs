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
using StreamActions.Twitch.Api.Extensions.Objects;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="ExtensionBitsProduct"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionBitsProductTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionBitsProductTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionBitsProductTests(TwitchKestrelServerFixture fixture)
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

    #region GetExtensionBitsProducts

    [Fact]
    [Trait("Member", "GetExtensionBitsProducts")]
    public async Task GetExtensionBitsProducts_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionBitsProduct.GetExtensionBitsProducts(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "GetExtensionBitsProducts")]
    public async Task GetExtensionBitsProducts_WhenSessionTokenIsNotApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await ExtensionBitsProduct.GetExtensionBitsProducts(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ExtensionBitsProduct.GetExtensionBitsProducts"/> retrieves a list of Bits products.
    /// </para>
    /// </summary>
    /// <param name="shouldIncludeAll">Whether to include all products.</param>
    [Theory]
    [Trait("Member", "GetExtensionBitsProducts")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetExtensionBitsProducts_WhenValidRequest_SendsGetAndReturnsProductList(bool shouldIncludeAll)
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "sku": "1010",
              "cost": {
                "amount": 990,
                "type": "bits"
              },
              "inDevelopment": true,
              "displayName": "Rusty Crate 2",
              "expiration": "2021-05-18T09:10:13.397Z",
              "broadcast": false,
              "domain": "twitch.ext.uo6dggojyb8d6soh92zknwmi5ej1q2"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/bits/extensions", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ExtensionProductData>? response = await ExtensionBitsProduct.GetExtensionBitsProducts(session, shouldIncludeAll).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ExtensionProductData product = response.Data[0];
        _ = product.Sku.Should().Be("1010");
        _ = product.Cost.Should().NotBeNull();
        _ = product.Cost.Amount.Should().Be(990);
        _ = product.Cost.Type.Should().Be(ExtensionProductCost.ExtensionProductCostType.Bits);
        _ = product.InDevelopment.Should().BeTrue();
        _ = product.DisplayName.Should().Be("Rusty Crate 2");
        _ = product.Expiration.Should().Be(DateTime.Parse("2021-05-18T09:10:13.397Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = product.Broadcast.Should().BeFalse();
        _ = product.Domain.Should().Be("twitch.ext.uo6dggojyb8d6soh92zknwmi5ej1q2");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/bits/extensions");
        _ = shouldIncludeAll
            ? request.QueryString.Should().Contain("should_include_all=true")
            : request.QueryString.Should().NotContain("should_include_all");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ExtensionBitsProduct.GetExtensionBitsProducts"/> omits should_include_all when the parameter is omitted.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionBitsProducts")]
    public async Task GetExtensionBitsProducts_WhenShouldIncludeAllOmitted_OmitsQueryParameter()
    {
        const string jsonResponse = """
        {
          "data": []
        }
        """;

        this._fixture.RegisterResponse("GET", "/helix/bits/extensions", jsonResponse);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ExtensionProductData>? response = await ExtensionBitsProduct.GetExtensionBitsProducts(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().NotContain("should_include_all");
    }

    #endregion GetExtensionBitsProducts

    #region UpdateExtensionBitsProduct

    [Fact]
    [Trait("Member", "UpdateExtensionBitsProduct")]
    public async Task UpdateExtensionBitsProduct_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        UpdateExtensionBitsProductParameters parameters = new()
        {
            Sku = "1010",
            DisplayName = "Rusty Crate 2"
        };

        Func<Task> action = async () => await ExtensionBitsProduct.UpdateExtensionBitsProduct(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "UpdateExtensionBitsProduct")]
    public async Task UpdateExtensionBitsProduct_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionBitsProduct.UpdateExtensionBitsProduct(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "UpdateExtensionBitsProduct")]
    public async Task UpdateExtensionBitsProduct_WhenSessionTokenIsNotApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        UpdateExtensionBitsProductParameters parameters = new()
        {
            Sku = "1010",
            DisplayName = "Rusty Crate 2"
        };

        Func<Task> action = async () => await ExtensionBitsProduct.UpdateExtensionBitsProduct(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ExtensionBitsProduct.UpdateExtensionBitsProduct"/> sends a PUT request with all fields and returns the updated product.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateExtensionBitsProduct")]
    public async Task UpdateExtensionBitsProduct_WhenValidRequest_SendsPutAndReturnsUpdatedProduct()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "sku": "1010",
              "cost": {
                "amount": 990,
                "type": "bits"
              },
              "inDevelopment": true,
              "displayName": "Rusty Crate 2",
              "expiration": "2021-05-18T09:10:13.397Z",
              "broadcast": true,
              "domain": "twitch.ext.uo6dggojyb8d6soh92zknwmi5ej1q2"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("PUT", "/helix/bits/extensions", jsonResponse);

        UpdateExtensionBitsProductParameters parameters = new()
        {
            Sku = "1010",
            Cost = new ExtensionProductCost
            {
                Amount = 990,
                Type = ExtensionProductCost.ExtensionProductCostType.Bits
            },
            InDevelopment = true,
            DisplayName = "Rusty Crate 2",
            IsBroadcast = true,
            Expiration = DateTime.Parse("2021-05-18T09:10:13.397Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)
        };

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ExtensionProductData>? response = await ExtensionBitsProduct.UpdateExtensionBitsProduct(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ExtensionProductData product = response.Data[0];
        _ = product.Sku.Should().Be("1010");
        _ = product.Cost.Should().NotBeNull();
        _ = product.Cost.Amount.Should().Be(990);
        _ = product.Cost.Type.Should().Be(ExtensionProductCost.ExtensionProductCostType.Bits);
        _ = product.DisplayName.Should().Be("Rusty Crate 2");
        _ = product.InDevelopment.Should().BeTrue();
        _ = product.Expiration.Should().Be(DateTime.Parse("2021-05-18T09:10:13.397Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = product.Broadcast.Should().BeTrue();
        _ = product.Domain.Should().Be("twitch.ext.uo6dggojyb8d6soh92zknwmi5ej1q2");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/bits/extensions");
        _ = request.Body.Should().Contain("\"sku\":\"1010\"");
        _ = request.Body.Should().Contain("\"display_name\":\"Rusty Crate 2\"");
        _ = request.Body.Should().Contain("\"cost\":");
        _ = request.Body.Should().Contain("\"amount\":990");
        _ = request.Body.Should().Contain("\"type\":\"bits\"");
        _ = request.Body.Should().Contain("\"in_development\":true");
        _ = request.Body.Should().Contain("\"expiration\":");
        _ = request.Body.Should().Contain("\"is_broadcast\":true");
    }

    /// <summary>
    /// <para>
    /// Tests that <see cref="ExtensionBitsProduct.UpdateExtensionBitsProduct"/> omits optional null fields from the request body.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateExtensionBitsProduct")]
    public async Task UpdateExtensionBitsProduct_WhenOptionalFieldsNull_OmitsOptionalFieldsInBody()
    {
        const string jsonResponse = """
        {
          "data": [
            {
              "sku": "1010",
              "displayName": "Rusty Crate 2"
            }
          ]
        }
        """;

        this._fixture.RegisterResponse("PUT", "/helix/bits/extensions", jsonResponse);

        UpdateExtensionBitsProductParameters parameters = new()
        {
            Sku = "1010",
            DisplayName = "Rusty Crate 2",
            Expiration = null,
            InDevelopment = null,
            IsBroadcast = null
        };

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ExtensionProductData>? response = await ExtensionBitsProduct.UpdateExtensionBitsProduct(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/bits/extensions");
        _ = request.Body.Should().Contain("\"sku\":\"1010\"");
        _ = request.Body.Should().Contain("\"display_name\":\"Rusty Crate 2\"");
        _ = request.Body.Should().NotContain("expiration");
        _ = request.Body.Should().NotContain("in_development");
        _ = request.Body.Should().NotContain("is_broadcast");
    }

    #endregion UpdateExtensionBitsProduct
}

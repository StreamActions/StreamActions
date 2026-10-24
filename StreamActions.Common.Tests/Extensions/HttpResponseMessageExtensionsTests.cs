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

using StreamActions.Common.Extensions;
using StreamActions.Common.Net;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Tests.Extensions;

/// <summary>
/// Performs unit testing on extension methods in <see cref="HttpResponseMessageExtensions"/>.
/// </summary>
public class HttpResponseMessageExtensionsTests
{
    #region Helper Types

    /// <summary>
    /// Test response type derived from <see cref="JsonApiResponse"/> for testing deserialization.
    /// </summary>
    private sealed record TestResponse : JsonApiResponse
    {
        /// <summary>
        /// Gets a custom test property.
        /// </summary>
        [JsonPropertyName("custom_field")]
        public string? CustomField { get; init; }
    }

    #endregion Helper Types

    #region ReadFromJsonAsync

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData((HttpStatusCode)299)]
    public async Task ReadFromJsonAsync_WithHttp2xxStatusCodes_ReturnsSuccessResponse(HttpStatusCode statusCode)
    {
        // Arrange
        using HttpResponseMessage message = new(statusCode);

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(statusCode);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.IsClientErrorStatusCode.Should().BeFalse();
        _ = response.IsServerErrorStatusCode.Should().BeFalse();
        _ = response.Message.Should().BeEmpty();
        _ = response.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode)499)]
    public async Task ReadFromJsonAsync_WithHttp4xxStatusCodes_ReturnsClientErrorResponse(HttpStatusCode statusCode)
    {
        // Arrange
        using HttpResponseMessage message = new(statusCode);

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(statusCode);
        _ = response.IsSuccessStatusCode.Should().BeFalse();
        _ = response.IsClientErrorStatusCode.Should().BeTrue();
        _ = response.IsServerErrorStatusCode.Should().BeFalse();
        _ = response.Message.Should().BeEmpty();
        _ = response.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData((HttpStatusCode)599)]
    public async Task ReadFromJsonAsync_WithHttp5xxStatusCodes_ReturnsServerErrorResponse(HttpStatusCode statusCode)
    {
        // Arrange
        using HttpResponseMessage message = new(statusCode);

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(statusCode);
        _ = response.IsSuccessStatusCode.Should().BeFalse();
        _ = response.IsClientErrorStatusCode.Should().BeFalse();
        _ = response.IsServerErrorStatusCode.Should().BeTrue();
        _ = response.Message.Should().BeEmpty();
        _ = response.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.Continue)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData((HttpStatusCode)0)]
    [InlineData((HttpStatusCode)399)]
    [InlineData((HttpStatusCode)600)]
    public async Task ReadFromJsonAsync_WithNon2xx4xx5xxStatusCodes_ReturnsAllFlagsFalse(HttpStatusCode statusCode)
    {
        // Arrange
        using HttpResponseMessage message = new(statusCode);

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(statusCode);
        _ = response.IsSuccessStatusCode.Should().BeFalse();
        _ = response.IsClientErrorStatusCode.Should().BeFalse();
        _ = response.IsServerErrorStatusCode.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WhenJsonErrorProvided_ReturnsProvidedError()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"custom_error_code\"}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Error.Should().Be("custom_error_code");
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.NotFound, "NotFound")]
    [InlineData((HttpStatusCode)299, "Unknown")]
    [InlineData((HttpStatusCode)599, "Unknown")]
    public async Task ReadFromJsonAsync_WhenJsonErrorIsNull_FallsBackToStatusNameOrUnknown(HttpStatusCode statusCode, string expectedError)
    {
        // Arrange
        using HttpResponseMessage message = new(statusCode)
        {
            Content = new StringContent("{\"error\":null}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Error.Should().Be(expectedError);
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.BadRequest, "BadRequest")]
    [InlineData((HttpStatusCode)299, "Unknown")]
    public async Task ReadFromJsonAsync_WhenJsonErrorIsEmpty_FallsBackToStatusNameOrUnknown(HttpStatusCode statusCode, string expectedError)
    {
        // Arrange
        using HttpResponseMessage message = new(statusCode)
        {
            Content = new StringContent("{\"error\":\"\"}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Error.Should().Be(expectedError);
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WhenJsonMessageProvided_ReturnsProvidedMessage()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"Something went wrong\"}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Message.Should().Be("Something went wrong");
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WhenJsonMessageOmitted_DefaultsToEmptyString()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Message.Should().Be("");
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WhenJsonStatusOmitted_SetsStatusToHttpResponseStatusCode()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"message\":\"Resource missing\"}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData(HttpStatusCode.NotFound, 200)]
    [InlineData(HttpStatusCode.OK, 500)]
    public async Task ReadFromJsonAsync_WhenJsonStatusMismatchesHttpResponseStatusCode_OverridesWithHttpResponseStatusCode(HttpStatusCode httpStatus, int jsonStatus)
    {
        // Arrange
        using HttpResponseMessage message = new(httpStatus)
        {
            Content = new StringContent($"{{\"status\":{jsonStatus},\"message\":\"Mismatched\"}}", Encoding.UTF8, "application/json")
        };

        // Act
        JsonApiResponse? response = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(httpStatus);
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithDerivedRecordType_PopulatesDerivedProperties()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"custom_field\":\"custom_data\"}", Encoding.UTF8, "application/json")
        };

        // Act
        TestResponse? response = await message.ReadFromJsonAsync<TestResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.CustomField.Should().Be("custom_data");
    }

    [Theory]
    [Trait("Member", "ReadFromJsonAsync")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public async Task ReadFromJsonAsync_WithDerivedRecordTypeAndEmptyOrWhitespaceContent_ReturnsNewInstanceWithStatus(string bodyContent)
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.NoContent)
        {
            Content = new StringContent(bodyContent, Encoding.UTF8, "application/json")
        };

        // Act
        TestResponse? response = await message.ReadFromJsonAsync<TestResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.CustomField.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithDerivedRecordTypeAndNullJsonBody_ReturnsNewInstanceWithStatus()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.NoContent)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };

        // Act
        TestResponse? response = await message.ReadFromJsonAsync<TestResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.CustomField.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithDerivedRecordTypeAndNullContent_ReturnsNewInstanceWithStatus()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.NoContent)
        {
            Content = null
        };

        // Act
        TestResponse? response = await message.ReadFromJsonAsync<TestResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.CustomField.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithDerivedRecordTypeAndZeroContentLength_ReturnsNewInstanceWithStatus()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.NoContent)
        {
            Content = new ByteArrayContent([])
        };

        // Act
        TestResponse? response = await message.ReadFromJsonAsync<TestResponse>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.CustomField.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithNullHttpResponseMessage_ThrowsArgumentNullException()
    {
        // Arrange
        HttpResponseMessage? message = null;

        // Act
        Func<Task> act = async () => _ = await message!.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Assert
        _ = (await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("message");
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        using HttpResponseMessage message = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{malformed_json", Encoding.UTF8, "application/json")
        };

        // Act
        Func<Task> act = async () => _ = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Assert
        _ = await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    [Trait("Member", "ReadFromJsonAsync")]
    public async Task ReadFromJsonAsync_WithCancelledCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        using HttpResponseMessage message = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        // Act
        Func<Task> act = async () => _ = await message.ReadFromJsonAsync<JsonApiResponse>(cancellationToken: cts.Token).ConfigureAwait(false);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion ReadFromJsonAsync
}

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

using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Extensions;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Extensions;

/// <summary>
/// Performs unit and integration testing on <see cref="ExtensionRequiredConfiguration"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionRequiredConfigurationTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionRequiredConfigurationTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionRequiredConfigurationTests(TwitchKestrelServerFixture fixture)
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

    #region SetExtensionRequiredConfiguration

    [Fact]
    [Trait("Member", "SetExtensionRequiredConfiguration")]
    public async Task SetExtensionRequiredConfiguration_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        SetExtensionRequiredConfigurationParameters parameters = new()
        {
            RequiredConfiguration = "RCS",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.1"
        };

        Func<Task> action = async () => await ExtensionRequiredConfiguration.SetExtensionRequiredConfiguration(null!, "274637212", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "SetExtensionRequiredConfiguration")]
    public async Task SetExtensionRequiredConfiguration_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Func<Task> action = async () => await ExtensionRequiredConfiguration.SetExtensionRequiredConfiguration(session, "274637212", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "SetExtensionRequiredConfiguration")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetExtensionRequiredConfiguration_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        SetExtensionRequiredConfigurationParameters parameters = new()
        {
            RequiredConfiguration = "RCS",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.1"
        };

        Func<Task> action = async () => await ExtensionRequiredConfiguration.SetExtensionRequiredConfiguration(session, invalidBroadcasterId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "SetExtensionRequiredConfiguration")]
    public async Task SetExtensionRequiredConfiguration_WhenSessionTokenIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        SetExtensionRequiredConfigurationParameters parameters = new()
        {
            RequiredConfiguration = "RCS",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.1"
        };

        Func<Task> action = async () => await ExtensionRequiredConfiguration.SetExtensionRequiredConfiguration(session, "274637212", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "SetExtensionRequiredConfiguration")]
    public async Task SetExtensionRequiredConfiguration_WhenValidRequest_SendsPutAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PUT", "/helix/extensions/required_configuration", StatusCodes.Status204NoContent);

        SetExtensionRequiredConfigurationParameters parameters = new()
        {
            RequiredConfiguration = "RCS",
            ExtensionId = "uo6dggojyb8d6soh92zknwmi5ej1q2",
            ExtensionVersion = "0.0.1"
        };

        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        JsonApiResponse? response = await ExtensionRequiredConfiguration.SetExtensionRequiredConfiguration(session, "274637212", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/extensions/required_configuration");
        _ = request.QueryString.Should().Contain("broadcaster_id=274637212");
        _ = request.Body.Should().Contain("\"required_configuration\":\"RCS\"");
        _ = request.Body.Should().Contain("\"extension_id\":\"uo6dggojyb8d6soh92zknwmi5ej1q2\"");
    }

    #endregion SetExtensionRequiredConfiguration
}

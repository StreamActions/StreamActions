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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="AutoModSettings"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class AutoModSettingsTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoModSettingsTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public AutoModSettingsTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetAutoModSettings

    [Fact]
    [Trait("Member", "GetAutoModSettings")]
    public async Task GetAutoModSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await AutoModSettings.GetAutoModSettings(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetAutoModSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAutoModSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadAutomodSettings);
        Func<Task> action = async () => await AutoModSettings.GetAutoModSettings(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "GetAutoModSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAutoModSettings_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadAutomodSettings);
        Func<Task> action = async () => await AutoModSettings.GetAutoModSettings(session, "1234", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "GetAutoModSettings")]
    public async Task GetAutoModSettings_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorReadAutomodSettings]
            }
        };

        Func<Task> action = async () => await AutoModSettings.GetAutoModSettings(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetAutoModSettings")]
    public async Task GetAutoModSettings_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await AutoModSettings.GetAutoModSettings(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetAutoModSettings")]
    public async Task GetAutoModSettings_WhenValidRequest_ReturnsSettings()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"overall_level\":null,\"disability\":0,\"aggression\":1,\"sexuality_sex_or_gender\":2,\"misogyny\":3,\"bullying\":4,\"swearing\":0,\"race_ethnicity_or_religion\":1,\"sex_based_terms\":2}]}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/automod/settings", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadAutomodSettings);
        ResponseData<AutoModSettings>? response = await AutoModSettings.GetAutoModSettings(session, "1234", "5678").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        AutoModSettings settings = response.Data![0];
        _ = settings.BroadcasterId.Should().Be("1234");
        _ = settings.ModeratorId.Should().Be("5678");
        _ = settings.OverallLevel.Should().BeNull();
        _ = settings.Disability.Should().Be(0);
        _ = settings.Aggression.Should().Be(1);
        _ = settings.SexualitySexOrGender.Should().Be(2);
        _ = settings.Misogyny.Should().Be(3);
        _ = settings.Bullying.Should().Be(4);
        _ = settings.Swearing.Should().Be(0);
        _ = settings.RaceEthnicityOrReligion.Should().Be(1);
        _ = settings.SexBasedTerms.Should().Be(2);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/automod/settings");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
    }

    #endregion GetAutoModSettings

    #region UpdateAutoModSettings

    [Fact]
    [Trait("Member", "UpdateAutoModSettings")]
    public async Task UpdateAutoModSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        AutoModSettings settings = new() { OverallLevel = 2 };
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(null!, "1234", "5678", settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "UpdateAutoModSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAutoModSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomodSettings);
        AutoModSettings settings = new() { OverallLevel = 2 };
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(session, invalidId!, "5678", settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "UpdateAutoModSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAutoModSettings_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomodSettings);
        AutoModSettings settings = new() { OverallLevel = 2 };
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(session, "1234", invalidId!, settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "UpdateAutoModSettings")]
    public async Task UpdateAutoModSettings_WhenSettingsIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomodSettings);
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(session, "1234", "5678", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("settings");
    }

    [Fact]
    [Trait("Member", "UpdateAutoModSettings")]
    public async Task UpdateAutoModSettings_WhenBothOverallAndIndividualSettingsProvided_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomodSettings);
        AutoModSettings settings = new() { OverallLevel = 2, Aggression = 1 };
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(session, "1234", "5678", settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("settings");
    }

    [Fact]
    [Trait("Member", "UpdateAutoModSettings")]
    public async Task UpdateAutoModSettings_WhenNeitherOverallNorIndividualSettingsProvided_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomodSettings);
        AutoModSettings settings = new();
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(session, "1234", "5678", settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("settings");
    }

    [Fact]
    [Trait("Member", "UpdateAutoModSettings")]
    public async Task UpdateAutoModSettings_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        AutoModSettings settings = new() { OverallLevel = 3 };
        Func<Task> action = async () => await AutoModSettings.UpdateAutoModSettings(session, "1234", "5678", settings).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "UpdateAutoModSettings")]
    public async Task UpdateAutoModSettings_WhenValidRequest_PutsSettingsAndReturnsUpdated()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"overall_level\":3,\"disability\":null,\"aggression\":null,\"sexuality_sex_or_gender\":null,\"misogyny\":null,\"bullying\":null,\"swearing\":null,\"race_ethnicity_or_religion\":null,\"sex_based_terms\":null}]}";
        this._fixture.RegisterResponse("PUT", "/helix/moderation/automod/settings", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomodSettings);
        AutoModSettings update = new() { OverallLevel = 3 };
        ResponseData<AutoModSettings>? response = await AutoModSettings.UpdateAutoModSettings(session, "1234", "5678", update).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        AutoModSettings result = response.Data![0];
        _ = result.BroadcasterId.Should().Be("1234");
        _ = result.ModeratorId.Should().Be("5678");
        _ = result.OverallLevel.Should().Be(3);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/moderation/automod/settings");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.Body.Should().Contain("\"overall_level\":3");
    }

    #endregion UpdateAutoModSettings
}

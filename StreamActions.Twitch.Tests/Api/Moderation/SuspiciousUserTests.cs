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
using StreamActions.Twitch.Api.Moderation;
using StreamActions.Twitch.Api.Moderation.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="SuspiciousUser"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class SuspiciousUserTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SuspiciousUserTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public SuspiciousUserTests(TwitchKestrelServerFixture fixture)
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

    #region AddSuspiciousStatus

    [Fact]
    [Trait("Member", "AddSuspiciousStatus")]
    public async Task AddSuspiciousStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = SuspiciousUser.SuspiciousStatuses.Restricted
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(null!, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "AddSuspiciousStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddSuspiciousStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = SuspiciousUser.SuspiciousStatuses.Restricted
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, invalidId!, "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "AddSuspiciousStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddSuspiciousStatus_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = SuspiciousUser.SuspiciousStatuses.Restricted
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "AddSuspiciousStatus")]
    public async Task AddSuspiciousStatus_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "AddSuspiciousStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddSuspiciousStatus_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = invalidUserId,
            Status = SuspiciousUser.SuspiciousStatuses.Restricted
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("UserId");
    }

    [Fact]
    [Trait("Member", "AddSuspiciousStatus")]
    public async Task AddSuspiciousStatus_WhenStatusIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = null
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Status");
    }

    [Fact]
    [Trait("Member", "AddSuspiciousStatus")]
    public async Task AddSuspiciousStatus_WhenStatusIsNoTreatment_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = SuspiciousUser.SuspiciousStatuses.NoTreatment
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Status");
    }

    [Fact]
    [Trait("Member", "AddSuspiciousStatus")]
    public async Task AddSuspiciousStatus_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorManageSuspiciousUsers]
            }
        };

        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = SuspiciousUser.SuspiciousStatuses.Restricted
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AddSuspiciousStatus")]
    public async Task AddSuspiciousStatus_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = SuspiciousUser.SuspiciousStatuses.Restricted
        };

        Func<Task> action = async () => await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "AddSuspiciousStatus")]
    [InlineData(SuspiciousUser.SuspiciousStatuses.Restricted)]
    [InlineData(SuspiciousUser.SuspiciousStatuses.ActiveMonitoring)]
    public async Task AddSuspiciousStatus_WhenValidRequest_PostsStatusAndReturnsResult(SuspiciousUser.SuspiciousStatuses status)
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"user_id\":\"6789\",\"status\":\"RESTRICTED\",\"types\":[\"MANUALLY_ADDED\"],\"updated_at\":\"2023-01-01T00:00:00Z\"}]}";
        this._fixture.RegisterResponse("POST", "/helix/moderation/suspicious_users", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        AddSuspiciousStatusParameters parameters = new()
        {
            UserId = "6789",
            Status = status
        };

        ResponseData<SuspiciousUser>? response = await SuspiciousUser.AddSuspiciousStatus(session, "1234", "5678", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        SuspiciousUser user = response.Data![0];
        _ = user.BroadcasterId.Should().Be("1234");
        _ = user.ModeratorId.Should().Be("5678");
        _ = user.UserId.Should().Be("6789");
        _ = user.Status.Should().Be(SuspiciousUser.SuspiciousStatuses.Restricted);
        _ = user.Types.Should().NotBeNull();
        _ = user.Types.Should().Contain(SuspiciousUser.SuspiciousUserTypes.ManuallyAdded);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/suspicious_users");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.Body.Should().Contain("\"user_id\":\"6789\"");
    }

    #endregion AddSuspiciousStatus

    #region RemoveSuspiciousStatus

    [Fact]
    [Trait("Member", "RemoveSuspiciousStatus")]
    public async Task RemoveSuspiciousStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await SuspiciousUser.RemoveSuspiciousStatus(null!, "1234", "5678", "6789").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "RemoveSuspiciousStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveSuspiciousStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        Func<Task> action = async () => await SuspiciousUser.RemoveSuspiciousStatus(session, invalidId!, "5678", "6789").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "RemoveSuspiciousStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveSuspiciousStatus_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        Func<Task> action = async () => await SuspiciousUser.RemoveSuspiciousStatus(session, "1234", invalidId!, "6789").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Theory]
    [Trait("Member", "RemoveSuspiciousStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveSuspiciousStatus_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        Func<Task> action = async () => await SuspiciousUser.RemoveSuspiciousStatus(session, "1234", "5678", invalidUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "RemoveSuspiciousStatus")]
    public async Task RemoveSuspiciousStatus_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await SuspiciousUser.RemoveSuspiciousStatus(session, "1234", "5678", "6789").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "RemoveSuspiciousStatus")]
    public async Task RemoveSuspiciousStatus_WhenValidRequest_DeletesStatusAndReturnsResult()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"user_id\":\"6789\",\"status\":\"NO_TREATMENT\",\"types\":[],\"updated_at\":\"2023-01-01T00:00:00Z\"}]}";
        this._fixture.RegisterResponse("DELETE", "/helix/moderation/suspicious_users", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageSuspiciousUsers);
        ResponseData<SuspiciousUser>? response = await SuspiciousUser.RemoveSuspiciousStatus(session, "1234", "5678", "6789").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        SuspiciousUser user = response.Data![0];
        _ = user.Status.Should().Be(SuspiciousUser.SuspiciousStatuses.NoTreatment);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/moderation/suspicious_users");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("user_id=6789");
    }

    #endregion RemoveSuspiciousStatus
}

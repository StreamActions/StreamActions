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
/// Performs unit tests on <see cref="Ban"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class BanTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BanTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public BanTests(TwitchKestrelServerFixture fixture)
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

    #region BanUser

    [Fact]
    [Trait("Member", "BanUser")]
    public async Task BanUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = "9876" }
        };
        Func<Task> action = async () => await Ban.BanUser(null!, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "BanUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BanUser_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = "9876" }
        };
        Func<Task> action = async () => await Ban.BanUser(session, invalidId!, "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "BanUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BanUser_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = "9876" }
        };
        Func<Task> action = async () => await Ban.BanUser(session, "1234", invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "BanUser")]
    public async Task BanUser_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        Func<Task> action = async () => await Ban.BanUser(session, "1234", "5678", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "BanUser")]
    public async Task BanUser_WhenParametersDataIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new() { Data = null };
        Func<Task> action = async () => await Ban.BanUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "BanUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BanUser_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = invalidUserId }
        };
        Func<Task> action = async () => await Ban.BanUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "BanUser")]
    [InlineData(0)]
    [InlineData(1209601)]
    public async Task BanUser_WhenDurationOutOfRange_ThrowsArgumentOutOfRangeException(long invalidDuration)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = "9876", DurationSeconds = invalidDuration }
        };
        Func<Task> action = async () => await Ban.BanUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "BanUser")]
    public async Task BanUser_WhenReasonTooLong_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = "9876", Reason = new string('x', 501) }
        };
        Func<Task> action = async () => await Ban.BanUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "BanUser")]
    public async Task BanUser_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        BanUserParameters parameters = new()
        {
            Data = new() { UserId = "9876" }
        };
        Func<Task> action = async () => await Ban.BanUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "BanUser")]
    public async Task BanUser_WhenValidRequest_PostsAndReturnsBanResponse()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"user_id\":\"9876\",\"created_at\":\"2022-03-15T01:30:28Z\",\"end_time\":\"2022-03-15T02:00:28Z\"}]}";
        this._fixture.RegisterResponse("POST", "/helix/moderation/bans", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        BanUserParameters parameters = new()
        {
            Data = new()
            {
                UserId = "9876",
                DurationSeconds = 1800,
                Reason = "Timeout test"
            }
        };

        _ = parameters.Data.Duration.Should().Be(TimeSpan.FromSeconds(1800));

        ResponseData<Ban>? response = await Ban.BanUser(session, "1234", "5678", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Ban ban = response.Data[0];
        _ = ban.BroadcasterId.Should().Be("1234");
        _ = ban.ModeratorId.Should().Be("5678");
        _ = ban.UserId.Should().Be("9876");
        _ = ban.CreatedAt.Should().Be(DateTime.Parse("2022-03-15T01:30:28Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = ban.EndTime.Should().Be(DateTime.Parse("2022-03-15T02:00:28Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/bans");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.Body.Should().Contain("\"user_id\":\"9876\"");
        _ = request.Body.Should().Contain("\"duration\":1800");
        _ = request.Body.Should().Contain("\"reason\":\"Timeout test\"");
    }

    #endregion BanUser

    #region UnbanUser

    [Fact]
    [Trait("Member", "UnbanUser")]
    public async Task UnbanUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Ban.UnbanUser(null!, "1234", "5678", "9876").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "UnbanUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnbanUser_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        Func<Task> action = async () => await Ban.UnbanUser(session, invalidId!, "5678", "9876").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "UnbanUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnbanUser_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        Func<Task> action = async () => await Ban.UnbanUser(session, "1234", invalidId!, "9876").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Theory]
    [Trait("Member", "UnbanUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnbanUser_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        Func<Task> action = async () => await Ban.UnbanUser(session, "1234", "5678", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    [Fact]
    [Trait("Member", "UnbanUser")]
    public async Task UnbanUser_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Ban.UnbanUser(session, "1234", "5678", "9876").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "UnbanUser")]
    public async Task UnbanUser_WhenValidRequest_DeletesAndReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/bans", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBannedUsers);
        JsonApiResponse? response = await Ban.UnbanUser(session, "1234", "5678", "9876").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/moderation/bans");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("user_id=9876");
    }

    #endregion UnbanUser
}

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
/// Performs unit tests on <see cref="Warning"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class WarningTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WarningTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public WarningTests(TwitchKestrelServerFixture fixture)
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

    #region WarnChatUser

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Please follow chat rules"
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(null!, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "WarnChatUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WarnChatUser_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Please follow chat rules"
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, invalidId!, "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "WarnChatUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WarnChatUser_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Please follow chat rules"
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenDataIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new() { Data = null };
        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Data");
    }

    [Theory]
    [Trait("Member", "WarnChatUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WarnChatUser_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = invalidUserId,
                Reason = "Please follow chat rules"
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("UserId");
    }

    [Theory]
    [Trait("Member", "WarnChatUser")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WarnChatUser_WhenReasonIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidReason)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = invalidReason
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Reason");
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenReasonTooLong_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = new string('x', 501)
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Reason");
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorManageWarnings]
            }
        };

        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Please follow chat rules"
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Please follow chat rules"
            }
        };

        Func<Task> action = async () => await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenValidUserRequest_WarnsUserAndReturnsResponse()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"user_id\":\"9876\",\"reason\":\"Please follow chat rules\"}]}";
        this._fixture.RegisterResponse("POST", "/helix/moderation/warnings", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Please follow chat rules"
            }
        };

        ResponseData<Warning>? response = await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Warning item = response.Data[0];
        _ = item.BroadcasterId.Should().Be("1234");
        _ = item.ModeratorId.Should().Be("5678");
        _ = item.UserId.Should().Be("9876");
        _ = item.Reason.Should().Be("Please follow chat rules");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/warnings");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.Body.Should().Contain("\"user_id\":\"9876\"");
        _ = request.Body.Should().Contain("\"reason\":\"Please follow chat rules\"");
    }

    [Fact]
    [Trait("Member", "WarnChatUser")]
    public async Task WarnChatUser_WhenAppToken_WarnsUserAndReturnsResponse()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"user_id\":\"9876\",\"reason\":\"Warned via bot\"}]}";
        this._fixture.RegisterResponse("POST", "/helix/moderation/warnings", json);

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModeratorManageWarnings);
        WarnChatUserParameters parameters = new()
        {
            Data = new WarnChatUserData
            {
                UserId = "9876",
                Reason = "Warned via bot"
            }
        };

        ResponseData<Warning>? response = await Warning.WarnChatUser(session, "1234", "5678", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data[0].Reason.Should().Be("Warned via bot");
    }

    #endregion WarnChatUser
}

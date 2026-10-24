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
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="AutoModStatus"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class AutoModStatusTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoModStatusTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public AutoModStatusTests(TwitchKestrelServerFixture fixture)
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

    #region CheckAutoModStatus

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        CheckAutoModStatusParameters parameters = new()
        {
            Data = [new() { MsgId = "123", MsgText = "Hello" }]
        };
        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(null!, "12345", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "CheckAutoModStatus")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckAutoModStatus_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        CheckAutoModStatusParameters parameters = new()
        {
            Data = [new() { MsgId = "123", MsgText = "Hello" }]
        };
        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(session, invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(session, "12345", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenParametersDataIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        CheckAutoModStatusParameters parameters = new() { Data = null };
        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(session, "12345", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenParametersDataIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        CheckAutoModStatusParameters parameters = new() { Data = [] };
        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(session, "12345", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenParametersDataExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        List<CheckAutoModStatusData> items = [];
        for (int i = 0; i < 101; i++)
        {
            items.Add(new() { MsgId = i.ToString(CultureInfo.InvariantCulture), MsgText = $"Msg {i}" });
        }
        CheckAutoModStatusParameters parameters = new() { Data = items };

        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(session, "12345", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        CheckAutoModStatusParameters parameters = new()
        {
            Data = [new() { MsgId = "123", MsgText = "Hello" }]
        };
        Func<Task> action = async () => await AutoModStatus.CheckAutoModStatus(session, "12345", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CheckAutoModStatus")]
    public async Task CheckAutoModStatus_WhenValidRequest_PostsAndReturnsStatusList()
    {
        const string json = "{\"data\":[{\"msg_id\":\"123\",\"is_permitted\":true},{\"msg_id\":\"393\",\"is_permitted\":false}]}";
        this._fixture.RegisterResponse("POST", "/helix/moderation/enforcements/status", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModerationRead);
        CheckAutoModStatusParameters parameters = new()
        {
            Data =
            [
                new() { MsgId = "123", MsgText = "Hello World!" },
                new() { MsgId = "393", MsgText = "Boooooo!" }
            ]
        };

        ResponseData<AutoModStatus>? response = await AutoModStatus.CheckAutoModStatus(session, "12345", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(2);

        AutoModStatus first = response.Data.ElementAt(0);
        _ = first.MsgId.Should().Be("123");
        _ = first.IsPermitted.Should().BeTrue();

        AutoModStatus second = response.Data.ElementAt(1);
        _ = second.MsgId.Should().Be("393");
        _ = second.IsPermitted.Should().BeFalse();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/enforcements/status");
        _ = request.QueryString.Should().Contain("broadcaster_id=12345");
        _ = request.Body.Should().Contain("\"msg_id\":\"123\"");
        _ = request.Body.Should().Contain("\"msg_text\":\"Hello World!\"");
    }

    #endregion CheckAutoModStatus

    #region ManageHeldAutoModMessage

    [Fact]
    [Trait("Member", "ManageHeldAutoModMessage")]
    public async Task ManageHeldAutoModMessage_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ManageHeldAutoModMessageParameters parameters = new()
        {
            UserId = "9327994",
            MsgId = "836013710",
            Action = ManageHeldAutoModMessageParameters.AutoModActions.Allow
        };
        Func<Task> action = async () => await AutoModStatus.ManageHeldAutoModMessage(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "ManageHeldAutoModMessage")]
    public async Task ManageHeldAutoModMessage_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomod);
        Func<Task> action = async () => await AutoModStatus.ManageHeldAutoModMessage(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "ManageHeldAutoModMessage")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ManageHeldAutoModMessage_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomod);
        ManageHeldAutoModMessageParameters parameters = new()
        {
            UserId = invalidUserId,
            MsgId = "836013710",
            Action = ManageHeldAutoModMessageParameters.AutoModActions.Allow
        };
        Func<Task> action = async () => await AutoModStatus.ManageHeldAutoModMessage(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "ManageHeldAutoModMessage")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ManageHeldAutoModMessage_WhenMsgIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidMsgId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomod);
        ManageHeldAutoModMessageParameters parameters = new()
        {
            UserId = "9327994",
            MsgId = invalidMsgId,
            Action = ManageHeldAutoModMessageParameters.AutoModActions.Allow
        };
        Func<Task> action = async () => await AutoModStatus.ManageHeldAutoModMessage(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "ManageHeldAutoModMessage")]
    public async Task ManageHeldAutoModMessage_WhenActionIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomod);
        ManageHeldAutoModMessageParameters parameters = new()
        {
            UserId = "9327994",
            MsgId = "836013710",
            Action = null
        };
        Func<Task> action = async () => await AutoModStatus.ManageHeldAutoModMessage(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "ManageHeldAutoModMessage")]
    public async Task ManageHeldAutoModMessage_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        ManageHeldAutoModMessageParameters parameters = new()
        {
            UserId = "9327994",
            MsgId = "836013710",
            Action = ManageHeldAutoModMessageParameters.AutoModActions.Allow
        };
        Func<Task> action = async () => await AutoModStatus.ManageHeldAutoModMessage(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ManageHeldAutoModMessage")]
    public async Task ManageHeldAutoModMessage_WhenValidRequest_PostsAndReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("POST", "/helix/moderation/automod/message", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageAutomod);
        ManageHeldAutoModMessageParameters parameters = new()
        {
            UserId = "9327994",
            MsgId = "836013710",
            Action = ManageHeldAutoModMessageParameters.AutoModActions.Allow
        };

        JsonApiResponse? response = await AutoModStatus.ManageHeldAutoModMessage(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/automod/message");
        _ = request.Body.Should().Contain("\"user_id\":\"9327994\"");
        _ = request.Body.Should().Contain("\"msg_id\":\"836013710\"");
        _ = request.Body.Should().Contain("\"action\":\"ALLOW\"");
    }

    #endregion ManageHeldAutoModMessage
}

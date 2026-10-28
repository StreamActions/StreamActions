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

using StreamActions.Twitch.Api.Moderation;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="UnbanRequest"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class UnbanRequestTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UnbanRequestTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public UnbanRequestTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetUnbanRequests

    [Fact]
    [Trait("Member", "GetUnbanRequests")]
    public async Task GetUnbanRequests_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await UnbanRequest.GetUnbanRequests(null!, "1234", "5678", UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetUnbanRequests")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUnbanRequests_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadUnbanRequests);
        Func<Task> action = async () => await UnbanRequest.GetUnbanRequests(session, invalidId!, "5678", UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "GetUnbanRequests")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUnbanRequests_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadUnbanRequests);
        Func<Task> action = async () => await UnbanRequest.GetUnbanRequests(session, "1234", invalidId!, UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "GetUnbanRequests")]
    public async Task GetUnbanRequests_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModeratorReadUnbanRequests);
        Func<Task> action = async () => await UnbanRequest.GetUnbanRequests(session, "1234", "5678", UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetUnbanRequests")]
    public async Task GetUnbanRequests_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorReadUnbanRequests]
            }
        };

        Func<Task> action = async () => await UnbanRequest.GetUnbanRequests(session, "1234", "5678", UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetUnbanRequests")]
    public async Task GetUnbanRequests_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await UnbanRequest.GetUnbanRequests(session, "1234", "5678", UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "GetUnbanRequests")]
    [InlineData("moderator:read:unban_requests")]
    [InlineData("moderator:manage:unban_requests")]
    public async Task GetUnbanRequests_WhenValidRequest_ReturnsUnbanRequests(string scopeName)
    {
        const string json = "{\"data\":[{\"id\":\"req-1\",\"broadcaster_id\":\"1234\",\"broadcaster_login\":\"streamer\",\"broadcaster_name\":\"Streamer\",\"moderator_id\":\"5678\",\"moderator_login\":\"mod\",\"moderator_name\":\"Mod\",\"user_id\":\"9876\",\"user_login\":\"bannedguy\",\"user_name\":\"BannedGuy\",\"text\":\"Please unban me\",\"status\":\"pending\",\"created_at\":\"2023-01-01T00:00:00Z\",\"resolved_at\":null,\"resolution_text\":null}],\"pagination\":{\"cursor\":\"cursor123\"}}";
        this._fixture.RegisterResponse("GET", "/moderation/unban_requests", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<UnbanRequest>? response = await UnbanRequest.GetUnbanRequests(
            session,
            "1234",
            "5678",
            UnbanRequest.UnbanRequestStatuses.Pending,
            userId: "9876",
            after: "cursor123",
            first: 50).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor123");

        UnbanRequest item = response.Data[0];
        _ = item.Id.Should().Be("req-1");
        _ = item.BroadcasterId.Should().Be("1234");
        _ = item.BroadcasterLogin.Should().Be("streamer");
        _ = item.BroadcasterName.Should().Be("Streamer");
        _ = item.ModeratorId.Should().Be("5678");
        _ = item.ModeratorLogin.Should().Be("mod");
        _ = item.ModeratorName.Should().Be("Mod");
        _ = item.UserId.Should().Be("9876");
        _ = item.UserLogin.Should().Be("bannedguy");
        _ = item.UserName.Should().Be("BannedGuy");
        _ = item.Text.Should().Be("Please unban me");
        _ = item.Status.Should().Be(UnbanRequest.UnbanRequestStatuses.Pending);
        _ = item.CreatedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.ResolvedAt.Should().BeNull();
        _ = item.ResolutionText.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/unban_requests");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("status=pending");
        _ = request.QueryString.Should().Contain("user_id=9876");
        _ = request.QueryString.Should().Contain("first=50");
        _ = request.QueryString.Should().Contain("after=cursor123");
    }

    /// <summary>
    /// <para>
    /// Verifies that <see cref="UnbanRequest.GetUnbanRequests(StreamActions.Twitch.Api.Common.TwitchSession, string, string, UnbanRequest.UnbanRequestStatuses, string?, string?, int)"/>
    /// omits optional parameters from the query string when using default values.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "GetUnbanRequests")]
    public async Task GetUnbanRequests_WhenDefaultOptionalParameters_OmitsOptionalQueryParameters()
    {
        const string json = "{\"data\":[]}";
        this._fixture.RegisterResponse("GET", "/moderation/unban_requests", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadUnbanRequests);
        _ = await UnbanRequest.GetUnbanRequests(session, "1234", "5678", UnbanRequest.UnbanRequestStatuses.Pending).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("status=pending");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().NotContain("user_id=");
        _ = request.QueryString.Should().NotContain("after=");
    }

    /// <summary>
    /// <para>
    /// Verifies that <see cref="UnbanRequest.GetUnbanRequests(StreamActions.Twitch.Api.Common.TwitchSession, string, string, UnbanRequest.UnbanRequestStatuses, string?, string?, int)"/>
    /// clamps the first parameter to the valid range [1, 100].
    /// </para>
    /// </summary>
    /// <param name="inputFirst">The input first count.</param>
    /// <param name="expectedFirst">The expected clamped first count in query string.</param>
    [Theory]
    [Trait("Member", "GetUnbanRequests")]
    [InlineData(0, 1)]
    [InlineData(101, 100)]
    public async Task GetUnbanRequests_WhenFirstOutOfRange_ClampsFirst(int inputFirst, int expectedFirst)
    {
        const string json = "{\"data\":[]}";
        this._fixture.RegisterResponse("GET", "/moderation/unban_requests", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadUnbanRequests);
        _ = await UnbanRequest.GetUnbanRequests(session, "1234", "5678", UnbanRequest.UnbanRequestStatuses.Pending, first: inputFirst).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain($"first={expectedFirst}");
    }

    /// <summary>
    /// <para>
    /// Verifies that <see cref="UnbanRequest.GetUnbanRequests(StreamActions.Twitch.Api.Common.TwitchSession, string, string, UnbanRequest.UnbanRequestStatuses, string?, string?, int)"/>
    /// sets the correct status query parameter for each <see cref="UnbanRequest.UnbanRequestStatuses"/> value.
    /// </para>
    /// </summary>
    /// <param name="status">The unban request status.</param>
    /// <param name="expectedStatusString">The expected status query string value.</param>
    [Theory]
    [Trait("Member", "GetUnbanRequests")]
    [InlineData(UnbanRequest.UnbanRequestStatuses.Pending, "pending")]
    [InlineData(UnbanRequest.UnbanRequestStatuses.Approved, "approved")]
    [InlineData(UnbanRequest.UnbanRequestStatuses.Denied, "denied")]
    [InlineData(UnbanRequest.UnbanRequestStatuses.Acknowledged, "acknowledged")]
    [InlineData(UnbanRequest.UnbanRequestStatuses.Canceled, "canceled")]
    public async Task GetUnbanRequests_WhenDifferentStatuses_SetsCorrectStatusQueryParameter(UnbanRequest.UnbanRequestStatuses status, string expectedStatusString)
    {
        const string json = "{\"data\":[]}";
        this._fixture.RegisterResponse("GET", "/moderation/unban_requests", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadUnbanRequests);
        _ = await UnbanRequest.GetUnbanRequests(session, "1234", "5678", status).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain($"status={expectedStatusString}");
    }

    #endregion GetUnbanRequests

    #region ResolveUnbanRequest

    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved,
            ResolutionText = "Accepted"
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(null!, "1234", "5678", "req-1", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "ResolveUnbanRequest")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveUnbanRequest_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved,
            ResolutionText = "Accepted"
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, invalidId!, "5678", "req-1", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "ResolveUnbanRequest")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveUnbanRequest_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved,
            ResolutionText = "Accepted"
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", invalidId!, "req-1", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Theory]
    [Trait("Member", "ResolveUnbanRequest")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveUnbanRequest_WhenUnbanRequestIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved,
            ResolutionText = "Accepted"
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("unbanRequestId");
    }

    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenResolutionTextTooLong_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved,
            ResolutionText = new string('a', 501)
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Verifies that <see cref="UnbanRequest.ResolveUnbanRequest(StreamActions.Twitch.Api.Common.TwitchSession, string, string, string, ResolveUnbanRequestParameters)"/>
    /// throws an <see cref="InvalidOperationException"/> when the token has an empty OAuth string.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ModeratorManageUnbanRequests]
            }
        };

        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved
        };

        Func<Task> action = async () => await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// <para>
    /// Verifies that <see cref="UnbanRequest.ResolveUnbanRequest(StreamActions.Twitch.Api.Common.TwitchSession, string, string, string, ResolveUnbanRequestParameters)"/>
    /// sends a PATCH request and deserializes the full unban request response.
    /// </para>
    /// </summary>
    /// <param name="resolution">The resolution status to apply.</param>
    /// <param name="expectedStatusString">The expected status query/body string.</param>
    /// <param name="expectedStatus">The expected unban request status enum.</param>
    [Theory]
    [Trait("Member", "ResolveUnbanRequest")]
    [InlineData(ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved, "approved", UnbanRequest.UnbanRequestStatuses.Approved)]
    [InlineData(ResolveUnbanRequestParameters.UnbanRequestResolutions.Denied, "denied", UnbanRequest.UnbanRequestStatuses.Denied)]
    public async Task ResolveUnbanRequest_WhenValidRequest_PatchesRequestAndReturnsResult(
        ResolveUnbanRequestParameters.UnbanRequestResolutions resolution,
        string expectedStatusString,
        UnbanRequest.UnbanRequestStatuses expectedStatus)
    {
        string json = $"{{\"data\":[{{\"id\":\"req-1\",\"broadcaster_id\":\"1234\",\"broadcaster_login\":\"streamer\",\"broadcaster_name\":\"Streamer\",\"moderator_id\":\"5678\",\"moderator_login\":\"mod\",\"moderator_name\":\"Mod\",\"user_id\":\"9876\",\"user_login\":\"bannedguy\",\"user_name\":\"BannedGuy\",\"text\":\"Please unban me\",\"status\":\"{expectedStatusString}\",\"created_at\":\"2023-01-01T00:00:00Z\",\"resolved_at\":\"2023-01-02T00:00:00Z\",\"resolution_text\":\"You have learned your lesson\"}}]}}";
        this._fixture.RegisterResponse("PATCH", "/helix/moderation/unban_requests", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = resolution,
            ResolutionText = "You have learned your lesson"
        };

        ResponseData<UnbanRequest>? response = await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        UnbanRequest item = response.Data[0];
        _ = item.Id.Should().Be("req-1");
        _ = item.BroadcasterId.Should().Be("1234");
        _ = item.BroadcasterLogin.Should().Be("streamer");
        _ = item.BroadcasterName.Should().Be("Streamer");
        _ = item.ModeratorId.Should().Be("5678");
        _ = item.ModeratorLogin.Should().Be("mod");
        _ = item.ModeratorName.Should().Be("Mod");
        _ = item.UserId.Should().Be("9876");
        _ = item.UserLogin.Should().Be("bannedguy");
        _ = item.UserName.Should().Be("BannedGuy");
        _ = item.Text.Should().Be("Please unban me");
        _ = item.Status.Should().Be(expectedStatus);
        _ = item.CreatedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.ResolvedAt.Should().Be(DateTime.Parse("2023-01-02T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.ResolutionText.Should().Be("You have learned your lesson");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/moderation/unban_requests");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("unban_request_id=req-1");
        _ = request.Body.Should().Contain($"\"status\":\"{expectedStatusString}\"");
        _ = request.Body.Should().Contain("\"resolution_text\":\"You have learned your lesson\"");
    }

    /// <summary>
    /// <para>
    /// Verifies that <see cref="UnbanRequest.ResolveUnbanRequest(StreamActions.Twitch.Api.Common.TwitchSession, string, string, string, ResolveUnbanRequestParameters)"/>
    /// omits resolution_text from the request body when it is null.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Member", "ResolveUnbanRequest")]
    public async Task ResolveUnbanRequest_WhenResolutionTextIsNull_OmitsResolutionTextAndReturnsResult()
    {
        const string json = "{\"data\":[{\"id\":\"req-1\",\"broadcaster_id\":\"1234\",\"broadcaster_login\":\"streamer\",\"broadcaster_name\":\"Streamer\",\"moderator_id\":\"5678\",\"moderator_login\":\"mod\",\"moderator_name\":\"Mod\",\"user_id\":\"9876\",\"user_login\":\"bannedguy\",\"user_name\":\"BannedGuy\",\"text\":\"Please unban me\",\"status\":\"approved\",\"created_at\":\"2023-01-01T00:00:00Z\",\"resolved_at\":\"2023-01-02T00:00:00Z\",\"resolution_text\":null}]}";
        this._fixture.RegisterResponse("PATCH", "/helix/moderation/unban_requests", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageUnbanRequests);
        ResolveUnbanRequestParameters parameters = new()
        {
            Status = ResolveUnbanRequestParameters.UnbanRequestResolutions.Approved,
            ResolutionText = null
        };

        ResponseData<UnbanRequest>? response = await UnbanRequest.ResolveUnbanRequest(session, "1234", "5678", "req-1", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Data[0].ResolutionText.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("\"resolution_text\":null");
    }

    #endregion ResolveUnbanRequest
}

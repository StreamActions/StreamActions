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
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Moderation;

/// <summary>
/// Performs unit tests on <see cref="BlockedTerm"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class BlockedTermTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockedTermTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public BlockedTermTests(TwitchKestrelServerFixture fixture)
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

    #region GetBlockedTerms

    [Fact]
    [Trait("Member", "GetBlockedTerms")]
    public async Task GetBlockedTerms_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await BlockedTerm.GetBlockedTerms(null!, "1234", "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetBlockedTerms")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBlockedTerms_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadBlockedTerms);
        Func<Task> action = async () => await BlockedTerm.GetBlockedTerms(session, invalidId!, "5678").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "GetBlockedTerms")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBlockedTerms_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadBlockedTerms);
        Func<Task> action = async () => await BlockedTerm.GetBlockedTerms(session, "1234", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "GetBlockedTerms")]
    public async Task GetBlockedTerms_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await BlockedTerm.GetBlockedTerms(session, "1234", "5678").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetBlockedTerms")]
    public async Task GetBlockedTerms_WhenValidRequest_ReturnsBlockedTerms()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"id\":\"520e4d4e-0cda-49c7-821e-e5ef4f88c2f2\",\"text\":\"A phrase Im not fond of\",\"created_at\":\"2021-09-29T19:45:37Z\",\"updated_at\":\"2021-09-29T19:45:37Z\",\"expires_at\":null}],\"pagination\":{\"cursor\":\"eyJiIjpudWxsLCJhIjp7IkN1cnNvciI6I\"}}";
        this._fixture.RegisterResponse("GET", "/helix/moderation/blocked_terms", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadBlockedTerms);
        ResponseData<BlockedTerm>? response = await BlockedTerm.GetBlockedTerms(session, "1234", "5678", first: 10, after: "cursor123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Pagination.Should().NotBeNull();

        BlockedTerm term = response.Data![0];
        _ = term.BroadcasterId.Should().Be("1234");
        _ = term.ModeratorId.Should().Be("5678");
        _ = term.Id.Should().Be("520e4d4e-0cda-49c7-821e-e5ef4f88c2f2");
        _ = term.Text.Should().Be("A phrase Im not fond of");
        _ = term.CreatedAt.Should().Be(DateTime.Parse("2021-09-29T19:45:37Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = term.UpdatedAt.Should().Be(DateTime.Parse("2021-09-29T19:45:37Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = term.ExpiresAt.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/moderation/blocked_terms");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("first=10");
        _ = request.QueryString.Should().Contain("after=cursor123");
    }

    #endregion GetBlockedTerms

    #region AddBlockedTerm

    [Fact]
    [Trait("Member", "AddBlockedTerm")]
    public async Task AddBlockedTerm_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        AddBlockedTermParameters parameters = new() { Text = "blocked" };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(null!, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "AddBlockedTerm")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddBlockedTerm_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        AddBlockedTermParameters parameters = new() { Text = "blocked" };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, invalidId!, "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "AddBlockedTerm")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddBlockedTerm_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        AddBlockedTermParameters parameters = new() { Text = "blocked" };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, "1234", invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Fact]
    [Trait("Member", "AddBlockedTerm")]
    public async Task AddBlockedTerm_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, "1234", "5678", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "AddBlockedTerm")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddBlockedTerm_WhenTextIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidText)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        AddBlockedTermParameters parameters = new() { Text = invalidText };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "AddBlockedTerm")]
    public async Task AddBlockedTerm_WhenTextTooShort_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        AddBlockedTermParameters parameters = new() { Text = "a" };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "AddBlockedTerm")]
    public async Task AddBlockedTerm_WhenTextTooLong_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        AddBlockedTermParameters parameters = new() { Text = new string('x', 501) };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Fact]
    [Trait("Member", "AddBlockedTerm")]
    public async Task AddBlockedTerm_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        AddBlockedTermParameters parameters = new() { Text = "blocked" };
        Func<Task> action = async () => await BlockedTerm.AddBlockedTerm(session, "1234", "5678", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "AddBlockedTerm")]
    public async Task AddBlockedTerm_WhenValidRequest_PostsAndReturnsCreatedTerm()
    {
        const string json = "{\"data\":[{\"broadcaster_id\":\"1234\",\"moderator_id\":\"5678\",\"id\":\"c9fc79b8-0f63-4ef7-9d38-efd811e74ac2\",\"text\":\"hate speech\",\"created_at\":\"2021-09-29T19:45:37Z\",\"updated_at\":\"2021-09-29T19:45:37Z\",\"expires_at\":null}]}";
        this._fixture.RegisterResponse("POST", "/helix/moderation/blocked_terms", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        AddBlockedTermParameters parameters = new() { Text = "hate speech" };
        ResponseData<BlockedTerm>? response = await BlockedTerm.AddBlockedTerm(session, "1234", "5678", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        BlockedTerm term = response.Data![0];
        _ = term.BroadcasterId.Should().Be("1234");
        _ = term.ModeratorId.Should().Be("5678");
        _ = term.Id.Should().Be("c9fc79b8-0f63-4ef7-9d38-efd811e74ac2");
        _ = term.Text.Should().Be("hate speech");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/moderation/blocked_terms");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.Body.Should().Contain("\"text\":\"hate speech\"");
    }

    #endregion AddBlockedTerm

    #region RemoveBlockedTerm

    [Fact]
    [Trait("Member", "RemoveBlockedTerm")]
    public async Task RemoveBlockedTerm_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await BlockedTerm.RemoveBlockedTerm(null!, "1234", "5678", "term_id").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "RemoveBlockedTerm")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveBlockedTerm_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        Func<Task> action = async () => await BlockedTerm.RemoveBlockedTerm(session, invalidId!, "5678", "term_id").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Theory]
    [Trait("Member", "RemoveBlockedTerm")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveBlockedTerm_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        Func<Task> action = async () => await BlockedTerm.RemoveBlockedTerm(session, "1234", invalidId!, "term_id").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    [Theory]
    [Trait("Member", "RemoveBlockedTerm")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveBlockedTerm_WhenIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        Func<Task> action = async () => await BlockedTerm.RemoveBlockedTerm(session, "1234", "5678", invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    [Fact]
    [Trait("Member", "RemoveBlockedTerm")]
    public async Task RemoveBlockedTerm_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await BlockedTerm.RemoveBlockedTerm(session, "1234", "5678", "term_id").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "RemoveBlockedTerm")]
    public async Task RemoveBlockedTerm_WhenValidRequest_DeletesAndReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/moderation/blocked_terms", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageBlockedTerms);
        JsonApiResponse? response = await BlockedTerm.RemoveBlockedTerm(session, "1234", "5678", "c9fc79b8-0f63-4ef7-9d38-efd811e74ac2").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/moderation/blocked_terms");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("moderator_id=5678");
        _ = request.QueryString.Should().Contain("id=c9fc79b8-0f63-4ef7-9d38-efd811e74ac2");
    }

    #endregion RemoveBlockedTerm
}

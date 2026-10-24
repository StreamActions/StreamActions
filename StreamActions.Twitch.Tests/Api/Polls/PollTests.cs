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
using StreamActions.Twitch.Api.Polls;
using StreamActions.Twitch.Api.Polls.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Polls;

/// <summary>
/// Performs unit tests on <see cref="Poll"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class PollTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PollTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public PollTests(TwitchKestrelServerFixture fixture)
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

    #region Duration

    [Fact]
    [Trait("Member", "Duration")]
    public void Duration_WhenDurationSecondsIsNull_ReturnsNull()
    {
        Poll poll = new() { DurationSeconds = null };
        _ = poll.Duration.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "Duration")]
    public void Duration_WhenDurationSecondsHasValue_ReturnsTimeSpan()
    {
        Poll poll = new() { DurationSeconds = 300 };
        _ = poll.Duration.Should().Be(TimeSpan.FromSeconds(300));
    }

    #endregion Duration

    #region GetPolls

    [Fact]
    [Trait("Member", "GetPolls")]
    public async Task GetPolls_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Poll.GetPolls(null!, "1234").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetPolls")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPolls_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadPolls);
        Func<Task> action = async () => await Poll.GetPolls(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetPolls")]
    public async Task GetPolls_WhenIdCountExceeds20_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadPolls);
        List<string> ids = Enumerable.Range(1, 21).Select(static i => $"id_{i}").ToList();
        Func<Task> action = async () => await Poll.GetPolls(session, "1234", id: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    [Fact]
    [Trait("Member", "GetPolls")]
    public async Task GetPolls_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelReadPolls);
        Func<Task> action = async () => await Poll.GetPolls(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetPolls")]
    public async Task GetPolls_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ChannelReadPolls]
            }
        };

        Func<Task> action = async () => await Poll.GetPolls(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetPolls")]
    public async Task GetPolls_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Poll.GetPolls(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "GetPolls")]
    [InlineData("channel:read:polls")]
    [InlineData("channel:manage:polls")]
    public async Task GetPolls_WhenValidRequest_ReturnsPolls(string scopeName)
    {
        const string json = "{\"data\":[{\"id\":\"p1\",\"broadcaster_id\":\"1234\",\"broadcaster_name\":\"Streamer\",\"broadcaster_login\":\"streamer\",\"title\":\"Best flavor?\",\"choices\":[{\"id\":\"c1\",\"title\":\"Chocolate\",\"votes\":10,\"channel_points_votes\":5},{\"id\":\"c2\",\"title\":\"Vanilla\",\"votes\":8,\"channel_points_votes\":4}],\"channel_points_voting_enabled\":true,\"channel_points_per_vote\":100,\"status\":\"ACTIVE\",\"duration\":300,\"started_at\":\"2023-01-01T00:00:00Z\",\"ended_at\":null}],\"pagination\":{\"cursor\":\"cursor123\"}}";
        this._fixture.RegisterResponse("GET", "/polls", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<Poll>? response = await Poll.GetPolls(session, "1234", id: ["p1", "p2"], first: 20, after: "cursor123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Poll poll = response.Data[0];
        _ = poll.Id.Should().Be("p1");
        _ = poll.BroadcasterId.Should().Be("1234");
        _ = poll.BroadcasterName.Should().Be("Streamer");
        _ = poll.BroadcasterLogin.Should().Be("streamer");
        _ = poll.Title.Should().Be("Best flavor?");
        _ = poll.Status.Should().Be(Poll.PollStatus.Active);
        _ = poll.DurationSeconds.Should().Be(300);
        _ = poll.Duration.Should().Be(TimeSpan.FromSeconds(300));
        _ = poll.ChannelPointsVotingEnabled.Should().BeTrue();
        _ = poll.ChannelPointsPerVote.Should().Be(100);
        _ = poll.StartedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = poll.EndedAt.Should().BeNull();
        _ = poll.Choices.Should().NotBeNull();
        _ = poll.Choices.Should().HaveCount(2);

        PollChoice choice1 = poll.Choices[0];
        _ = choice1.Id.Should().Be("c1");
        _ = choice1.Title.Should().Be("Chocolate");
        _ = choice1.Votes.Should().Be(10);
        _ = choice1.ChannelPointsVotes.Should().Be(5);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/polls");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("id=p1");
        _ = request.QueryString.Should().Contain("id=p2");
        _ = request.QueryString.Should().Contain("after=cursor123");
    }

    #endregion GetPolls

    #region CreatePoll

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        Func<Task> action = async () => await Poll.CreatePoll(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "CreatePoll")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePoll_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = invalidId,
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("BroadcasterId");
    }

    [Theory]
    [Trait("Member", "CreatePoll")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePoll_WhenTitleIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTitle)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = invalidTitle,
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenDurationIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = null,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Duration");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenChoicesIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = null
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Choices");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenTitleExceeds60Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = new string('a', 61),
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    [Theory]
    [Trait("Member", "CreatePoll")]
    [InlineData(1)]
    [InlineData(6)]
    public async Task CreatePoll_WhenChoicesCountOutOfRange_ThrowsArgumentOutOfRangeException(int count)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        List<PollCreationChoice> choices = Enumerable.Range(1, count).Select(static i => new PollCreationChoice { Title = $"Choice {i}" }).ToList();
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = choices
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Choices");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenChoiceTitleIsNullOrWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "  " }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Choices");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenChoiceTitleExceeds25Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = new string('c', 26) }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Choices");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenChannelPointsEnabledWithoutPointsPerVote_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }],
            ChannelPointsVotingEnabled = true,
            ChannelPointsPerVote = null
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ChannelPointsPerVote");
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Poll Title",
            Duration = 300,
            Choices = [new() { Title = "Choice 1" }, new() { Title = "Choice 2" }]
        };

        Func<Task> action = async () => await Poll.CreatePoll(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CreatePoll")]
    public async Task CreatePoll_WhenValidRequest_PostsAndReturnsPoll()
    {
        const string json = "{\"data\":[{\"id\":\"p1\",\"broadcaster_id\":\"1234\",\"broadcaster_name\":\"Streamer\",\"broadcaster_login\":\"streamer\",\"title\":\"Favorite Game?\",\"choices\":[{\"id\":\"c1\",\"title\":\"Game A\",\"votes\":0,\"channel_points_votes\":0},{\"id\":\"c2\",\"title\":\"Game B\",\"votes\":0,\"channel_points_votes\":0}],\"channel_points_voting_enabled\":true,\"channel_points_per_vote\":10,\"status\":\"ACTIVE\",\"duration\":300,\"started_at\":\"2023-01-01T00:00:00Z\",\"ended_at\":null}]}";
        this._fixture.RegisterResponse("POST", "/helix/polls", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Favorite Game?",
            Duration = 300,
            Choices = [new() { Title = "Game A" }, new() { Title = "Game B" }],
            ChannelPointsVotingEnabled = true,
            ChannelPointsPerVote = 10
        };

        ResponseData<Poll>? response = await Poll.CreatePoll(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Poll poll = response.Data[0];
        _ = poll.Id.Should().Be("p1");
        _ = poll.Title.Should().Be("Favorite Game?");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/polls");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"1234\"");
        _ = request.Body.Should().Contain("\"title\":\"Favorite Game?\"");
    }

    #endregion CreatePoll

    #region EndPoll

    [Fact]
    [Trait("Member", "EndPoll")]
    public async Task EndPoll_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = "p1",
            Status = Poll.PollStatus.Terminated
        };

        Func<Task> action = async () => await Poll.EndPoll(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "EndPoll")]
    public async Task EndPoll_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        Func<Task> action = async () => await Poll.EndPoll(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "EndPoll")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EndPoll_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollEndParameters parameters = new()
        {
            BroadcasterId = invalidId,
            Id = "p1",
            Status = Poll.PollStatus.Terminated
        };

        Func<Task> action = async () => await Poll.EndPoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("BroadcasterId");
    }

    [Theory]
    [Trait("Member", "EndPoll")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EndPoll_WhenIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = invalidId,
            Status = Poll.PollStatus.Terminated
        };

        Func<Task> action = async () => await Poll.EndPoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");
    }

    [Fact]
    [Trait("Member", "EndPoll")]
    public async Task EndPoll_WhenStatusIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = "p1",
            Status = null
        };

        Func<Task> action = async () => await Poll.EndPoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Status");
    }

    [Theory]
    [Trait("Member", "EndPoll")]
    [InlineData(Poll.PollStatus.Active)]
    [InlineData(Poll.PollStatus.Completed)]
    [InlineData(Poll.PollStatus.Invalid)]
    [InlineData(Poll.PollStatus.Moderated)]
    public async Task EndPoll_WhenStatusInvalid_ThrowsArgumentOutOfRangeException(Poll.PollStatus invalidStatus)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = "p1",
            Status = invalidStatus
        };

        Func<Task> action = async () => await Poll.EndPoll(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Status");
    }

    [Fact]
    [Trait("Member", "EndPoll")]
    public async Task EndPoll_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManagePolls);
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = "p1",
            Status = Poll.PollStatus.Terminated
        };

        Func<Task> action = async () => await Poll.EndPoll(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "EndPoll")]
    public async Task EndPoll_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = "p1",
            Status = Poll.PollStatus.Terminated
        };

        Func<Task> action = async () => await Poll.EndPoll(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "EndPoll")]
    [InlineData(Poll.PollStatus.Terminated)]
    [InlineData(Poll.PollStatus.Archived)]
    public async Task EndPoll_WhenValidRequest_PatchesAndReturnsPoll(Poll.PollStatus status)
    {
        const string json = "{\"data\":[{\"id\":\"p1\",\"broadcaster_id\":\"1234\",\"broadcaster_name\":\"Streamer\",\"broadcaster_login\":\"streamer\",\"title\":\"Favorite Game?\",\"choices\":[{\"id\":\"c1\",\"title\":\"Game A\",\"votes\":5,\"channel_points_votes\":2}],\"status\":\"TERMINATED\",\"duration\":300,\"started_at\":\"2023-01-01T00:00:00Z\",\"ended_at\":\"2023-01-01T00:05:00Z\"}]}";
        this._fixture.RegisterResponse("PATCH", "/helix/polls", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePolls);
        PollEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = "p1",
            Status = status
        };

        ResponseData<Poll>? response = await Poll.EndPoll(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Poll poll = response.Data[0];
        _ = poll.Id.Should().Be("p1");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/polls");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"1234\"");
        _ = request.Body.Should().Contain("\"id\":\"p1\"");
    }

    #endregion EndPoll
}

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

using FluentAssertions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Teams;
using StreamActions.Twitch.Api.Teams.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Teams;

/// <summary>
/// Provides unit tests for the <see cref="Team"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class TeamTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public TeamTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetChannelTeams

    /// <summary>
    /// Tests that <see cref="Team.GetChannelTeams"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelTeams")]
    public async Task GetChannelTeams_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Team.GetChannelTeams(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Team.GetChannelTeams"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetChannelTeams")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelTeams_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Team.GetChannelTeams(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Team.GetChannelTeams"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelTeams")]
    public async Task GetChannelTeams_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await Team.GetChannelTeams(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Team.GetChannelTeams"/> returns teams on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelTeams")]
    public async Task GetChannelTeams_WhenRequestSucceeds_ReturnsTeams()
    {
        this._fixture.RegisterEndpoint("/helix/teams/channel", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""broadcaster_id"": ""96909659"",
      ""broadcaster_name"": ""spooky"",
      ""broadcaster_login"": ""spooky"",
      ""background_image_url"": ""https://static-cdn.jtvnw.net/jtv_user_pictures/team-background.png"",
      ""banner"": ""https://static-cdn.jtvnw.net/jtv_user_pictures/team-banner.png"",
      ""created_at"": ""2019-02-11T12:00:00Z"",
      ""updated_at"": ""2020-11-10T15:00:00Z"",
      ""info"": ""The best team"",
      ""thumbnail_url"": ""https://static-cdn.jtvnw.net/jtv_user_pictures/team-logo.png"",
      ""team_name"": ""staff"",
      ""team_display_name"": ""Twitch Staff"",
      ""id"": ""1""
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Team>? response = await Team.GetChannelTeams(session, "96909659").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Team team = response.Data[0];
        _ = team.BroadcasterId.Should().Be("96909659");
        _ = team.BroadcasterName.Should().Be("spooky");
        _ = team.BroadcasterLogin.Should().Be("spooky");
        _ = team.BackgroundImageUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/jtv_user_pictures/team-background.png"));
        _ = team.Banner.Should().Be(new Uri("https://static-cdn.jtvnw.net/jtv_user_pictures/team-banner.png"));
        _ = team.CreatedAt.Should().Be(DateTime.Parse("2019-02-11T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = team.UpdatedAt.Should().Be(DateTime.Parse("2020-11-10T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
        _ = team.Info.Should().Be("The best team");
        _ = team.ThumbnailUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/jtv_user_pictures/team-logo.png"));
        _ = team.TeamName.Should().Be("staff");
        _ = team.TeamDisplayName.Should().Be("Twitch Staff");
        _ = team.Id.Should().Be("1");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/teams/channel");
        _ = request.QueryString.Should().Contain("broadcaster_id=96909659");
    }

    #endregion GetChannelTeams

    #region GetTeams

    /// <summary>
    /// Tests that <see cref="Team.GetTeams"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetTeams")]
    public async Task GetTeams_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Team.GetTeams(null!, teamId: "1").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Team.GetTeams"/> throws <see cref="ArgumentNullException"/> when both teamId and teamName are null or whitespace.
    /// </summary>
    /// <param name="teamId">The team ID.</param>
    /// <param name="teamName">The team name.</param>
    [Theory]
    [Trait("Member", "GetTeams")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task GetTeams_WhenBothTeamIdAndTeamNameAreNullOrWhitespace_ThrowsArgumentNullException(string? teamId, string? teamName)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Team.GetTeams(session, teamId, teamName).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("teamId,teamName");
    }

    /// <summary>
    /// Tests that <see cref="Team.GetTeams"/> throws <see cref="ArgumentOutOfRangeException"/> when both teamId and teamName are specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetTeams")]
    public async Task GetTeams_WhenBothTeamIdAndTeamNameAreSpecified_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Team.GetTeams(session, teamId: "1", teamName: "staff").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("teamId,teamName");
    }

    /// <summary>
    /// Tests that <see cref="Team.GetTeams"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "GetTeams")]
    public async Task GetTeams_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await Team.GetTeams(session, teamId: "1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Team.GetTeams"/> returns team information by team ID on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetTeams")]
    public async Task GetTeams_WhenCalledWithTeamId_ReturnsTeam()
    {
        this._fixture.RegisterEndpoint("/helix/teams", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""1"",
      ""team_name"": ""staff"",
      ""team_display_name"": ""Twitch Staff"",
      ""info"": ""Staff team"",
      ""users"": [
        {
          ""user_id"": ""141981764"",
          ""user_login"": ""twitchdev"",
          ""user_name"": ""TwitchDev""
        }
      ]
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Team>? response = await Team.GetTeams(session, teamId: "1").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Team team = response.Data[0];
        _ = team.Id.Should().Be("1");
        _ = team.TeamName.Should().Be("staff");
        _ = team.Users.Should().NotBeNull();
        _ = team.Users!.Count.Should().Be(1);

        TeamMember member = team.Users[0];
        _ = member.UserId.Should().Be("141981764");
        _ = member.UserLogin.Should().Be("twitchdev");
        _ = member.UserName.Should().Be("TwitchDev");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/teams");
        _ = request.QueryString.Should().Contain("id=1");
    }

    /// <summary>
    /// Tests that <see cref="Team.GetTeams"/> queries with team name when teamName is provided.
    /// </summary>
    [Fact]
    [Trait("Member", "GetTeams")]
    public async Task GetTeams_WhenCalledWithTeamName_ReturnsTeam()
    {
        this._fixture.RegisterEndpoint("/helix/teams", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""id"": ""1"",
      ""team_name"": ""staff"",
      ""team_display_name"": ""Twitch Staff""
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Team>? response = await Team.GetTeams(session, teamName: "staff").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/teams");
        _ = request.QueryString.Should().Contain("name=staff");
    }

    #endregion GetTeams
}

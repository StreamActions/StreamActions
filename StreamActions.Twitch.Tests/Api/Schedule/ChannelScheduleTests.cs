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
using StreamActions.Twitch.Api.Schedule;
using StreamActions.Twitch.Api.Schedule.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Schedule;

/// <summary>
/// Provides unit tests for the <see cref="ChannelSchedule"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelScheduleTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelScheduleTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChannelScheduleTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after each test.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region GetChannelStreamSchedule

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelStreamSchedule"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelStreamSchedule")]
    public async Task GetChannelStreamSchedule_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelSchedule.GetChannelStreamSchedule(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelStreamSchedule"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetChannelStreamSchedule")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelStreamSchedule_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await ChannelSchedule.GetChannelStreamSchedule(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelStreamSchedule"/> throws <see cref="ArgumentOutOfRangeException"/> when segmentIds exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelStreamSchedule")]
    public async Task GetChannelStreamSchedule_WhenSegmentIdsExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        List<string> segmentIds = [.. Enumerable.Range(1, 101).Select(i => $"seg_{i}")];

        Func<Task> action = async () => await ChannelSchedule.GetChannelStreamSchedule(session, "141981764", segmentIds: segmentIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("segmentIds");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelStreamSchedule"/> returns schedule information when given valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelStreamSchedule")]
    public async Task GetChannelStreamSchedule_WhenCalledWithValidParameters_ReturnsSchedule()
    {
        this._fixture.RegisterResponse("GET", "/helix/schedule", @"{
  ""data"": {
    ""segments"": [
      {
        ""id"": ""segment123"",
        ""start_time"": ""2021-07-01T18:00:00Z"",
        ""end_time"": ""2021-07-01T19:00:00Z"",
        ""title"": ""TwitchDev Monthly Update // July 1, 2021"",
        ""canceled_until"": null,
        ""category"": {
          ""id"": ""509670"",
          ""name"": ""Science & Technology""
        },
        ""is_recurring"": false
      }
    ],
    ""broadcaster_id"": ""141981764"",
    ""broadcaster_name"": ""TwitchDev"",
    ""broadcaster_login"": ""twitchdev"",
    ""vacation"": {
      ""start_time"": ""2021-08-01T00:00:00Z"",
      ""end_time"": ""2021-08-15T00:00:00Z""
    }
  },
  ""pagination"": {}
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseDataObject<ChannelSchedule>? response = await ChannelSchedule.GetChannelStreamSchedule(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();

        ChannelSchedule schedule = response.Data;
        _ = schedule.BroadcasterId.Should().Be("141981764");
        _ = schedule.BroadcasterName.Should().Be("TwitchDev");
        _ = schedule.BroadcasterLogin.Should().Be("twitchdev");
        _ = schedule.Vacation.Should().NotBeNull();
        _ = schedule.Vacation.StartTime.Should().Be(DateTime.Parse("2021-08-01T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = schedule.Vacation.EndTime.Should().Be(DateTime.Parse("2021-08-15T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = schedule.Segments.Should().NotBeNull();
        _ = schedule.Segments.Count.Should().Be(1);

        ScheduleSegment segment = schedule.Segments[0];
        _ = segment.Id.Should().Be("segment123");
        _ = segment.StartTime.Should().Be(DateTime.Parse("2021-07-01T18:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = segment.EndTime.Should().Be(DateTime.Parse("2021-07-01T19:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = segment.Title.Should().Be("TwitchDev Monthly Update // July 1, 2021");
        _ = segment.CanceledUntil.Should().BeNull();
        _ = segment.IsRecurring.Should().BeFalse();
        _ = segment.Category.Should().NotBeNull();
        _ = segment.Category.Id.Should().Be("509670");
        _ = segment.Category.Name.Should().Be("Science & Technology");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/schedule");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelStreamSchedule"/> sends optional query parameters when specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelStreamSchedule")]
    public async Task GetChannelStreamSchedule_WhenOptionalParametersProvided_SendsExpectedQueryParameters()
    {
        this._fixture.RegisterResponse("GET", "/helix/schedule", @"{ ""data"": { ""segments"": [], ""broadcaster_id"": ""141981764"", ""broadcaster_name"": ""TwitchDev"", ""broadcaster_login"": ""twitchdev"", ""vacation"": null }, ""pagination"": {} }");

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        DateTime startTime = DateTime.Parse("2021-08-01T12:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        List<string> segmentIds = ["seg1", "seg2"];

        ResponseDataObject<ChannelSchedule>? response = await ChannelSchedule.GetChannelStreamSchedule(session, "141981764", segmentIds: segmentIds, startTime: startTime, first: 15, after: "cursor123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("id=seg1");
        _ = request.QueryString.Should().Contain("id=seg2");
        _ = request.QueryString.Should().Contain($"start_time={Uri.EscapeDataString(startTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}");
        _ = request.QueryString.Should().Contain("first=15");
        _ = request.QueryString.Should().Contain("after=cursor123");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelStreamSchedule"/> clamps the first parameter between 1 and 25.
    /// </summary>
    /// <param name="first">The first count to test.</param>
    /// <param name="expectedClamped">The expected clamped count.</param>
    [Theory]
    [Trait("Member", "GetChannelStreamSchedule")]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(30, 25)]
    [InlineData(100, 25)]
    public async Task GetChannelStreamSchedule_WhenFirstOutOfRange_ClampsFirst(int first, int expectedClamped)
    {
        this._fixture.RegisterResponse("GET", "/helix/schedule", @"{ ""data"": { ""segments"": [], ""broadcaster_id"": ""141981764"", ""broadcaster_name"": ""TwitchDev"", ""broadcaster_login"": ""twitchdev"", ""vacation"": null }, ""pagination"": {} }");

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseDataObject<ChannelSchedule>? response = await ChannelSchedule.GetChannelStreamSchedule(session, "141981764", first: first).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain($"first={expectedClamped}");
    }

    #endregion GetChannelStreamSchedule

    #region GetChannelICalendar

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelICalendar"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelICalendar")]
    public async Task GetChannelICalendar_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelSchedule.GetChannelICalendar(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelICalendar"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetChannelICalendar")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelICalendar_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await ChannelSchedule.GetChannelICalendar(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.GetChannelICalendar"/> returns iCalendar data when given valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelICalendar")]
    public async Task GetChannelICalendar_WhenCalledWithValidParameters_ReturnsCalendarString()
    {
        const string calendarContent = "BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//twitch.tv//StreamSchedule//1.0\nNAME:TwitchDev\nEND:VCALENDAR";
        this._fixture.RegisterResponse("GET", "/helix/schedule/icalendar", calendarContent, contentType: "text/calendar");

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        string? result = await ChannelSchedule.GetChannelICalendar(session, "141981764").ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.Should().Be(calendarContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/schedule/icalendar");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    #endregion GetChannelICalendar

    #region UpdateChannelStreamSchedule

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamSchedule"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamSchedule")]
    public async Task UpdateChannelStreamSchedule_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamSchedule(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamSchedule"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "UpdateChannelStreamSchedule")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateChannelStreamSchedule_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamSchedule(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamSchedule"/> throws <see cref="TokenTypeException"/> when session is an App token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamSchedule")]
    public async Task UpdateChannelStreamSchedule_WhenSessionIsAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamSchedule(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamSchedule"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamSchedule")]
    public async Task UpdateChannelStreamSchedule_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamSchedule(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamSchedule"/> updates settings and returns success response.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamSchedule")]
    public async Task UpdateChannelStreamSchedule_WhenCalledWithValidParameters_ReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/schedule/settings", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        DateTime start = DateTime.UtcNow.AddDays(7);
        DateTime end = DateTime.UtcNow.AddDays(14);

        JsonApiResponse? response = await ChannelSchedule.UpdateChannelStreamSchedule(session, "141981764", isVacationEnabled: true, vacationStartTime: start, vacationEndTime: end, timezone: "America/New_York").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/schedule/settings");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("is_vacation_enabled=true");
        _ = request.QueryString.Should().Contain($"vacation_start_time={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}");
        _ = request.QueryString.Should().Contain($"vacation_end_time={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}");
        _ = request.QueryString.Should().Contain("timezone=America%2FNew_York");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamSchedule"/> omits vacation times and timezone when only isVacationEnabled is set.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamSchedule")]
    public async Task UpdateChannelStreamSchedule_WhenOnlyIsVacationEnabledProvided_OmitsOptionalQueryParameters()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/schedule/settings", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        JsonApiResponse? response = await ChannelSchedule.UpdateChannelStreamSchedule(session, "141981764", isVacationEnabled: false).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("is_vacation_enabled=false");
        _ = request.QueryString.Should().NotContain("vacation_start_time");
        _ = request.QueryString.Should().NotContain("vacation_end_time");
        _ = request.QueryString.Should().NotContain("timezone");
    }

    #endregion UpdateChannelStreamSchedule

    #region CreateChannelStreamScheduleSegment

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.CreateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateChannelStreamScheduleSegment")]
    public async Task CreateChannelStreamScheduleSegment_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        CreateChannelStreamScheduleSegmentParameters parameters = new()
        {
            StartTime = DateTime.UtcNow.AddDays(1),
            Timezone = "America/New_York",
            Duration = "60",
            Title = "Test Stream"
        };

        Func<Task> action = async () => await ChannelSchedule.CreateChannelStreamScheduleSegment(null!, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.CreateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when parameters are null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateChannelStreamScheduleSegment")]
    public async Task CreateChannelStreamScheduleSegment_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        Func<Task> action = async () => await ChannelSchedule.CreateChannelStreamScheduleSegment(session, "141981764", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.CreateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "CreateChannelStreamScheduleSegment")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateChannelStreamScheduleSegment_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        CreateChannelStreamScheduleSegmentParameters parameters = new()
        {
            StartTime = DateTime.UtcNow.AddDays(1),
            Timezone = "America/New_York",
            Duration = "60",
            Title = "Test Stream"
        };

        Func<Task> action = async () => await ChannelSchedule.CreateChannelStreamScheduleSegment(session, invalidBroadcasterId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.CreateChannelStreamScheduleSegment"/> throws <see cref="TokenTypeException"/> when session is an App token.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateChannelStreamScheduleSegment")]
    public async Task CreateChannelStreamScheduleSegment_WhenSessionIsAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        CreateChannelStreamScheduleSegmentParameters parameters = new()
        {
            StartTime = DateTime.UtcNow.AddDays(1),
            Timezone = "America/New_York",
            Duration = "60",
            Title = "Test Stream"
        };

        Func<Task> action = async () => await ChannelSchedule.CreateChannelStreamScheduleSegment(session, "141981764", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.CreateChannelStreamScheduleSegment"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateChannelStreamScheduleSegment")]
    public async Task CreateChannelStreamScheduleSegment_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        CreateChannelStreamScheduleSegmentParameters parameters = new()
        {
            StartTime = DateTime.UtcNow.AddDays(1),
            Timezone = "America/New_York",
            Duration = "60",
            Title = "Test Stream"
        };

        Func<Task> action = async () => await ChannelSchedule.CreateChannelStreamScheduleSegment(session, "141981764", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.CreateChannelStreamScheduleSegment"/> creates segment and returns schedule.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateChannelStreamScheduleSegment")]
    public async Task CreateChannelStreamScheduleSegment_WhenCalledWithValidParameters_ReturnsCreatedSegment()
    {
        this._fixture.RegisterResponse("POST", "/helix/schedule/segment", @"{
  ""data"": {
    ""segments"": [
      {
        ""id"": ""seg_new_123"",
        ""start_time"": ""2021-07-01T18:00:00Z"",
        ""end_time"": ""2021-07-01T19:00:00Z"",
        ""title"": ""TwitchDev Monthly Update // July 1, 2021"",
        ""canceled_until"": null,
        ""category"": {
          ""id"": ""509670"",
          ""name"": ""Science & Technology""
        },
        ""is_recurring"": false
      }
    ],
    ""broadcaster_id"": ""141981764"",
    ""broadcaster_name"": ""TwitchDev"",
    ""broadcaster_login"": ""twitchdev"",
    ""vacation"": null
  }
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        CreateChannelStreamScheduleSegmentParameters parameters = new()
        {
            StartTime = DateTime.Parse("2021-07-01T18:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            Timezone = "America/New_York",
            Duration = "60",
            IsRecurring = false,
            CategoryId = "509670",
            Title = "TwitchDev Monthly Update // July 1, 2021"
        };

        ResponseDataObject<ChannelSchedule>? response = await ChannelSchedule.CreateChannelStreamScheduleSegment(session, "141981764", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();

        ChannelSchedule schedule = response.Data;
        _ = schedule.BroadcasterId.Should().Be("141981764");
        _ = schedule.BroadcasterName.Should().Be("TwitchDev");
        _ = schedule.BroadcasterLogin.Should().Be("twitchdev");
        _ = schedule.Vacation.Should().BeNull();
        _ = schedule.Segments.Should().NotBeNull();
        _ = schedule.Segments.Count.Should().Be(1);

        ScheduleSegment segment = schedule.Segments[0];
        _ = segment.Id.Should().Be("seg_new_123");
        _ = segment.StartTime.Should().Be(DateTime.Parse("2021-07-01T18:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = segment.EndTime.Should().Be(DateTime.Parse("2021-07-01T19:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = segment.Title.Should().Be("TwitchDev Monthly Update // July 1, 2021");
        _ = segment.CanceledUntil.Should().BeNull();
        _ = segment.IsRecurring.Should().BeFalse();
        _ = segment.Category.Should().NotBeNull();
        _ = segment.Category.Id.Should().Be("509670");
        _ = segment.Category.Name.Should().Be("Science & Technology");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/schedule/segment");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.Body.Should().Contain("\"start_time\"");
        _ = request.Body.Should().Contain("\"timezone\":\"America/New_York\"");
        _ = request.Body.Should().Contain("\"duration\":\"60\"");
        _ = request.Body.Should().Contain("\"is_recurring\":false");
        _ = request.Body.Should().Contain("\"category_id\":\"509670\"");
        _ = request.Body.Should().Contain("\"title\":\"TwitchDev Monthly Update // July 1, 2021\"");
    }

    #endregion CreateChannelStreamScheduleSegment

    #region UpdateChannelStreamScheduleSegment

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    public async Task UpdateChannelStreamScheduleSegment_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        UpdateChannelStreamScheduleSegmentParameters parameters = new() { Duration = "120" };
        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamScheduleSegment(null!, "141981764", "seg_123", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateChannelStreamScheduleSegment_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        UpdateChannelStreamScheduleSegmentParameters parameters = new() { Duration = "120" };

        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamScheduleSegment(session, invalidBroadcasterId!, "seg_123", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when segmentId is null or whitespace.
    /// </summary>
    /// <param name="invalidSegmentId">The invalid segment ID to test.</param>
    [Theory]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateChannelStreamScheduleSegment_WhenSegmentIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSegmentId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        UpdateChannelStreamScheduleSegmentParameters parameters = new() { Duration = "120" };

        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamScheduleSegment(session, "141981764", invalidSegmentId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("segmentId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when parameters are null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    public async Task UpdateChannelStreamScheduleSegment_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamScheduleSegment(session, "141981764", "seg_123", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> throws <see cref="TokenTypeException"/> when session is an App token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    public async Task UpdateChannelStreamScheduleSegment_WhenSessionIsAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        UpdateChannelStreamScheduleSegmentParameters parameters = new() { Duration = "120" };

        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamScheduleSegment(session, "141981764", "seg_123", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    public async Task UpdateChannelStreamScheduleSegment_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        UpdateChannelStreamScheduleSegmentParameters parameters = new() { Duration = "120" };

        Func<Task> action = async () => await ChannelSchedule.UpdateChannelStreamScheduleSegment(session, "141981764", "seg_123", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.UpdateChannelStreamScheduleSegment"/> updates segment and returns schedule.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChannelStreamScheduleSegment")]
    public async Task UpdateChannelStreamScheduleSegment_WhenCalledWithValidParameters_ReturnsUpdatedSchedule()
    {
        this._fixture.RegisterResponse("PATCH", "/helix/schedule/segment", @"{
  ""data"": {
    ""segments"": [
      {
        ""id"": ""seg_123"",
        ""start_time"": ""2021-07-01T18:00:00Z"",
        ""end_time"": ""2021-07-01T20:00:00Z"",
        ""title"": ""TwitchDev Monthly Update // July 1, 2021"",
        ""canceled_until"": null,
        ""category"": {
          ""id"": ""509670"",
          ""name"": ""Science & Technology""
        },
        ""is_recurring"": false
      }
    ],
    ""broadcaster_id"": ""141981764"",
    ""broadcaster_name"": ""TwitchDev"",
    ""broadcaster_login"": ""twitchdev"",
    ""vacation"": null
  }
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        UpdateChannelStreamScheduleSegmentParameters parameters = new()
        {
            StartTime = DateTime.Parse("2021-07-01T18:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            Duration = "120",
            CategoryId = "509670",
            Title = "TwitchDev Monthly Update // July 1, 2021",
            IsCanceled = false,
            Timezone = "America/New_York"
        };

        ResponseDataObject<ChannelSchedule>? response = await ChannelSchedule.UpdateChannelStreamScheduleSegment(session, "141981764", "seg_123", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();

        ChannelSchedule schedule = response.Data;
        _ = schedule.BroadcasterId.Should().Be("141981764");
        _ = schedule.BroadcasterName.Should().Be("TwitchDev");
        _ = schedule.BroadcasterLogin.Should().Be("twitchdev");
        _ = schedule.Vacation.Should().BeNull();
        _ = schedule.Segments.Should().NotBeNull();
        _ = schedule.Segments.Count.Should().Be(1);

        ScheduleSegment segment = schedule.Segments[0];
        _ = segment.Id.Should().Be("seg_123");
        _ = segment.StartTime.Should().Be(DateTime.Parse("2021-07-01T18:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = segment.EndTime.Should().Be(DateTime.Parse("2021-07-01T20:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = segment.Title.Should().Be("TwitchDev Monthly Update // July 1, 2021");
        _ = segment.CanceledUntil.Should().BeNull();
        _ = segment.IsRecurring.Should().BeFalse();
        _ = segment.Category.Should().NotBeNull();
        _ = segment.Category.Id.Should().Be("509670");
        _ = segment.Category.Name.Should().Be("Science & Technology");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/schedule/segment");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("id=seg_123");
        _ = request.Body.Should().Contain("\"start_time\"");
        _ = request.Body.Should().Contain("\"duration\":\"120\"");
        _ = request.Body.Should().Contain("\"category_id\":\"509670\"");
        _ = request.Body.Should().Contain("\"title\":\"TwitchDev Monthly Update // July 1, 2021\"");
        _ = request.Body.Should().Contain("\"is_canceled\":false");
        _ = request.Body.Should().Contain("\"timezone\":\"America/New_York\"");
    }

    #endregion UpdateChannelStreamScheduleSegment

    #region DeleteChannelStreamScheduleSegment

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.DeleteChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteChannelStreamScheduleSegment")]
    public async Task DeleteChannelStreamScheduleSegment_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelSchedule.DeleteChannelStreamScheduleSegment(null!, "141981764", "seg_123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.DeleteChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "DeleteChannelStreamScheduleSegment")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteChannelStreamScheduleSegment_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        Func<Task> action = async () => await ChannelSchedule.DeleteChannelStreamScheduleSegment(session, invalidBroadcasterId!, "seg_123").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.DeleteChannelStreamScheduleSegment"/> throws <see cref="ArgumentNullException"/> when segmentId is null or whitespace.
    /// </summary>
    /// <param name="invalidSegmentId">The invalid segment ID to test.</param>
    [Theory]
    [Trait("Member", "DeleteChannelStreamScheduleSegment")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteChannelStreamScheduleSegment_WhenSegmentIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSegmentId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        Func<Task> action = async () => await ChannelSchedule.DeleteChannelStreamScheduleSegment(session, "141981764", invalidSegmentId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("segmentId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.DeleteChannelStreamScheduleSegment"/> throws <see cref="TokenTypeException"/> when session is an App token.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteChannelStreamScheduleSegment")]
    public async Task DeleteChannelStreamScheduleSegment_WhenSessionIsAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ChannelSchedule.DeleteChannelStreamScheduleSegment(session, "141981764", "seg_123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.DeleteChannelStreamScheduleSegment"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteChannelStreamScheduleSegment")]
    public async Task DeleteChannelStreamScheduleSegment_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await ChannelSchedule.DeleteChannelStreamScheduleSegment(session, "141981764", "seg_123").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelSchedule.DeleteChannelStreamScheduleSegment"/> removes segment and returns no content.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteChannelStreamScheduleSegment")]
    public async Task DeleteChannelStreamScheduleSegment_WhenCalledWithValidParameters_ReturnsNoContent()
    {
        this._fixture.RegisterStatusCode("DELETE", "/helix/schedule/segment", StatusCodes.Status204NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageSchedule);
        JsonApiResponse? response = await ChannelSchedule.DeleteChannelStreamScheduleSegment(session, "141981764", "seg_123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/schedule/segment");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("id=seg_123");
    }

    #endregion DeleteChannelStreamScheduleSegment
}


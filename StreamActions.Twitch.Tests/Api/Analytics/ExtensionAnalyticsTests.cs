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
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Analytics;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Analytics;

/// <summary>
/// Provides unit and integration tests for <see cref="ExtensionAnalytics"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ExtensionAnalyticsTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionAnalyticsTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ExtensionAnalyticsTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test execution.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region GetExtensionAnalytics

    /// <summary>
    /// Tests that <see cref="ExtensionAnalytics.GetExtensionAnalytics"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionAnalytics")]
    public async Task GetExtensionAnalytics_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ExtensionAnalytics.GetExtensionAnalytics(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ExtensionAnalytics.GetExtensionAnalytics"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionAnalytics")]
    public async Task GetExtensionAnalytics_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ExtensionAnalytics.GetExtensionAnalytics(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ExtensionAnalytics.GetExtensionAnalytics"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionAnalytics")]
    public async Task GetExtensionAnalytics_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ExtensionAnalytics.GetExtensionAnalytics(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ExtensionAnalytics.GetExtensionAnalytics"/> throws <see cref="InvalidOperationException"/> when startedAt is specified without endedAt.
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionAnalytics")]
    public async Task GetExtensionAnalytics_WhenStartedAtSpecifiedWithoutEndedAt_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.AnalyticsReadExtensions);
        Func<Task> action = async () => await ExtensionAnalytics.GetExtensionAnalytics(session, startedAt: DateTime.UtcNow.AddDays(-30)).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("If startedAt is specified, endedAt must also be specified (and vice-versa)");
    }

    /// <summary>
    /// Tests that <see cref="ExtensionAnalytics.GetExtensionAnalytics"/> throws <see cref="InvalidOperationException"/> when endedAt is specified without startedAt.
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionAnalytics")]
    public async Task GetExtensionAnalytics_WhenEndedAtSpecifiedWithoutStartedAt_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.AnalyticsReadExtensions);
        Func<Task> action = async () => await ExtensionAnalytics.GetExtensionAnalytics(session, endedAt: DateTime.UtcNow.AddDays(-2)).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("If startedAt is specified, endedAt must also be specified (and vice-versa)");
    }

    /// <summary>
    /// Tests that <see cref="ExtensionAnalytics.GetExtensionAnalytics"/> sends a GET request and returns extension analytics data.
    /// </summary>
    [Fact]
    [Trait("Member", "GetExtensionAnalytics")]
    public async Task GetExtensionAnalytics_WhenValidParameters_ReturnsExtensionAnalyticsData()
    {
        DateTime startedAt = new(2018, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime endedAt = new(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        string reportUrl = "https://twitch-piper-reports.s3-us-west-2.amazonaws.com/dynamic/LoL";

        this._fixture.RegisterJsonResponse("GET", "/helix/analytics/extensions", new
        {
            data = new[]
            {
                new
                {
                    extension_id = "efgh",
                    URL = reportUrl,
                    type = "overview_v2",
                    date_range = new
                    {
                        started_at = startedAt,
                        ended_at = endedAt
                    }
                }
            },
            pagination = new
            {
                cursor = "eyJiIjpudWxsLCJhIjp7Ik9mZnNldCI6NX19"
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.AnalyticsReadExtensions);
        ResponseData<ExtensionAnalytics>? response = await ExtensionAnalytics.GetExtensionAnalytics(
            session,
            extensionId: "efgh",
            type: ExtensionAnalytics.ExtensionReportType.OverviewV2,
            startedAt: startedAt,
            endedAt: endedAt,
            first: 20).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ExtensionAnalytics analytics = response.Data[0];
        _ = analytics.ExtensionId.Should().Be("efgh");
        _ = analytics.Url.Should().Be(new Uri(reportUrl));
        _ = analytics.Type.Should().Be(ExtensionAnalytics.ExtensionReportType.OverviewV2);
        _ = analytics.DateRange.Should().NotBeNull();
        _ = analytics.DateRange.StartedAt.Should().Be(startedAt);
        _ = analytics.DateRange.EndedAt.Should().Be(endedAt);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("eyJiIjpudWxsLCJhIjp7Ik9mZnNldCI6NX19");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/analytics/extensions");
        _ = request.QueryString.Should().Contain("extension_id=efgh");
        _ = request.QueryString.Should().Contain("type=overview_v2");
        _ = request.QueryString.Should().Contain("first=20");
    }

    #endregion GetExtensionAnalytics
}

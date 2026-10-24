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
using StreamActions.Twitch.Api.Search;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Search;

/// <summary>
/// Provides unit tests for the <see cref="SearchChannel"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class SearchChannelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchChannelTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public SearchChannelTests(TwitchKestrelServerFixture fixture)
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

    #region SearchChannels

    /// <summary>
    /// Tests that <see cref="SearchChannel.SearchChannels"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SearchChannels")]
    public async Task SearchChannels_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await SearchChannel.SearchChannels(null!, "loserfruit").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="SearchChannel.SearchChannels"/> throws <see cref="ArgumentNullException"/> when query is null or whitespace.
    /// </summary>
    /// <param name="invalidQuery">The invalid query string.</param>
    [Theory]
    [Trait("Member", "SearchChannels")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchChannels_WhenQueryIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidQuery)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await SearchChannel.SearchChannels(session, invalidQuery!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("query");
    }

    /// <summary>
    /// Tests that <see cref="SearchChannel.SearchChannels"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "SearchChannels")]
    public async Task SearchChannels_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await SearchChannel.SearchChannels(session, "loserfruit").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="SearchChannel.SearchChannels"/> returns channels on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "SearchChannels")]
    public async Task SearchChannels_WhenRequestSucceeds_ReturnsChannels()
    {
        this._fixture.RegisterEndpoint("/helix/search/channels", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""broadcaster_language"": ""en"",
      ""broadcaster_login"": ""loserfruit"",
      ""display_name"": ""Loserfruit"",
      ""game_id"": ""488552"",
      ""game_name"": ""Overwatch"",
      ""id"": ""41245072"",
      ""is_live"": false,
      ""tags"": [""English"", ""FPS""],
      ""thumbnail_url"": ""https://static-cdn.jtvnw.net/jtv_user_pictures/loserfruit-profile_image-5262517666380dee-300x300.jpeg"",
      ""title"": ""a stream"",
      ""started_at"": ""2021-06-03T18:00:00Z""
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursor456""
  }
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<SearchChannel>? response = await SearchChannel.SearchChannels(session, "loserfruit", true, 20, "afterCursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        SearchChannel channel = response.Data[0];
        _ = channel.BroadcasterLanguage.Should().Be("en");
        _ = channel.BroadcasterLogin.Should().Be("loserfruit");
        _ = channel.DisplayName.Should().Be("Loserfruit");
        _ = channel.GameId.Should().Be("488552");
        _ = channel.GameName.Should().Be("Overwatch");
        _ = channel.Id.Should().Be("41245072");
        _ = channel.IsLive.Should().BeFalse();
        _ = channel.Tags.Should().ContainInOrder("English", "FPS");
        _ = channel.ThumbnailUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/jtv_user_pictures/loserfruit-profile_image-5262517666380dee-300x300.jpeg"));
        _ = channel.Title.Should().Be("a stream");
        _ = channel.StartedAt.Should().Be(DateTime.Parse("2021-06-03T18:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));

        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor456");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/search/channels");
        _ = request.QueryString.Should().Contain("query=loserfruit");
        _ = request.QueryString.Should().Contain("live_only=true");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=afterCursor");
    }

    #endregion SearchChannels
}

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

using StreamActions.Twitch.Api.Search;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Search;

/// <summary>
/// Provides unit tests for the <see cref="SearchCategory"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class SearchCategoryTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchCategoryTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public SearchCategoryTests(TwitchKestrelServerFixture fixture)
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

    #region SearchCategories

    /// <summary>
    /// Tests that <see cref="SearchCategory.SearchCategories"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "SearchCategories")]
    public async Task SearchCategories_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await SearchCategory.SearchCategories(null!, "fortnite").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="SearchCategory.SearchCategories"/> throws <see cref="ArgumentNullException"/> when query is null or whitespace.
    /// </summary>
    /// <param name="invalidQuery">The invalid query string.</param>
    [Theory]
    [Trait("Member", "SearchCategories")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchCategories_WhenQueryIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidQuery)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await SearchCategory.SearchCategories(session, invalidQuery!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("query");
    }

    /// <summary>
    /// Tests that <see cref="SearchCategory.SearchCategories"/> throws <see cref="TokenTypeException"/> when session token type is None.
    /// </summary>
    [Fact]
    [Trait("Member", "SearchCategories")]
    public async Task SearchCategories_WhenTokenTypeIsNone_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateNoneSession();
        Func<Task> action = async () => await SearchCategory.SearchCategories(session, "fortnite").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="SearchCategory.SearchCategories"/> returns categories on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "SearchCategories")]
    public async Task SearchCategories_WhenRequestSucceeds_ReturnsCategories()
    {
        this._fixture.RegisterEndpoint("/helix/search/categories", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""box_art_url"": ""https://static-cdn.jtvnw.net/ttv-boxart/33214-{width}x{height}.jpg"",
      ""name"": ""Fortnite"",
      ""id"": ""33214""
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursor123""
  }
}");

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<SearchCategory>? response = await SearchCategory.SearchCategories(session, "fortnite", 20, "afterCursor").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        SearchCategory category = response.Data[0];
        _ = category.Id.Should().Be("33214");
        _ = category.Name.Should().Be("Fortnite");
        _ = category.BoxArtUrl.Should().Be(new Uri("https://static-cdn.jtvnw.net/ttv-boxart/33214-{width}x{height}.jpg"));

        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor123");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/search/categories");
        _ = request.QueryString.Should().Contain("query=fortnite");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=afterCursor");
    }

    #endregion SearchCategories
}

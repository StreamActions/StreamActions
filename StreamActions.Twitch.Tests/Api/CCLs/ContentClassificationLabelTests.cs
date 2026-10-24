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
using StreamActions.Twitch.Api.CCLs;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.CCLs;

/// <summary>
/// Provides unit and integration tests for <see cref="ContentClassificationLabel"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ContentClassificationLabelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentClassificationLabelTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ContentClassificationLabelTests(TwitchKestrelServerFixture fixture)
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

    #region GetContentClassificationLabels

    /// <summary>
    /// Tests that <see cref="ContentClassificationLabel.GetContentClassificationLabels"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetContentClassificationLabels")]
    public async Task GetContentClassificationLabels_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ContentClassificationLabel.GetContentClassificationLabels(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ContentClassificationLabel.GetContentClassificationLabels"/> sends a GET request and returns classification labels.
    /// </summary>
    [Fact]
    [Trait("Member", "GetContentClassificationLabels")]
    public async Task GetContentClassificationLabels_WhenValidParameters_ReturnsLabelsList()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/content_classification_labels", new
        {
            data = new[]
            {
                new
                {
                    id = "Gambling",
                    description = "Gambling content",
                    name = "Gambling"
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ContentClassificationLabel>? response = await ContentClassificationLabel.GetContentClassificationLabels(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        ContentClassificationLabel label = response.Data[0];
        _ = label.Id.Should().Be("Gambling");
        _ = label.Description.Should().Be("Gambling content");
        _ = label.Name.Should().Be("Gambling");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/content_classification_labels");
    }

    /// <summary>
    /// Tests that <see cref="ContentClassificationLabel.GetContentClassificationLabels"/> includes the locale query parameter when provided.
    /// </summary>
    [Fact]
    [Trait("Member", "GetContentClassificationLabels")]
    public async Task GetContentClassificationLabels_WhenLocaleProvided_IncludesLocaleInQuery()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/content_classification_labels", new
        {
            data = Array.Empty<object>()
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<ContentClassificationLabel>? response = await ContentClassificationLabel.GetContentClassificationLabels(session, "en-US").ConfigureAwait(true);

        _ = response.Should().NotBeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/content_classification_labels");
        _ = request.QueryString.Should().Contain("locale=en-US");
    }

    #endregion GetContentClassificationLabels
}

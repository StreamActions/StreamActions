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
using StreamActions.Twitch.Api.Clips;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Clips;

/// <summary>
/// Provides unit tests for the <see cref="CreatedClip"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class CreatedClipTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatedClipTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public CreatedClipTests(TwitchKestrelServerFixture fixture)
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

    #region CreateClip

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClip"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateClip")]
    public async Task CreateClip_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await CreatedClip.CreateClip(null!, "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClip"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "CreateClip")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateClip_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ClipsEdit);
        Func<Task> action = async () => await CreatedClip.CreateClip(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClip"/> throws <see cref="ArgumentOutOfRangeException"/> when duration is not between 5 and 60.
    /// </summary>
    /// <param name="invalidDuration">The invalid duration value.</param>
    [Theory]
    [Trait("Member", "CreateClip")]
    [InlineData(4.9f)]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(60.1f)]
    [InlineData(100f)]
    public async Task CreateClip_WhenDurationIsOutOfRange_ThrowsArgumentOutOfRangeException(float invalidDuration)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ClipsEdit);
        Func<Task> action = async () => await CreatedClip.CreateClip(session, "12345", duration: invalidDuration).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("duration");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClip"/> throws <see cref="TwitchScopeMissingException"/> when session is missing required scope.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateClip")]
    public async Task CreateClip_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await CreatedClip.CreateClip(session, "12345").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClip"/> creates a clip successfully with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateClip")]
    public async Task CreateClip_WhenCalledWithValidParameters_ReturnsCreatedClip()
    {
        this._fixture.RegisterJsonResponse("POST", "/helix/clips", new
        {
            data = new[]
            {
                new
                {
                    id = "12345678",
                    edit_url = "https://clips.twitch.tv/12345678/edit"
                }
            }
        }, HttpStatusCode.Accepted);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ClipsEdit);
        ResponseData<CreatedClip>? response = await CreatedClip.CreateClip(session, "12345", title: "Awesome Clip", duration: 30f).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.Accepted);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        CreatedClip clip = response.Data[0];
        _ = clip.Id.Should().Be("12345678");
        _ = clip.EditUrl.Should().Be(new Uri("https://clips.twitch.tv/12345678/edit"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/clips");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["title"].ToString().Should().Be("Awesome Clip");
        _ = request.Query["duration"].ToString().Should().Be("30");
    }

    #endregion CreateClip

    #region CreateClipFromVod

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateClipFromVod")]
    public async Task CreateClipFromVod_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(null!, "12345", "67890", "99999", 120, "My Vod Clip").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "CreateClipFromVod")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateClipFromVod_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(session, invalidId!, "67890", "99999", 120, "My Vod Clip").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="ArgumentNullException"/> when editorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid editor ID.</param>
    [Theory]
    [Trait("Member", "CreateClipFromVod")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateClipFromVod_WhenEditorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(session, "12345", invalidId!, "99999", 120, "My Vod Clip").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("editorId");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="ArgumentNullException"/> when vodId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid VOD ID.</param>
    [Theory]
    [Trait("Member", "CreateClipFromVod")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateClipFromVod_WhenVodIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(session, "12345", "67890", invalidId!, 120, "My Vod Clip").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("vodId");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="ArgumentNullException"/> when title is null or whitespace.
    /// </summary>
    /// <param name="invalidTitle">The invalid clip title.</param>
    [Theory]
    [Trait("Member", "CreateClipFromVod")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateClipFromVod_WhenTitleIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTitle)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(session, "12345", "67890", "99999", 120, invalidTitle!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("title");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="ArgumentOutOfRangeException"/> when duration is not between 5 and 60.
    /// </summary>
    /// <param name="invalidDuration">The invalid duration value.</param>
    [Theory]
    [Trait("Member", "CreateClipFromVod")]
    [InlineData(4.9f)]
    [InlineData(0f)]
    [InlineData(-10f)]
    [InlineData(60.1f)]
    [InlineData(120f)]
    public async Task CreateClipFromVod_WhenDurationIsOutOfRange_ThrowsArgumentOutOfRangeException(float invalidDuration)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(session, "12345", "67890", "99999", 120, "My Vod Clip", duration: invalidDuration).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("duration");
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> throws <see cref="TwitchScopeMissingException"/> when session is missing required scope.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateClipFromVod")]
    public async Task CreateClipFromVod_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await CreatedClip.CreateClipFromVod(session, "12345", "67890", "99999", 120, "My Vod Clip").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="CreatedClip.CreateClipFromVod"/> creates a clip from VOD successfully with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateClipFromVod")]
    public async Task CreateClipFromVod_WhenCalledWithValidParameters_ReturnsCreatedClip()
    {
        this._fixture.RegisterJsonResponse("POST", "/helix/videos/clips", new
        {
            data = new[]
            {
                new
                {
                    id = "vodclip999",
                    edit_url = "https://clips.twitch.tv/vodclip999/edit"
                }
            }
        }, HttpStatusCode.Accepted);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.EditorManageClips);
        ResponseData<CreatedClip>? response = await CreatedClip.CreateClipFromVod(session, "12345", "67890", "99999", 120, "My Vod Clip", duration: 45f).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.Accepted);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        CreatedClip clip = response.Data[0];
        _ = clip.Id.Should().Be("vodclip999");
        _ = clip.EditUrl.Should().Be(new Uri("https://clips.twitch.tv/vodclip999/edit"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/videos/clips");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["editor_id"].ToString().Should().Be("67890");
        _ = request.Query["vod_id"].ToString().Should().Be("99999");
        _ = request.Query["vod_offset"].ToString().Should().Be("120");
        _ = request.Query["title"].ToString().Should().Be("My Vod Clip");
        _ = request.Query["duration"].ToString().Should().Be("45");
    }

    #endregion CreateClipFromVod
}

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

using StreamActions.Twitch.Api.Clips;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Clips;

/// <summary>
/// Provides unit tests for the <see cref="ClipDownload"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ClipDownloadTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipDownloadTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ClipDownloadTests(TwitchKestrelServerFixture fixture)
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

    #region GetClipsDownload

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ClipDownload.GetClipsDownload(null!, "12345", "67890", ["clip1"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetClipsDownload")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetClipsDownload_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await ClipDownload.GetClipsDownload(session, invalidId!, "67890", ["clip1"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> throws <see cref="ArgumentNullException"/> when editorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid editor ID.</param>
    [Theory]
    [Trait("Member", "GetClipsDownload")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetClipsDownload_WhenEditorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> action = async () => await ClipDownload.GetClipsDownload(session, "12345", invalidId!, ["clip1"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("editorId");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> throws <see cref="ArgumentNullException"/> when clipId is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenClipIdIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        Func<Task> actionNull = async () => await ClipDownload.GetClipsDownload(session, "12345", "67890", null!).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("clipId");

        Func<Task> actionEmpty = async () => await ClipDownload.GetClipsDownload(session, "12345", "67890", []).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("clipId");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> throws <see cref="ArgumentOutOfRangeException"/> when clipId has more than 10 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenClipIdCountExceeds10_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        List<string> clipIds = [.. Enumerable.Range(1, 11).Select(i => $"clip{i}")];
        Func<Task> action = async () => await ClipDownload.GetClipsDownload(session, "12345", "67890", clipIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("clipId");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ClipDownload.GetClipsDownload(session, "12345", "67890", ["clip1"]).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> retrieves clip download URLs.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenCalledWithValidParameters_ReturnsClipDownloads()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/clips/downloads", new
        {
            data = new[]
            {
                new
                {
                    clip_id = "1234",
                    landscape_download_url = "https://clips-media-assets.twitch.tv/1234.mp4",
                    portrait_download_url = "https://clips-media-assets.twitch.tv/1234_portrait.mp4"
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        ResponseData<ClipDownload>? response = await ClipDownload.GetClipsDownload(session, "12345", "67890", ["1234"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ClipDownload download = response.Data[0];
        _ = download.ClipId.Should().Be("1234");
        _ = download.LandscapeDownloadUrl.Should().Be(new Uri("https://clips-media-assets.twitch.tv/1234.mp4"));
        _ = download.PortraitDownloadUrl.Should().Be(new Uri("https://clips-media-assets.twitch.tv/1234_portrait.mp4"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/clips/downloads");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["editor_id"].ToString().Should().Be("67890");
        _ = request.Query["clip_id"].ToString().Should().Be("1234");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> sends multiple clip IDs in the query string.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenCalledWithMultipleClipIds_SendsAllIdsInQuery()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/clips/downloads", new
        {
            data = new[]
            {
                new
                {
                    clip_id = "clip1",
                    landscape_download_url = "https://clips-media-assets.twitch.tv/clip1.mp4",
                    portrait_download_url = (string?)null
                },
                new
                {
                    clip_id = "clip2",
                    landscape_download_url = "https://clips-media-assets.twitch.tv/clip2.mp4",
                    portrait_download_url = (string?)"https://clips-media-assets.twitch.tv/clip2_p.mp4"
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageClips);
        ResponseData<ClipDownload>? response = await ClipDownload.GetClipsDownload(session, "12345", "67890", ["clip1", "clip2"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(2);
        _ = response.Data[0].ClipId.Should().Be("clip1");
        _ = response.Data[0].LandscapeDownloadUrl.Should().Be(new Uri("https://clips-media-assets.twitch.tv/clip1.mp4"));
        _ = response.Data[0].PortraitDownloadUrl.Should().BeNull();
        _ = response.Data[1].ClipId.Should().Be("clip2");
        _ = response.Data[1].LandscapeDownloadUrl.Should().Be(new Uri("https://clips-media-assets.twitch.tv/clip2.mp4"));
        _ = response.Data[1].PortraitDownloadUrl.Should().Be(new Uri("https://clips-media-assets.twitch.tv/clip2_p.mp4"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Query["clip_id"].ToString().Should().Contain("clip1");
        _ = request.Query["clip_id"].ToString().Should().Contain("clip2");
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> succeeds when using the EditorManageClips scope.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenCalledWithEditorManageClipsScope_ReturnsClipDownloads()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/clips/downloads", new
        {
            data = new[]
            {
                new
                {
                    clip_id = "1234",
                    landscape_download_url = "https://clips-media-assets.twitch.tv/1234.mp4",
                    portrait_download_url = (string?)null
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.EditorManageClips);
        ResponseData<ClipDownload>? response = await ClipDownload.GetClipsDownload(session, "12345", "67890", ["1234"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Tests that <see cref="ClipDownload.GetClipsDownload"/> succeeds when called with an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetClipsDownload")]
    public async Task GetClipsDownload_WhenCalledWithAppToken_ReturnsClipDownloads()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/clips/downloads", new
        {
            data = new[]
            {
                new
                {
                    clip_id = "1234",
                    landscape_download_url = "https://clips-media-assets.twitch.tv/1234.mp4",
                    portrait_download_url = (string?)null
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManageClips);
        ResponseData<ClipDownload>? response = await ClipDownload.GetClipsDownload(session, "12345", "67890", ["1234"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
    }

    #endregion GetClipsDownload
}

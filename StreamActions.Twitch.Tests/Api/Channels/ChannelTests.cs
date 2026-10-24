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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Channels.Objects;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Channels;

/// <summary>
/// Provides unit and integration tests for <see cref="Channel"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChannelTests(TwitchKestrelServerFixture fixture)
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

    #region GetChannelInformation

    /// <summary>
    /// Tests that <see cref="Channel.GetChannelInformation"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelInformation")]
    public async Task GetChannelInformation_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Channel.GetChannelInformation(null!, ["141981764"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Channel.GetChannelInformation"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelInformation")]
    public async Task GetChannelInformation_WhenBroadcasterIdIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Channel.GetChannelInformation(session, []).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Channel.GetChannelInformation"/> throws <see cref="ArgumentOutOfRangeException"/> when broadcasterId has more than 100 elements.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelInformation")]
    public async Task GetChannelInformation_WhenBroadcasterIdCountExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<string> ids = Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
        Func<Task> action = async () => await Channel.GetChannelInformation(session, ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Channel.GetChannelInformation"/> sends a GET request and returns channel information.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelInformation")]
    public async Task GetChannelInformation_WhenValidParameters_ReturnsChannelInformation()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/channels", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_id = "141981764",
                    broadcaster_login = "twitchdev",
                    broadcaster_name = "TwitchDev",
                    broadcaster_language = "en",
                    game_id = "509670",
                    game_name = "Science & Technology",
                    title = "TwitchDev Monthly Update",
                    delay = 0,
                    tags = new[] { "DevsInTheKnow" },
                    content_classification_labels = new[] { "Gambling", "DrugsIntoxication" },
                    is_branded_content = false
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Channel>? response = await Channel.GetChannelInformation(session, ["141981764"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Channel channel = response.Data[0];
        _ = channel.BroadcasterId.Should().Be("141981764");
        _ = channel.BroadcasterLogin.Should().Be("twitchdev");
        _ = channel.BroadcasterName.Should().Be("TwitchDev");
        _ = channel.BroadcasterLanguage.Should().Be("en");
        _ = channel.GameId.Should().Be("509670");
        _ = channel.GameName.Should().Be("Science & Technology");
        _ = channel.Title.Should().Be("TwitchDev Monthly Update");
        _ = channel.Delay.Should().Be(0);
        _ = channel.IsBrandedContent.Should().BeFalse();
        _ = channel.Tags.Should().ContainSingle().Which.Should().Be("DevsInTheKnow");
        _ = channel.ContentClassificationLabels.Should().HaveCount(2);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channels");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    #endregion GetChannelInformation

    #region ModifyChannelInformation

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ModifyChannelParameters parameters = new() { Title = "New Title" };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(null!, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "ModifyChannelInformation")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ModifyChannelInformation_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Title = "New Title" };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, invalidId!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when title is empty or whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenTitleIsWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Title = "   " };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentOutOfRangeException"/> when tags count exceeds 10.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenTagsCountExceeds10_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        List<string> tags = Enumerable.Range(1, 11).Select(i => $"Tag{i}").ToList();
        ModifyChannelParameters parameters = new() { Tags = tags };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Tags");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ModifyChannelParameters parameters = new() { Title = "New Title" };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        ModifyChannelParameters parameters = new() { Title = "New Title" };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> sends a PATCH request and returns 204 No Content.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenValidParameters_PatchesChannelAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/channels", (int)HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new()
        {
            Title = "Updated Stream Title",
            BroadcasterLanguage = "en",
            GameId = "509670"
        };

        JsonApiResponse? response = await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/channels");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.Body.Should().Contain("\"title\":\"Updated Stream Title\"");
    }

    #endregion ModifyChannelInformation
}

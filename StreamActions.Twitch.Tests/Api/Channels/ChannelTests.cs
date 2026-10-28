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
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Channels.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Channels;

/// <summary>
/// Provides unit and integration tests for <see cref="Channel"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// Sample tags for channel information response.
    /// </summary>
    private static readonly string[] _sampleTags = ["DevsInTheKnow"];

    /// <summary>
    /// Sample content classification labels for channel information response.
    /// </summary>
    private static readonly string[] _sampleContentClassificationLabels = ["Gambling", "DrugsIntoxication"];

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
    public void Dispose() => this._fixture.Reset();

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
        List<string> ids = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];
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
                    tags = _sampleTags,
                    content_classification_labels = _sampleContentClassificationLabels,
                    is_branded_content = false
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Channel>? response = await Channel.GetChannelInformation(session, ["141981764", "999999"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

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
        _ = channel.ContentClassificationLabels.Should().NotBeNull();
        _ = channel.ContentClassificationLabels.Should().ContainInOrder(ContentClassificationLabel.LabelId.Gambling, ContentClassificationLabel.LabelId.DrugsIntoxication);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channels");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("broadcaster_id=999999");
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
        List<string> tags = [.. Enumerable.Range(1, 11).Select(i => $"Tag{i}")];
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
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> sends a PATCH request with all properties and returns 204 No Content.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenValidParameters_PatchesChannelAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/channels", (int)HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new()
        {
            GameId = "509670",
            BroadcasterLanguage = "en",
            Title = "Updated Stream Title",
            Delay = 60,
            Tags = ["DevsInTheKnow"],
            ContentClassificationLabels = [new() { Id = ContentClassificationLabel.LabelId.Gambling, IsEnabled = true }],
            IsBrandedContent = false
        };

        JsonApiResponse? response = await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/channels");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.Body.Should().Contain("\"game_id\":\"509670\"");
        _ = request.Body.Should().Contain("\"broadcaster_language\":\"en\"");
        _ = request.Body.Should().Contain("\"title\":\"Updated Stream Title\"");
        _ = request.Body.Should().Contain("\"delay\":60");
        _ = request.Body.Should().Contain("\"tags\":[\"DevsInTheKnow\"]");
        _ = request.Body.Should().Contain("\"id\":\"Gambling\"");
        _ = request.Body.Should().Contain("\"is_enabled\":true");
        _ = request.Body.Should().Contain("\"is_branded_content\":false");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> omits null properties from the request body when only a single property is set.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenOnlySinglePropertySet_OmitsOtherFieldsFromBody()
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/channels", (int)HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Title = "Only Title" };

        JsonApiResponse? response = await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("\"title\":\"Only Title\"");
        _ = request.Body.Should().NotContain("game_id");
        _ = request.Body.Should().NotContain("broadcaster_language");
        _ = request.Body.Should().NotContain("delay");
        _ = request.Body.Should().NotContain("tags");
        _ = request.Body.Should().NotContain("content_classification_labels");
        _ = request.Body.Should().NotContain("is_branded_content");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> clamps the delay parameter between 0 and 900.
    /// </summary>
    /// <param name="inputDelay">The input delay to test.</param>
    /// <param name="expectedDelay">The expected clamped delay.</param>
    [Theory]
    [Trait("Member", "ModifyChannelInformation")]
    [InlineData(-10, 0)]
    [InlineData(-1, 0)]
    [InlineData(901, 900)]
    [InlineData(1200, 900)]
    public async Task ModifyChannelInformation_WhenDelayOutOfRange_ClampsDelay(int inputDelay, int expectedDelay)
    {
        this._fixture.RegisterStatusCode("PATCH", "/helix/channels", (int)HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Delay = inputDelay };
        _ = await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain($"\"delay\":{expectedDelay}");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when a tag is null, empty, or whitespace.
    /// </summary>
    /// <param name="invalidTag">The invalid tag to test.</param>
    [Theory]
    [Trait("Member", "ModifyChannelInformation")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ModifyChannelInformation_WhenTagIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTag)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Tags = [invalidTag!] };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Tags");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentOutOfRangeException"/> when a tag contains whitespace or special characters.
    /// </summary>
    /// <param name="invalidTag">The invalid tag containing whitespace or special characters.</param>
    [Theory]
    [Trait("Member", "ModifyChannelInformation")]
    [InlineData("Tag With Space")]
    [InlineData("Tag#Special")]
    [InlineData("Tag!")]
    public async Task ModifyChannelInformation_WhenTagContainsInvalidCharacters_ThrowsArgumentOutOfRangeException(string invalidTag)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Tags = [invalidTag] };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Tags");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentOutOfRangeException"/> when a tag exceeds 25 characters.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenTagExceeds25Characters_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { Tags = [new string('a', 26)] };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Tags");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when a content classification label element is null.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenContentClassificationLabelElementIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { ContentClassificationLabels = [null!] };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ContentClassificationLabels");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when a content classification label has a null Id.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenContentClassificationLabelIdIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { ContentClassificationLabels = [new() { Id = null, IsEnabled = true }] };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");
    }

    /// <summary>
    /// Tests that <see cref="Channel.ModifyChannelInformation"/> throws <see cref="ArgumentNullException"/> when a content classification label has a null IsEnabled.
    /// </summary>
    [Fact]
    [Trait("Member", "ModifyChannelInformation")]
    public async Task ModifyChannelInformation_WhenContentClassificationLabelIsEnabledIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManageBroadcast);
        ModifyChannelParameters parameters = new() { ContentClassificationLabels = [new() { Id = ContentClassificationLabel.LabelId.Gambling, IsEnabled = null }] };
        Func<Task> action = async () => await Channel.ModifyChannelInformation(session, "141981764", parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("IsEnabled");
    }

    #endregion ModifyChannelInformation
}

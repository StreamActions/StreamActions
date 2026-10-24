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

using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Channels;

/// <summary>
/// Provides unit and integration tests for <see cref="ChannelEditor"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChannelEditorTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelEditorTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChannelEditorTests(TwitchKestrelServerFixture fixture)
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

    #region GetChannelEditors

    /// <summary>
    /// Tests that <see cref="ChannelEditor.GetChannelEditors"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelEditors")]
    public async Task GetChannelEditors_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChannelEditor.GetChannelEditors(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChannelEditor.GetChannelEditors"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetChannelEditors")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelEditors_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadEditors);
        Func<Task> action = async () => await ChannelEditor.GetChannelEditors(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChannelEditor.GetChannelEditors"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelEditors")]
    public async Task GetChannelEditors_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ChannelEditor.GetChannelEditors(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelEditor.GetChannelEditors"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelEditors")]
    public async Task GetChannelEditors_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ChannelEditor.GetChannelEditors(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChannelEditor.GetChannelEditors"/> sends a GET request and returns the list of editors.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelEditors")]
    public async Task GetChannelEditors_WhenValidParameters_ReturnsEditorsList()
    {
        DateTime createdAt = new(2019, 2, 15, 21, 19, 50, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/channels/editors", new
        {
            data = new[]
            {
                new
                {
                    user_id = "1828911",
                    user_name = "mauerbac",
                    created_at = createdAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadEditors);
        ResponseData<ChannelEditor>? response = await ChannelEditor.GetChannelEditors(session, "141981764").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChannelEditor editor = response.Data[0];
        _ = editor.UserId.Should().Be("1828911");
        _ = editor.UserName.Should().Be("mauerbac");
        _ = editor.CreatedAt.Should().Be(createdAt);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/channels/editors");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
    }

    #endregion GetChannelEditors
}

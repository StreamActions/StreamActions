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
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="ChatSettings"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChatSettingsTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSettingsTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChatSettingsTests(TwitchKestrelServerFixture fixture)
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

    #region GetChatSettings

    /// <summary>
    /// Tests that <see cref="ChatSettings.GetChatSettings"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChatSettings")]
    public async Task GetChatSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChatSettings.GetChatSettings(null!, "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.GetChatSettings"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetChatSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChatSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatSettings);
        Func<Task> action = async () => await ChatSettings.GetChatSettings(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.GetChatSettings"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChatSettings")]
    public async Task GetChatSettings_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ChatSettings.GetChatSettings(session, "12345").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.GetChatSettings"/> correctly retrieves and maps chat settings.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChatSettings")]
    public async Task GetChatSettings_WhenCalledWithValidParameters_ReturnsChatSettings()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/settings", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_id = "12345",
                    moderator_id = "67890",
                    slow_mode = true,
                    slow_mode_wait_time = 30,
                    follower_mode = true,
                    follower_mode_duration = 15,
                    subscriber_mode = false,
                    emote_mode = false,
                    unique_chat_mode = true,
                    non_moderator_chat_delay = true,
                    non_moderator_chat_delay_duration = 2
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorReadChatSettings);
        ResponseData<ChatSettings>? response = await ChatSettings.GetChatSettings(session, "12345", "67890").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        ChatSettings settings = response.Data[0];
        _ = settings.BroadcasterId.Should().Be("12345");
        _ = settings.ModeratorId.Should().Be("67890");
        _ = settings.SlowMode.Should().BeTrue();
        _ = settings.SlowModeWaitTime.Should().Be(30);
        _ = settings.FollowerMode.Should().BeTrue();
        _ = settings.FollowerModeDuration.Should().Be(15);
        _ = settings.SubscriberMode.Should().BeFalse();
        _ = settings.EmoteMode.Should().BeFalse();
        _ = settings.UniqueChatMode.Should().BeTrue();
        _ = settings.NonModeratorChatDelay.Should().BeTrue();
        _ = settings.NonModeratorChatDelayDuration.Should().Be(2);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/settings");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
    }

    #endregion GetChatSettings

    #region UpdateChatSettings

    /// <summary>
    /// Tests that <see cref="ChatSettings.UpdateChatSettings"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChatSettings")]
    public async Task UpdateChatSettings_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ChatSettings settings = new() { SlowMode = true };
        Func<Task> action = async () => await ChatSettings.UpdateChatSettings(null!, "12345", "67890", settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.UpdateChatSettings"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "UpdateChatSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateChatSettings_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatSettings);
        ChatSettings settings = new() { SlowMode = true };
        Func<Task> action = async () => await ChatSettings.UpdateChatSettings(session, invalidId!, "67890", settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.UpdateChatSettings"/> throws <see cref="ArgumentNullException"/> when moderatorId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid moderator ID.</param>
    [Theory]
    [Trait("Member", "UpdateChatSettings")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateChatSettings_WhenModeratorIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatSettings);
        ChatSettings settings = new() { SlowMode = true };
        Func<Task> action = async () => await ChatSettings.UpdateChatSettings(session, "12345", invalidId!, settings).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("moderatorId");
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.UpdateChatSettings"/> throws <see cref="ArgumentNullException"/> when settings is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChatSettings")]
    public async Task UpdateChatSettings_WhenSettingsIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatSettings);
        Func<Task> action = async () => await ChatSettings.UpdateChatSettings(session, "12345", "67890", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("settings");
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.UpdateChatSettings"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChatSettings")]
    public async Task UpdateChatSettings_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        ChatSettings settings = new() { SlowMode = true };
        Func<Task> action = async () => await ChatSettings.UpdateChatSettings(session, "12345", "67890", settings).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChatSettings.UpdateChatSettings"/> sends PATCH request and returns updated settings.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateChatSettings")]
    public async Task UpdateChatSettings_WhenCalledWithValidParameters_ReturnsUpdatedSettings()
    {
        this._fixture.RegisterJsonResponse("PATCH", "/helix/chat/settings", new
        {
            data = new[]
            {
                new
                {
                    broadcaster_id = "12345",
                    moderator_id = "67890",
                    slow_mode = true,
                    slow_mode_wait_time = 10,
                    follower_mode = false,
                    follower_mode_duration = (int?)null,
                    subscriber_mode = false,
                    emote_mode = false,
                    unique_chat_mode = false,
                    non_moderator_chat_delay = false,
                    non_moderator_chat_delay_duration = (int?)null
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ModeratorManageChatSettings);
        ChatSettings updateRequest = new()
        {
            SlowMode = true,
            SlowModeWaitTime = 10
        };

        ResponseData<ChatSettings>? response = await ChatSettings.UpdateChatSettings(session, "12345", "67890", updateRequest).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        ChatSettings updated = response.Data[0];
        _ = updated.SlowMode.Should().BeTrue();
        _ = updated.SlowModeWaitTime.Should().Be(10);
        _ = updated.FollowerMode.Should().BeFalse();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/chat/settings");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
        _ = request.Query["moderator_id"].ToString().Should().Be("67890");
        _ = request.Body.Should().Contain("\"slow_mode\":true");
        _ = request.Body.Should().Contain("\"slow_mode_wait_time\":10");
    }

    #endregion UpdateChatSettings
}

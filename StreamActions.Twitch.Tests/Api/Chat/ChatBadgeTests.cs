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

using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Chat.Objects;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="ChatBadge"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChatBadgeTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatBadgeTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChatBadgeTests(TwitchKestrelServerFixture fixture)
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

    #region GetChannelChatBadges

    /// <summary>
    /// Tests that <see cref="ChatBadge.GetChannelChatBadges"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelChatBadges")]
    public async Task GetChannelChatBadges_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChatBadge.GetChannelChatBadges(null!, "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChatBadge.GetChannelChatBadges"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID to test.</param>
    [Theory]
    [Trait("Member", "GetChannelChatBadges")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChannelChatBadges_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await ChatBadge.GetChannelChatBadges(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="ChatBadge.GetChannelChatBadges"/> correctly retrieves and maps channel badges.
    /// </summary>
    [Fact]
    [Trait("Member", "GetChannelChatBadges")]
    public async Task GetChannelChatBadges_WhenCalledWithValidParameters_ReturnsBadges()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/badges", new
        {
            data = new[]
            {
                new
                {
                    set_id = "subscriber",
                    versions = new[]
                    {
                        new
                        {
                            id = "0",
                            image_url_1x = "https://static-cdn.jtvnw.net/badges/v1/sub-0-1.png",
                            image_url_2x = "https://static-cdn.jtvnw.net/badges/v1/sub-0-2.png",
                            image_url_4x = "https://static-cdn.jtvnw.net/badges/v1/sub-0-4.png",
                            title = "Subscriber",
                            description = "Subscriber badge",
                            click_action = "visit_url",
                            click_url = "https://twitch.tv/subscriber"
                        }
                    }
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<ChatBadge>? response = await ChatBadge.GetChannelChatBadges(session, "12345").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChatBadge badge = response.Data[0];
        _ = badge.SetId.Should().Be("subscriber");
        _ = badge.Versions.Should().NotBeNull();
        _ = badge.Versions.Count.Should().Be(1);

        ChatBadgeVersion version = badge.Versions[0];
        _ = version.Id.Should().Be("0");
        _ = version.ImageUrl1x.Should().Be(new Uri("https://static-cdn.jtvnw.net/badges/v1/sub-0-1.png"));
        _ = version.ImageUrl2x.Should().Be(new Uri("https://static-cdn.jtvnw.net/badges/v1/sub-0-2.png"));
        _ = version.ImageUrl4x.Should().Be(new Uri("https://static-cdn.jtvnw.net/badges/v1/sub-0-4.png"));
        _ = version.Title.Should().Be("Subscriber");
        _ = version.Description.Should().Be("Subscriber badge");
        _ = version.ClickAction.Should().Be("visit_url");
        _ = version.ClickUrl.Should().Be(new Uri("https://twitch.tv/subscriber"));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/badges");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
    }

    #endregion GetChannelChatBadges

    #region GetGlobalChatBadges

    /// <summary>
    /// Tests that <see cref="ChatBadge.GetGlobalChatBadges"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetGlobalChatBadges")]
    public async Task GetGlobalChatBadges_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChatBadge.GetGlobalChatBadges(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChatBadge.GetGlobalChatBadges"/> correctly retrieves and maps global badges.
    /// </summary>
    [Fact]
    [Trait("Member", "GetGlobalChatBadges")]
    public async Task GetGlobalChatBadges_WhenCalledWithValidParameters_ReturnsGlobalBadges()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/badges/global", new
        {
            data = new[]
            {
                new
                {
                    set_id = "vip",
                    versions = new[]
                    {
                        new
                        {
                            id = "1",
                            image_url_1x = "https://static-cdn.jtvnw.net/badges/v1/vip-1.png",
                            image_url_2x = "https://static-cdn.jtvnw.net/badges/v1/vip-2.png",
                            image_url_4x = "https://static-cdn.jtvnw.net/badges/v1/vip-4.png",
                            title = "VIP",
                            description = "VIP badge",
                            click_action = (string?)null,
                            click_url = (string?)null
                        }
                    }
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<ChatBadge>? response = await ChatBadge.GetGlobalChatBadges(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ChatBadge badge = response.Data[0];
        _ = badge.SetId.Should().Be("vip");
        _ = badge.Versions.Should().NotBeNull();
        _ = badge.Versions.Count.Should().Be(1);

        ChatBadgeVersion version = badge.Versions[0];
        _ = version.Id.Should().Be("1");
        _ = version.ImageUrl1x.Should().Be(new Uri("https://static-cdn.jtvnw.net/badges/v1/vip-1.png"));
        _ = version.ImageUrl2x.Should().Be(new Uri("https://static-cdn.jtvnw.net/badges/v1/vip-2.png"));
        _ = version.ImageUrl4x.Should().Be(new Uri("https://static-cdn.jtvnw.net/badges/v1/vip-4.png"));
        _ = version.Title.Should().Be("VIP");
        _ = version.Description.Should().Be("VIP badge");
        _ = version.ClickAction.Should().BeNull();
        _ = version.ClickUrl.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/badges/global");
    }

    #endregion GetGlobalChatBadges
}

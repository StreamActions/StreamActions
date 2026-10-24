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
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Drawing;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="ChatColor"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ChatColorTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatColorTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ChatColorTests(TwitchKestrelServerFixture fixture)
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

    #region GetUserChatColor

    /// <summary>
    /// Tests that <see cref="ChatColor.GetUserChatColor"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserChatColor")]
    public async Task GetUserChatColor_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChatColor.GetUserChatColor(null!, ["12345"]).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.GetUserChatColor"/> throws <see cref="ArgumentNullException"/> when userId is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserChatColor")]
    public async Task GetUserChatColor_WhenUserIdIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> actionNull = async () => await ChatColor.GetUserChatColor(session, null!).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");

        Func<Task> actionEmpty = async () => await ChatColor.GetUserChatColor(session, []).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.GetUserChatColor"/> throws <see cref="ArgumentOutOfRangeException"/> when userId has more than 100 elements.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserChatColor")]
    public async Task GetUserChatColor_WhenUserIdCountExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        List<string> userIds = [.. Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture))];
        Func<Task> action = async () => await ChatColor.GetUserChatColor(session, userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.GetUserChatColor"/> returns colors and maps properties correctly.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserChatColor")]
    public async Task GetUserChatColor_WhenCalledWithValidParameters_ReturnsChatColors()
    {
        this._fixture.RegisterJsonResponse("GET", "/helix/chat/color", new
        {
            data = new[]
            {
                new
                {
                    user_id = "11111",
                    user_name = "UserOne",
                    user_login = "userone",
                    color = "#0000FF"
                },
                new
                {
                    user_id = "22222",
                    user_name = "UserTwo",
                    user_login = "usertwo",
                    color = ""
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<ChatColor>? response = await ChatColor.GetUserChatColor(session, ["11111", "22222"]).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(2);

        ChatColor color1 = response.Data[0];
        _ = color1.UserId.Should().Be("11111");
        _ = color1.UserName.Should().Be("UserOne");
        _ = color1.UserLogin.Should().Be("userone");
        _ = color1.ColorString.Should().Be("#0000FF");
        _ = color1.Color.Should().Be(Color.FromArgb(0, 0, 255));

        ChatColor color2 = response.Data[1];
        _ = color2.ColorString.Should().BeEmpty();
        _ = color2.Color.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/chat/color");
        _ = request.Query["user_id"].ToString().Should().Contain("11111");
    }

    #endregion GetUserChatColor

    #region UpdateUserChatColor

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserChatColor")]
    public async Task UpdateUserChatColor_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ChatColor.UpdateUserChatColor(null!, "12345", ChatColor.NamedColor.Blue).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> throws <see cref="ArgumentNullException"/> when userId is whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid user ID.</param>
    [Theory]
    [Trait("Member", "UpdateUserChatColor")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateUserChatColor_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageChatColor);
        Func<Task> action = async () => await ChatColor.UpdateUserChatColor(session, invalidId!, ChatColor.NamedColor.Blue).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> throws <see cref="ArgumentNullException"/> when color is Custom and hex is null or whitespace.
    /// </summary>
    /// <param name="invalidHex">The invalid hex string.</param>
    [Theory]
    [Trait("Member", "UpdateUserChatColor")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateUserChatColor_WhenCustomColorAndHexIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidHex)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageChatColor);
        Func<Task> action = async () => await ChatColor.UpdateUserChatColor(session, "12345", ChatColor.NamedColor.Custom, invalidHex).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("hex");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> throws <see cref="ArgumentOutOfRangeException"/> when hex is not a valid hex string.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserChatColor")]
    public async Task UpdateUserChatColor_WhenCustomColorAndHexIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageChatColor);
        Func<Task> action = async () => await ChatColor.UpdateUserChatColor(session, "12345", ChatColor.NamedColor.Custom, "not_a_hex").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("hex");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> throws <see cref="TokenTypeException"/> when using an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserChatColor")]
    public async Task UpdateUserChatColor_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ChatColor.UpdateUserChatColor(session, "12345", ChatColor.NamedColor.Blue).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> throws <see cref="TwitchScopeMissingException"/> when required scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserChatColor")]
    public async Task UpdateUserChatColor_WhenSessionMissingScope_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Func<Task> action = async () => await ChatColor.UpdateUserChatColor(session, "12345", ChatColor.NamedColor.Blue).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> sends PUT request and returns 204 No Content for a named color.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserChatColor")]
    public async Task UpdateUserChatColor_WhenValidNamedColor_SendsPutRequestAndReturnsSuccess()
    {
        this._fixture.RegisterStatusCode("PUT", "/helix/chat/color", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageChatColor);
        JsonApiResponse? response = await ChatColor.UpdateUserChatColor(session, "12345", ChatColor.NamedColor.BlueViolet).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);
        _ = response.IsSuccessStatusCode.Should().BeTrue();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PUT");
        _ = request.Path.Should().Be("/helix/chat/color");
        _ = request.Query["user_id"].ToString().Should().Be("12345");
        _ = request.Query["color"].ToString().Should().Be("blue_violet");
    }

    /// <summary>
    /// Tests that <see cref="ChatColor.UpdateUserChatColor"/> sends PUT request with custom hex value.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateUserChatColor")]
    public async Task UpdateUserChatColor_WhenValidCustomHexColor_SendsPutRequestWithHex()
    {
        this._fixture.RegisterStatusCode("PUT", "/helix/chat/color", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserManageChatColor);
        JsonApiResponse? response = await ChatColor.UpdateUserChatColor(session, "12345", ChatColor.NamedColor.Custom, "#1E90FF").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Query["color"].ToString().Should().Be("#1E90FF");
    }

    #endregion UpdateUserChatColor
}

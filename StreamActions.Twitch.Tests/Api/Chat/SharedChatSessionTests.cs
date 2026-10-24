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
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Chat;

/// <summary>
/// Provides unit tests for the <see cref="SharedChatSession"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class SharedChatSessionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedChatSessionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public SharedChatSessionTests(TwitchKestrelServerFixture fixture)
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

    #region GetSharedChatSession

    /// <summary>
    /// Tests that <see cref="SharedChatSession.GetSharedChatSession"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetSharedChatSession")]
    public async Task GetSharedChatSession_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await SharedChatSession.GetSharedChatSession(null!, "12345").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="SharedChatSession.GetSharedChatSession"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetSharedChatSession")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSharedChatSession_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await SharedChatSession.GetSharedChatSession(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="SharedChatSession.GetSharedChatSession"/> retrieves shared chat session and maps properties.
    /// </summary>
    [Fact]
    [Trait("Member", "GetSharedChatSession")]
    public async Task GetSharedChatSession_WhenCalledWithValidParameters_ReturnsSharedChatSession()
    {
        Guid sessionId = Guid.Parse("1f855e8f-79d3-4927-b50a-e57ae4268e21");
        DateTime createdAt = DateTime.UtcNow.AddHours(-1);
        DateTime updatedAt = DateTime.UtcNow;

        this._fixture.RegisterJsonResponse("GET", "/helix/shared_chat/session", new
        {
            data = new[]
            {
                new
                {
                    session_id = sessionId,
                    host_broadcaster_id = "12345",
                    participants = new[]
                    {
                        new { broadcaster_id = "12345" },
                        new { broadcaster_id = "67890" }
                    },
                    created_at = createdAt,
                    updated_at = updatedAt
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ResponseData<SharedChatSession>? response = await SharedChatSession.GetSharedChatSession(session, "12345").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        SharedChatSession chatSession = response.Data[0];
        _ = chatSession.SessionId.Should().Be(sessionId);
        _ = chatSession.HostBroadcasterId.Should().Be("12345");
        _ = chatSession.Participants.Should().NotBeNull();
        _ = chatSession.Participants.Count.Should().Be(2);
        _ = chatSession.Participants[0].BroadcasterId.Should().Be("12345");
        _ = chatSession.Participants[1].BroadcasterId.Should().Be("67890");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/shared_chat/session");
        _ = request.Query["broadcaster_id"].ToString().Should().Be("12345");
    }

    #endregion GetSharedChatSession
}

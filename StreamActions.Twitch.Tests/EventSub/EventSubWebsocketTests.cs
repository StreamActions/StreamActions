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
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.EventSub.Channel;
using StreamActions.Twitch.EventSub.Common;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using Xunit;
using EventSubService = StreamActions.Twitch.EventSub.EventSub;

namespace StreamActions.Twitch.Tests.EventSub;

/// <summary>
/// Performs unit and integration testing on EventSub WebSocket connection and receiving functionality.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class EventSubWebsocketTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubWebsocketTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public EventSubWebsocketTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose()
    {
        this._fixture.Reset();
    }

    #endregion IDisposable Implementation

    #region ConnectWebsocketAsync

    [Fact]
    [Trait("Member", "ConnectWebsocketAsync")]
    public async Task ConnectWebsocketAsync_WhenKeepaliveTimeoutLessThan10_ThrowsArgumentOutOfRangeException()
    {
        Func<Task> action = async () => await EventSubService.ConnectWebsocketAsync(9, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("keepaliveTimeoutSeconds");
    }

    [Fact]
    [Trait("Member", "ConnectWebsocketAsync")]
    public async Task ConnectWebsocketAsync_WhenKeepaliveTimeoutGreaterThan600_ThrowsArgumentOutOfRangeException()
    {
        Func<Task> action = async () => await EventSubService.ConnectWebsocketAsync(601, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("keepaliveTimeoutSeconds");
    }

    [Fact]
    [Trait("Member", "ConnectWebsocketAsync")]
    public async Task ConnectWebsocketAsync_WithoutKeepaliveTimeout_ConnectsSuccessfully()
    {
        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);

        _ = ws.State.Should().Be(WebSocketState.Open);
        _ = (this._fixture.LastRequest?.QueryString.Contains("keepalive_timeout_seconds", StringComparison.OrdinalIgnoreCase) ?? false).Should().BeFalse();

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ConnectWebsocketAsync")]
    public async Task ConnectWebsocketAsync_WithValidKeepaliveTimeout_SendsQueryParameter()
    {
        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(keepaliveTimeoutSeconds: 30, cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);

        _ = ws.State.Should().Be(WebSocketState.Open);
        _ = this._fixture.LastRequest.Should().NotBeNull();
        _ = this._fixture.LastRequest!.QueryString.Should().Contain("keepalive_timeout_seconds=30");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    #endregion ConnectWebsocketAsync

    #region ReceiveWebsocketMessageAsync

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenSocketIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await EventSubService.ReceiveWebsocketMessageAsync(null!, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("socket");
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenSessionWelcomeMessage_ReturnsParsedWelcomeSession()
    {
        string messageId = Guid.NewGuid().ToString();
        string welcomeJson = $$"""
        {
          "metadata": {
            "message_id": "{{messageId}}",
            "message_type": "session_welcome",
            "message_timestamp": "2023-07-19T14:56:51.616329Z"
          },
          "payload": {
            "session": {
              "id": "AQoQILLtwP2YTI6stDYQgILv-gEg-x",
              "status": "connected",
              "connected_at": "2023-07-19T14:56:51.616329Z",
              "keepalive_timeout_seconds": 10,
              "reconnect_url": null
            }
          }
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(welcomeJson);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.Event.Should().NotBeNull();
        _ = result.Event!.Metadata.Should().NotBeNull();
        _ = result.Event.Metadata!.MessageType.Should().Be(EventMetadata.MessageTypes.SessionWelcome);

        EventSubEvent? eventSubEvent = result.Event as EventSubEvent;
        _ = eventSubEvent.Should().NotBeNull();
        _ = eventSubEvent!.Payload.Should().NotBeNull();
        _ = eventSubEvent.Payload!.Session.Should().NotBeNull();
        _ = eventSubEvent.Payload.Session!.Id.Should().Be("AQoQILLtwP2YTI6stDYQgILv-gEg-x");
        _ = eventSubEvent.Payload.Session.Status.Should().Be(EventSubSession.ConnectionStatus.Connected);
        _ = eventSubEvent.Payload.Session.KeepaliveTimeoutSeconds.Should().Be(10);
        _ = eventSubEvent.Payload.Session.ConnectedAt.Should().Be(DateTime.Parse("2023-07-19T14:56:51.616329Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenSessionKeepaliveMessage_ReturnsKeepaliveMetadata()
    {
        string messageId = Guid.NewGuid().ToString();
        string keepaliveJson = $$"""
        {
          "metadata": {
            "message_id": "{{messageId}}",
            "message_type": "session_keepalive",
            "message_timestamp": "2023-07-19T14:57:01.616329Z"
          },
          "payload": {}
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(keepaliveJson);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.Event.Should().NotBeNull();
        _ = result.Event!.Metadata.Should().NotBeNull();
        _ = result.Event.Metadata!.MessageType.Should().Be(EventMetadata.MessageTypes.SessionKeepAlive);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenSessionReconnectMessage_ReturnsReconnectSessionWithUrl()
    {
        string messageId = Guid.NewGuid().ToString();
        string reconnectJson = $$"""
        {
          "metadata": {
            "message_id": "{{messageId}}",
            "message_type": "session_reconnect",
            "message_timestamp": "2023-07-19T14:58:01.616329Z"
          },
          "payload": {
            "session": {
              "id": "AQoQILLtwP2YTI6stDYQgILv-gEg-x",
              "status": "reconnecting",
              "keepalive_timeout_seconds": 10,
              "reconnect_url": "wss://eventsub.wss.twitch.tv/ws?reconnect=true",
              "connected_at": "2023-07-19T14:56:51.616329Z"
            }
          }
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(reconnectJson);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.Event.Should().NotBeNull();
        _ = result.Event!.Metadata.Should().NotBeNull();
        _ = result.Event.Metadata!.MessageType.Should().Be(EventMetadata.MessageTypes.SessionReconnect);

        EventSubEvent? eventSubEvent = result.Event as EventSubEvent;
        _ = eventSubEvent.Should().NotBeNull();
        _ = eventSubEvent!.Payload.Should().NotBeNull();
        _ = eventSubEvent.Payload!.Session.Should().NotBeNull();
        _ = eventSubEvent.Payload.Session!.Status.Should().Be(EventSubSession.ConnectionStatus.Reconnecting);
        _ = eventSubEvent.Payload.Session.ReconnectUrl.Should().Be(new Uri("wss://eventsub.wss.twitch.tv/ws?reconnect=true"));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenNotificationMessage_ReturnsTypedFollowEvent()
    {
        string messageId = Guid.NewGuid().ToString();
        string notificationJson = $$"""
        {
          "metadata": {
            "message_id": "{{messageId}}",
            "message_type": "notification",
            "message_timestamp": "2023-07-19T14:59:01.616329Z",
            "subscription_type": "channel.follow",
            "subscription_version": "2"
          },
          "payload": {
            "subscription": {
              "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
              "status": "enabled",
              "type": "channel.follow",
              "version": "2",
              "cost": 0,
              "condition": {
                "broadcaster_user_id": "1337",
                "moderator_user_id": "1337"
              },
              "transport": {
                "method": "websocket",
                "session_id": "AQoQILLtwP2YTI6stDYQgILv-gEg-x"
              },
              "created_at": "2023-07-19T14:56:51.616329Z"
            },
            "event": {
              "user_id": "1234",
              "user_login": "cool_user",
              "user_name": "Cool_User",
              "broadcaster_user_id": "1337",
              "broadcaster_user_login": "streamer",
              "broadcaster_user_name": "Streamer",
              "followed_at": "2023-07-19T14:59:00Z"
            }
          }
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(notificationJson);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.Event.Should().NotBeNull();
        _ = result.Event.Should().BeOfType<EventSubEvent<Follow>>();

        EventSubEvent<Follow>? followEvent = result.Event as EventSubEvent<Follow>;
        _ = followEvent.Should().NotBeNull();
        _ = followEvent!.Metadata.Should().NotBeNull();
        _ = followEvent.Metadata!.MessageType.Should().Be(EventMetadata.MessageTypes.Notification);
        _ = followEvent.Payload.Should().NotBeNull();
        _ = followEvent.Payload!.Subscription.Should().NotBeNull();
        _ = followEvent.Payload.Subscription!.Type.Should().Be("channel.follow");
        _ = followEvent.Payload.Event.Should().NotBeNull();
        _ = followEvent.Payload.Event!.UserId.Should().Be("1234");
        _ = followEvent.Payload.Event.UserLogin.Should().Be("cool_user");
        _ = followEvent.Payload.Event.UserName.Should().Be("Cool_User");
        _ = followEvent.Payload.Event.BroadcasterUserId.Should().Be("1337");
        _ = followEvent.Payload.Event.BroadcasterUserLogin.Should().Be("streamer");
        _ = followEvent.Payload.Event.BroadcasterUserName.Should().Be("Streamer");
        _ = followEvent.Payload.Event.FollowedAt.Should().Be(DateTime.Parse("2023-07-19T14:59:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenRevocationMessage_ReturnsRevocationSubscription()
    {
        string messageId = Guid.NewGuid().ToString();
        string revocationJson = $$"""
        {
          "metadata": {
            "message_id": "{{messageId}}",
            "message_type": "revocation",
            "message_timestamp": "2023-07-19T15:00:01.616329Z",
            "subscription_type": "channel.follow",
            "subscription_version": "2"
          },
          "payload": {
            "subscription": {
              "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
              "status": "authorization_revoked",
              "type": "channel.follow",
              "version": "2",
              "cost": 0,
              "condition": {
                "broadcaster_user_id": "1337",
                "moderator_user_id": "1337"
              },
              "transport": {
                "method": "websocket",
                "session_id": "AQoQILLtwP2YTI6stDYQgILv-gEg-x"
              },
              "created_at": "2023-07-19T14:56:51.616329Z"
            }
          }
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(revocationJson);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.Event.Should().NotBeNull();
        _ = result.Event!.Metadata.Should().NotBeNull();
        _ = result.Event.Metadata!.MessageType.Should().Be(EventMetadata.MessageTypes.Revocation);

        EventSubEvent? eventSubEvent = result.Event as EventSubEvent;
        _ = eventSubEvent.Should().NotBeNull();
        _ = eventSubEvent!.Payload.Should().NotBeNull();
        _ = eventSubEvent.Payload!.Subscription.Should().NotBeNull();
        _ = eventSubEvent.Payload.Subscription!.Status.Should().Be(EventSubSubscription.SubscriptionStatus.AuthorizationRevoked);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenMultiFrameFragmentedMessage_ReassemblesAndParsesSuccessfully()
    {
        string messageId = Guid.NewGuid().ToString();
        string welcomeJson = $$"""
        {
          "metadata": {
            "message_id": "{{messageId}}",
            "message_type": "session_welcome",
            "message_timestamp": "2023-07-19T14:56:51.616329Z"
          },
          "payload": {
            "session": {
              "id": "AQoQILLtwP2YTI6stDYQgILv-gEg-x",
              "status": "connected",
              "connected_at": "2023-07-19T14:56:51.616329Z",
              "keepalive_timeout_seconds": 10,
              "reconnect_url": null
            }
          }
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] fullBytes = Encoding.UTF8.GetBytes(welcomeJson);
            int half = fullBytes.Length / 2;
            await webSocket.SendAsync(new ArraySegment<byte>(fullBytes, 0, half), WebSocketMessageType.Text, false, TestContext.Current.CancellationToken).ConfigureAwait(false);
            await webSocket.SendAsync(new ArraySegment<byte>(fullBytes, half, fullBytes.Length - half), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.Event.Should().NotBeNull();
        _ = result.Event!.Metadata.Should().NotBeNull();
        _ = result.Event.Metadata!.MessageType.Should().Be(EventMetadata.MessageTypes.SessionWelcome);

        EventSubEvent? eventSubEvent = result.Event as EventSubEvent;
        _ = eventSubEvent.Should().NotBeNull();
        _ = eventSubEvent!.Payload!.Session!.Id.Should().Be("AQoQILLtwP2YTI6stDYQgILv-gEg-x");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenDuplicateMessageId_ReturnsDuplicateFlagAndNullEvent()
    {
        string duplicateMessageId = Guid.NewGuid().ToString();
        string message1 = $$"""
        {
          "metadata": {
            "message_id": "{{duplicateMessageId}}",
            "message_type": "session_welcome",
            "message_timestamp": "2023-07-19T14:56:51.616329Z"
          },
          "payload": {
            "session": {
              "id": "AQoQILLtwP2YTI6stDYQgILv-gEg-x",
              "status": "connected",
              "connected_at": "2023-07-19T14:56:51.616329Z",
              "keepalive_timeout_seconds": 10,
              "reconnect_url": null
            }
          }
        }
        """;
        string message2 = $$"""
        {
          "metadata": {
            "message_id": "{{duplicateMessageId}}",
            "message_type": "session_keepalive",
            "message_timestamp": "2023-07-19T14:57:01.616329Z"
          },
          "payload": {}
        }
        """;

        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) =>
        {
            byte[] bytes1 = Encoding.UTF8.GetBytes(message1);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes1), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);

            byte[] bytes2 = Encoding.UTF8.GetBytes(message2);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes2), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        });

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);

        EventSubWebsocketResult firstResult = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = firstResult.Should().NotBeNull();
        _ = firstResult.IsDuplicate.Should().BeFalse();
        _ = firstResult.Event.Should().NotBeNull();

        EventSubWebsocketResult secondResult = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = secondResult.Should().NotBeNull();
        _ = secondResult.IsDuplicate.Should().BeTrue();
        _ = secondResult.Event.Should().BeNull();

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "ReceiveWebsocketMessageAsync")]
    public async Task ReceiveWebsocketMessageAsync_WhenServerInitiatesCloseWith4000_ReturnsInternalServerErrorCloseCode()
    {
        this._fixture.QueueWebSocketAction(async (webSocket, httpContext) => await webSocket.CloseOutputAsync((WebSocketCloseStatus)4000, "Internal Server Error", TestContext.Current.CancellationToken).ConfigureAwait(false));

        Uri wsUri = new($"{this._fixture.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)}/ws");
        using ClientWebSocket ws = await EventSubService.ConnectWebsocketAsync(cancellationToken: TestContext.Current.CancellationToken, uri: wsUri).ConfigureAwait(true);
        EventSubWebsocketResult result = await EventSubService.ReceiveWebsocketMessageAsync(ws, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.CloseCode.Should().Be(EventSubService.CloseCodes.InternalServerError);
        _ = ws.CloseStatus.Should().Be((WebSocketCloseStatus)4000);
    }

    #endregion ReceiveWebsocketMessageAsync
}

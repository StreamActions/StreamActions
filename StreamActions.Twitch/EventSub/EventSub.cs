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

using StreamActions.Common;
using StreamActions.Common.Attributes;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.EventSub.Common;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IoStream = System.IO.Stream;

namespace StreamActions.Twitch.EventSub;

/// <summary>
/// Handles processing of messages for EventSub, including creating websocket connections and processing webhooks.
/// </summary>
[DocParser("EventSub Fields", 57, "https://dev.twitch.tv/docs/eventsub/reference/", "twitch/TwitchEventSubReferenceParser")]
[DocParser("EventSub WebSocket Messages", 57, "https://dev.twitch.tv/docs/eventsub/websocket-reference/", "twitch/TwitchEventSubWebSocketMessagesParser")]
[DocParser("EventSub Webhook Handling", 57, "https://dev.twitch.tv/docs/eventsub/handling-webhook-events/", "twitch/TwitchGenericDocumentationParser")]
[DocParser("EventSub Websocket Handling", 57, "https://dev.twitch.tv/docs/eventsub/handling-websocket-events/", "twitch/TwitchGenericDocumentationParser")]
public sealed class EventSub
{
    /// <summary>
    /// Close codes for EventSub WebSocket connections.
    /// </summary>
    public enum CloseCodes
    {
        /// <summary>
        /// Unknown close reason.
        /// </summary>
        None = 0,
        /// <summary>
        /// The connection was closed normally.
        /// </summary>
        NormalClosure = 1000,
        /// <summary>
        /// Indicates a problem with the server (similar to an HTTP 500 status code).
        /// </summary>
        InternalServerError = 4000,
        /// <summary>
        /// Sending outgoing messages to the server is prohibited with the exception of pong messages.
        /// </summary>
        ClientSentInboundTraffic = 4001,
        /// <summary>
        /// You must respond to ping messages with a pong message.
        /// </summary>
        ClientFailedPingPong = 4002,
        /// <summary>
        /// When you connect to the server, you must create a subscription within <see cref="EventSubSession.KeepaliveTimeoutSeconds"/> seconds or the connection is closed.
        /// </summary>
        ConnectionUnused = 4003,
        /// <summary>
        /// When you receive a <see cref="EventMetadata.MessageTypes.SessionReconnect"/> message, you have 30 seconds to reconnect to the server and close the old connection.
        /// </summary>
        ReconnectGraceTimeExpired = 4004,
        /// <summary>
        /// Transient network timeout.
        /// </summary>
        NetworkTimeout = 4005,
        /// <summary>
        /// Transient network error.
        /// </summary>
        NetworkError = 4006,
        /// <summary>
        /// The reconnect URL is invalid.
        /// </summary>
        InvalidReconnect = 4007
    }

    #region Private Constants

    /// <summary>
    /// The default WebSocket URI for Twitch EventSub.
    /// </summary>
    private const string _defaultWebsocketUri = "wss://eventsub.wss.twitch.tv/ws";

    /// <summary>
    /// The header name for the Twitch EventSub message ID.
    /// </summary>
    private const string _messageIdHeader = "Twitch-Eventsub-Message-Id";

    /// <summary>
    /// The header name for the Twitch EventSub message timestamp.
    /// </summary>
    private const string _messageTimestampHeader = "Twitch-Eventsub-Message-Timestamp";

    /// <summary>
    /// The header name for the Twitch EventSub message signature.
    /// </summary>
    private const string _messageSignatureHeader = "Twitch-Eventsub-Message-Signature";

    /// <summary>
    /// The header name for the Twitch EventSub message type.
    /// </summary>
    private const string _messageTypeHeader = "Twitch-Eventsub-Message-Type";

    /// <summary>
    /// The header name for the Twitch EventSub subscription type.
    /// </summary>
    private const string _subscriptionTypeHeader = "Twitch-Eventsub-Subscription-Type";

    /// <summary>
    /// The maximum allowed webhook request body size in bytes (256 KB).
    /// </summary>
    private const int _maxWebhookBodyBytes = 256 * 1024;

    /// <summary>
    /// The maximum allowed WebSocket message size in bytes (256 KB).
    /// </summary>
    private const int _maxWebsocketMessageBytes = 256 * 1024;

    /// <summary>
    /// The header name for the Twitch EventSub subscription version.
    /// </summary>
    private const string _subscriptionVersionHeader = "Twitch-Eventsub-Subscription-Version";

    #endregion Private Constants

    #region Private Fields

    /// <summary>
    /// The maximum age allowed for a webhook message before it is considered expired or pruned from duplicate detection.
    /// </summary>
    private static readonly TimeSpan _messageMaxAge = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The maximum allowed future clock skew for incoming webhook timestamps.
    /// </summary>
    private static readonly TimeSpan _maxFutureSkew = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The minimum interval between duplicate detection cache pruning runs.
    /// </summary>
    private static readonly TimeSpan _pruneInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A cache mapping recently seen EventSub message IDs to the UTC time they were first received.
    /// </summary>
    private static readonly ConcurrentDictionary<string, DateTime> _seenMessageIds = new(StringComparer.Ordinal);

    /// <summary>
    /// The UTC timestamp in ticks when the message ID cache was last pruned.
    /// </summary>
    private static long _lastPruneTicks;

    /// <summary>
    /// A cache mapping EventSub subscription types to deserialization factory delegates.
    /// </summary>
    private static readonly FrozenDictionary<string, SubscriptionDeserializer> _subscriptionTypeCache = BuildSubscriptionTypeCache();

    /// <summary>
    /// Delegate to deserialize an EventSub event from raw UTF-8 body bytes.
    /// </summary>
    /// <param name="bodyBytes">The raw UTF-8 body bytes.</param>
    /// <param name="metadata">The metadata associated with the event.</param>
    /// <returns>The deserialized <see cref="IEventSubEvent"/>.</returns>
    private delegate IEventSubEvent DeserializeEventSubDelegate(ReadOnlySpan<byte> bodyBytes, EventMetadata metadata);

    /// <summary>
    /// Encapsulates deserialization delegates and payload type metadata for an EventSub subscription type.
    /// </summary>
    /// <param name="FromBytes">Delegate to deserialize from raw UTF-8 bytes.</param>
    /// <param name="PayloadType">The concrete <see cref="EventPayload{TEventType}"/> type.</param>
    /// <param name="WrapPayload">Delegate to wrap a deserialized payload object into an <see cref="IEventSubEvent"/>.</param>
    private readonly record struct SubscriptionDeserializer(
        DeserializeEventSubDelegate FromBytes,
        Type PayloadType,
        Func<object?, EventMetadata, IEventSubEvent> WrapPayload);

    #endregion Private Fields

    #region Public Methods

    /// <summary>
    /// Processes an EventSub message delivered via webhook from headers and a request body stream.
    /// </summary>
    /// <param name="headers">The incoming HTTP headers.</param>
    /// <param name="body">The incoming HTTP request body stream.</param>
    /// <param name="secret">The secret string configured for the EventSub subscription to verify message signatures.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used for timestamp and replay-window checks, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token to cancel reading the body stream.</param>
    /// <returns>An <see cref="EventSubWebhookResult"/> containing the processing results and appropriate HTTP response information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> or <paramref name="body"/> is <see langword="null"/>; or <paramref name="secret"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Possible HTTP status codes returned in the result:
    /// </para>
    /// <para>
    /// 200 OK: Returned when handling a webhook callback verification challenge (<c>webhook_callback_verification</c>).
    /// </para>
    /// <para>
    /// 202 Accepted: Returned when receiving an event notification (<c>notification</c>), revocation (<c>revocation</c>), duplicate message, or unknown message type.
    /// </para>
    /// <para>
    /// 403 Forbidden: Returned when signature verification fails, required headers are missing, or the message timestamp is expired or too far in the future.
    /// </para>
    /// <para>
    /// 413 Payload Too Large: Returned when the webhook body exceeds 256 KB.
    /// </para>
    /// <para>
    /// Duplicate detection is maintained in-memory per process (not shared across multiple server instances) with a 10-minute window.
    /// </para>
    /// </remarks>
    public static async Task<EventSubWebhookResult> ProcessWebhook(
        IReadOnlyDictionary<string, string?> headers,
        IoStream body,
        string secret,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentNullException(nameof(secret));
        }

        if (!TryExtractAndValidateHeaders(
            headers,
            timeProvider,
            out string? messageId,
            out string? messageTimestamp,
            out string? messageSignature,
            out string? messageType,
            out string? subscriptionType,
            out string? subscriptionVersion,
            out DateTime parsedTimestamp,
            out DateTime utcNow,
            out TimeProvider effectiveTimeProvider))
        {
            return new()
            {
                IsVerified = false,
                StatusCode = (int)HttpStatusCode.Forbidden
            };
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        using MemoryStream memoryStream = new();
        try
        {
            int totalRead = 0;
            int bytesRead;
            while ((bytesRead = await body.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > _maxWebhookBodyBytes)
                {
                    return new()
                    {
                        IsVerified = false,
                        StatusCode = (int)HttpStatusCode.RequestEntityTooLarge
                    };
                }

                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        ReadOnlySpan<byte> bodySpan = memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length);
        return ProcessWebhookCore(
            messageId!,
            messageTimestamp!,
            messageSignature!,
            messageType!,
            subscriptionType,
            subscriptionVersion,
            parsedTimestamp,
            utcNow,
            effectiveTimeProvider,
            bodySpan,
            secret);
    }

    /// <summary>
    /// Processes an EventSub message delivered via webhook from headers and raw body bytes.
    /// </summary>
    /// <param name="headers">The incoming HTTP headers.</param>
    /// <param name="bodyBytes">The raw webhook request body bytes.</param>
    /// <param name="secret">The secret string configured for the EventSub subscription to verify message signatures.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used for timestamp and replay-window checks, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    /// <returns>An <see cref="EventSubWebhookResult"/> containing the processing results and appropriate HTTP response information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>; or <paramref name="secret"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Possible HTTP status codes returned in the result:
    /// </para>
    /// <para>
    /// 200 OK: Returned when handling a webhook callback verification challenge (<c>webhook_callback_verification</c>).
    /// </para>
    /// <para>
    /// 202 Accepted: Returned when receiving an event notification (<c>notification</c>), revocation (<c>revocation</c>), duplicate message, or unknown message type.
    /// </para>
    /// <para>
    /// 403 Forbidden: Returned when signature verification fails, required headers are missing, or the message timestamp is expired or too far in the future.
    /// </para>
    /// <para>
    /// 413 Payload Too Large: Returned when <paramref name="bodyBytes"/> exceeds 256 KB.
    /// </para>
    /// </remarks>
    public static EventSubWebhookResult ProcessWebhook(
        IReadOnlyDictionary<string, string?> headers,
        ReadOnlySpan<byte> bodyBytes,
        string secret,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return string.IsNullOrWhiteSpace(secret)
            ? throw new ArgumentNullException(nameof(secret))
            : !TryExtractAndValidateHeaders(
            headers,
            timeProvider,
            out string? messageId,
            out string? messageTimestamp,
            out string? messageSignature,
            out string? messageType,
            out string? subscriptionType,
            out string? subscriptionVersion,
            out DateTime parsedTimestamp,
            out DateTime utcNow,
            out TimeProvider effectiveTimeProvider)
            ? new()
            {
                IsVerified = false,
                StatusCode = (int)HttpStatusCode.Forbidden
            }
            : bodyBytes.Length > _maxWebhookBodyBytes
            ? new()
            {
                IsVerified = false,
                StatusCode = (int)HttpStatusCode.RequestEntityTooLarge
            }
            : ProcessWebhookCore(
                messageId!,
                messageTimestamp!,
                messageSignature!,
                messageType!,
                subscriptionType,
                subscriptionVersion,
                parsedTimestamp,
                utcNow,
                effectiveTimeProvider,
                bodyBytes,
                secret);
    }

    /// <summary>
    /// Connects to the EventSub WebSocket server.
    /// </summary>
    /// <param name="keepaliveTimeoutSeconds">The keepalive timeout in seconds, between 10 and 600 seconds. If specified, sent as a query parameter.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous connection operation.</param>
    /// <param name="uri">The WebSocket server URI. If <see langword="null"/>, the default Twitch EventSub WebSocket URI is used.</param>
    /// <returns>A connected <see cref="ClientWebSocket"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="keepaliveTimeoutSeconds"/> is less than 10 or greater than 600.</exception>
    /// <remarks>
    /// <para>
    /// Callers are responsible for managing the lifetime of the returned <see cref="ClientWebSocket"/>.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The uri parameter is placed last as it is only intended to be overridden for testing purposes.")]
    public static async Task<ClientWebSocket> ConnectWebsocketAsync(int? keepaliveTimeoutSeconds = null, CancellationToken cancellationToken = default, Uri? uri = null)
    {
        if (keepaliveTimeoutSeconds.HasValue && keepaliveTimeoutSeconds.Value is < 10 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(keepaliveTimeoutSeconds), keepaliveTimeoutSeconds.Value, "The keepalive timeout must be between 10 and 600 seconds.");
        }

        NameValueCollection queryParameters = [];
        if (keepaliveTimeoutSeconds.HasValue)
        {
            queryParameters.Add("keepalive_timeout_seconds", keepaliveTimeoutSeconds.Value.ToString(CultureInfo.InvariantCulture));
        }

        // The URI is intentionally not validated as it can be overridden for testing purposes.
        Uri targetUri = Util.BuildUri(uri ?? new(_defaultWebsocketUri), queryParameters);

        ClientWebSocket client = new();
        try
        {
            // The URI is intentionally not validated as it can be overridden for testing purposes.
            await client.ConnectAsync(targetUri, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Receives and processes a single message from an EventSub WebSocket connection.
    /// </summary>
    /// <param name="socket">The WebSocket connection from which to receive the message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous receive operation.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used for replay-window and cache-pruning checks, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    /// <returns>An <see cref="EventSubWebsocketResult"/> containing the received event, close code, or exception information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="socket"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method reads frames until a complete message is assembled, decodes the message according to its message type, and detects duplicate messages.
    /// </para>
    /// <para>
    /// If the connection is closed, the returned result includes the corresponding <see cref="CloseCodes"/>. If a socket or JSON parsing exception occurs, or if the message exceeds 256 KB, it is captured in the result rather than thrown.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Socket exceptions are intentionally caught and returned in the result record.")]
    [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The timeProvider parameter is placed last as it is only intended to be overridden for testing purposes.")]
    public static async Task<EventSubWebsocketResult> ReceiveWebsocketMessageAsync(WebSocket socket, CancellationToken cancellationToken = default, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(socket);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        ArrayBufferWriter<byte> bufferWriter = new();
        ValueWebSocketReceiveResult receiveResult;

        try
        {
            do
            {
                receiveResult = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (bufferWriter.WrittenCount + receiveResult.Count > _maxWebsocketMessageBytes)
                {
                    return new()
                    {
                        Exception = new InvalidDataException($"EventSub WebSocket message exceeded maximum allowed size of {_maxWebsocketMessageBytes} bytes.")
                    };
                }

                bufferWriter.Write(buffer.AsSpan(0, receiveResult.Count));
            }
            while (!receiveResult.EndOfMessage);
        }
        catch (Exception ex)
        {
            return new()
            {
                Exception = ex
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (receiveResult.MessageType == WebSocketMessageType.Close)
        {
            WebSocketCloseStatus? closeStatus = socket.CloseStatus;
            CloseCodes closeCode = closeStatus.HasValue && Enum.IsDefined(typeof(CloseCodes), (int)closeStatus.Value)
                ? (CloseCodes)(int)closeStatus.Value
                : CloseCodes.None;

            return new()
            {
                CloseCode = closeCode
            };
        }

        if (bufferWriter.WrittenCount == 0)
        {
            return new()
            {
                IsDuplicate = false,
                Event = null
            };
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(bufferWriter.WrittenMemory);
            if (!document.RootElement.TryGetProperty("metadata", out JsonElement metadataElement))
            {
                return new()
                {
                    IsDuplicate = false,
                    Event = null
                };
            }

            string? rawMessageType = metadataElement.TryGetProperty("message_type", out JsonElement messageTypeElement) && messageTypeElement.ValueKind == JsonValueKind.String
                ? messageTypeElement.GetString()
                : null;

            EventMetadata.MessageTypes? mappedMessageType = rawMessageType switch
            {
                "session_welcome" => EventMetadata.MessageTypes.SessionWelcome,
                "session_keepalive" => EventMetadata.MessageTypes.SessionKeepAlive,
                "notification" => EventMetadata.MessageTypes.Notification,
                "revocation" => EventMetadata.MessageTypes.Revocation,
                "session_reconnect" => EventMetadata.MessageTypes.SessionReconnect,
                _ => null
            };

            EventMetadata? deserializedMetadata = metadataElement.Deserialize<EventMetadata>(TwitchApi.SerializerOptions);
            EventMetadata? metadata = deserializedMetadata is not null
                ? deserializedMetadata with { MessageType = mappedMessageType }
                : null;

            string? messageId = metadataElement.TryGetProperty("message_id", out JsonElement messageIdElement) && messageIdElement.ValueKind == JsonValueKind.String
                ? messageIdElement.GetString()
                : metadata?.MessageId?.ToString();

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                TimeProvider effectiveTimeProvider = timeProvider ?? TimeProvider.System;
                PruneSeenMessages(effectiveTimeProvider);

                if (!_seenMessageIds.TryAdd(messageId, effectiveTimeProvider.GetUtcNow().UtcDateTime))
                {
                    return new()
                    {
                        IsDuplicate = true,
                        Event = null
                    };
                }
            }

            IEventSubEvent? eventData = null;
            if (metadata?.MessageType is not null)
            {
                switch (metadata.MessageType.Value)
                {
                    case EventMetadata.MessageTypes.SessionWelcome:
                    case EventMetadata.MessageTypes.SessionKeepAlive:
                    case EventMetadata.MessageTypes.SessionReconnect:
                        EventPayload? sessionPayload = document.RootElement.TryGetProperty("payload", out JsonElement sessionPayloadElement)
                            ? sessionPayloadElement.Deserialize<EventPayload>(TwitchApi.SerializerOptions)
                            : null;

                        eventData = new EventSubEvent
                        {
                            Metadata = metadata,
                            Payload = sessionPayload
                        };
                        break;

                    case EventMetadata.MessageTypes.Notification:
                    case EventMetadata.MessageTypes.Revocation:
                        if (!string.IsNullOrWhiteSpace(metadata.SubscriptionType) &&
                            _subscriptionTypeCache.TryGetValue(metadata.SubscriptionType, out SubscriptionDeserializer deserializer) &&
                            document.RootElement.TryGetProperty("payload", out JsonElement eventPayloadElement))
                        {
                            object? payload = eventPayloadElement.Deserialize(deserializer.PayloadType, TwitchApi.SerializerOptions);
                            eventData = deserializer.WrapPayload(payload, metadata);
                        }
                        break;
                    default:
                        break;
                }
            }

            return new()
            {
                IsDuplicate = false,
                Event = eventData
            };
        }
        catch (JsonException ex)
        {
            return new()
            {
                Exception = ex
            };
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Extracts a header value from <paramref name="headers"/> using a direct lookup fast path followed by a case-insensitive fallback scan.
    /// </summary>
    /// <param name="headers">The HTTP header dictionary.</param>
    /// <param name="headerName">The header name to look up.</param>
    /// <returns>The header value if found; otherwise, <see langword="null"/>.</returns>
    private static string? GetHeaderValue(IReadOnlyDictionary<string, string?> headers, string headerName)
    {
        if (headers.TryGetValue(headerName, out string? value))
        {
            return value;
        }

        foreach (KeyValuePair<string, string?> kvp in headers)
        {
            if (string.Equals(kvp.Key, headerName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates required EventSub webhook headers and the replay/future timestamp window before reading the body.
    /// </summary>
    /// <param name="headers">The incoming HTTP header dictionary.</param>
    /// <param name="timeProvider">Optional <see cref="TimeProvider"/> for current UTC time.</param>
    /// <param name="messageId">The extracted message ID header.</param>
    /// <param name="messageTimestamp">The extracted message timestamp header string.</param>
    /// <param name="messageSignature">The extracted HMAC signature header.</param>
    /// <param name="messageType">The extracted message type header.</param>
    /// <param name="subscriptionType">The extracted subscription type header, if present.</param>
    /// <param name="subscriptionVersion">The extracted subscription version header, if present.</param>
    /// <param name="parsedTimestamp">The parsed UTC timestamp.</param>
    /// <param name="utcNow">The current UTC time from <paramref name="effectiveTimeProvider"/>.</param>
    /// <param name="effectiveTimeProvider">The resolved <see cref="TimeProvider"/> instance.</param>
    /// <returns><see langword="true"/> if all required headers and the timestamp window are valid; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractAndValidateHeaders(
        IReadOnlyDictionary<string, string?> headers,
        TimeProvider? timeProvider,
        out string? messageId,
        out string? messageTimestamp,
        out string? messageSignature,
        out string? messageType,
        out string? subscriptionType,
        out string? subscriptionVersion,
        out DateTime parsedTimestamp,
        out DateTime utcNow,
        out TimeProvider effectiveTimeProvider)
    {
        effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        messageId = GetHeaderValue(headers, _messageIdHeader);
        messageTimestamp = GetHeaderValue(headers, _messageTimestampHeader);
        messageSignature = GetHeaderValue(headers, _messageSignatureHeader);
        messageType = GetHeaderValue(headers, _messageTypeHeader);
        subscriptionType = GetHeaderValue(headers, _subscriptionTypeHeader);
        subscriptionVersion = GetHeaderValue(headers, _subscriptionVersionHeader);

        if (string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(messageTimestamp) ||
            string.IsNullOrWhiteSpace(messageSignature) ||
            string.IsNullOrWhiteSpace(messageType))
        {
            parsedTimestamp = default;
            utcNow = default;
            return false;
        }

        if (!DateTime.TryParse(messageTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsedTimestamp))
        {
            utcNow = default;
            return false;
        }

        utcNow = effectiveTimeProvider.GetUtcNow().UtcDateTime;
        return utcNow - parsedTimestamp <= _messageMaxAge && parsedTimestamp - utcNow <= _maxFutureSkew;
    }

    /// <summary>
    /// Verifies the HMAC-SHA256 webhook signature, enforces deduplication, and parses the EventSub payload.
    /// </summary>
    /// <param name="messageId">The validated message ID.</param>
    /// <param name="messageTimestamp">The validated timestamp header string.</param>
    /// <param name="messageSignature">The provided HMAC signature header.</param>
    /// <param name="messageType">The EventSub message type.</param>
    /// <param name="subscriptionType">The optional subscription type.</param>
    /// <param name="subscriptionVersion">The optional subscription version.</param>
    /// <param name="parsedTimestamp">The parsed UTC timestamp.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="effectiveTimeProvider">The resolved <see cref="TimeProvider"/>.</param>
    /// <param name="bodyBytes">The raw webhook body bytes.</param>
    /// <param name="secret">The webhook secret key.</param>
    /// <returns>The resulting <see cref="EventSubWebhookResult"/>.</returns>
    private static EventSubWebhookResult ProcessWebhookCore(
        string messageId,
        string messageTimestamp,
        string messageSignature,
        string messageType,
        string? subscriptionType,
        string? subscriptionVersion,
        DateTime parsedTimestamp,
        DateTime utcNow,
        TimeProvider effectiveTimeProvider,
        ReadOnlySpan<byte> bodyBytes,
        string secret)
    {
        using IncrementalHash hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret));
        hmac.AppendData(Encoding.UTF8.GetBytes(messageId));
        hmac.AppendData(Encoding.UTF8.GetBytes(messageTimestamp));
        hmac.AppendData(bodyBytes);
        byte[] computedHash = hmac.GetHashAndReset();

        string expectedSignature = "sha256=" + Convert.ToHexStringLower(computedHash);
        byte[] expectedSignatureBytes = Encoding.UTF8.GetBytes(expectedSignature);
        byte[] actualSignatureBytes = Encoding.UTF8.GetBytes(messageSignature);

        if (!CryptographicOperations.FixedTimeEquals(expectedSignatureBytes, actualSignatureBytes))
        {
            return new()
            {
                IsVerified = false,
                StatusCode = (int)HttpStatusCode.Forbidden
            };
        }

        if (string.Equals(messageType, "webhook_callback_verification", StringComparison.Ordinal))
        {
            Utf8JsonReader jsonReader = new(bodyBytes);
            using JsonDocument document = JsonDocument.ParseValue(ref jsonReader);
            string? challenge = document.RootElement.TryGetProperty("challenge", out JsonElement challengeElement)
                ? challengeElement.GetString()
                : null;

            return new()
            {
                IsVerified = true,
                StatusCode = (int)HttpStatusCode.OK,
                ContentType = "text/plain",
                Body = challenge,
                Event = null
            };
        }

        PruneSeenMessages(effectiveTimeProvider);

        if (!_seenMessageIds.TryAdd(messageId, utcNow))
        {
            return new()
            {
                IsVerified = true,
                IsDuplicate = true,
                StatusCode = (int)HttpStatusCode.Accepted,
                Event = null
            };
        }

        EventMetadata.MessageTypes? mappedMessageType = messageType switch
        {
            "notification" => EventMetadata.MessageTypes.Notification,
            "revocation" => EventMetadata.MessageTypes.Revocation,
            _ => null
        };

        EventMetadata metadata = new()
        {
            MessageId = Guid.TryParse(messageId, out Guid parsedId) ? parsedId : null,
            MessageType = mappedMessageType,
            MessageTimestamp = parsedTimestamp,
            SubscriptionType = subscriptionType,
            SubscriptionVersion = subscriptionVersion
        };

        IEventSubEvent? eventData = null;
        if (!string.IsNullOrWhiteSpace(subscriptionType) &&
            _subscriptionTypeCache.TryGetValue(subscriptionType, out SubscriptionDeserializer deserializer))
        {
            eventData = deserializer.FromBytes(bodyBytes, metadata);
        }

        return new()
        {
            IsVerified = true,
            IsDuplicate = false,
            StatusCode = (int)HttpStatusCode.Accepted,
            Event = eventData
        };
    }

    /// <summary>
    /// Builds the cache mapping subscription type strings to deserialization factory delegates.
    /// </summary>
    /// <returns>A frozen dictionary mapping subscription types to deserializer entries.</returns>
    private static FrozenDictionary<string, SubscriptionDeserializer> BuildSubscriptionTypeCache()
    {
        Dictionary<string, SubscriptionDeserializer> cache = [];
        MethodInfo deserializeMethod = typeof(EventSub).GetMethod(nameof(DeserializeEventSubEvent), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo wrapMethod = typeof(EventSub).GetMethod(nameof(WrapEventSubEvent), BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (Type type in typeof(EventSub).Assembly.GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && !type.IsGenericType && typeof(IEventSubType).IsAssignableFrom(type))
            {
                PropertyInfo? typeProperty = type.GetProperty(nameof(IEventSubType.Type), BindingFlags.Public | BindingFlags.Static);
                if (typeProperty?.GetValue(null) is string subscriptionType && !string.IsNullOrWhiteSpace(subscriptionType))
                {
                    MethodInfo genericMethod = deserializeMethod.MakeGenericMethod(type);
                    DeserializeEventSubDelegate handler = genericMethod.CreateDelegate<DeserializeEventSubDelegate>();
                    Type payloadType = typeof(EventPayload<>).MakeGenericType(type);
                    MethodInfo genericWrapMethod = wrapMethod.MakeGenericMethod(type);
                    Func<object?, EventMetadata, IEventSubEvent> wrapHandler = genericWrapMethod.CreateDelegate<Func<object?, EventMetadata, IEventSubEvent>>();
                    cache[subscriptionType] = new(handler, payloadType, wrapHandler);
                }
            }
        }

        return cache.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Wraps a deserialized payload object into an <see cref="EventSubEvent{TEventType}"/> with the specified metadata.
    /// </summary>
    /// <typeparam name="TEventType">The event payload type.</typeparam>
    /// <param name="payload">The deserialized payload instance.</param>
    /// <param name="metadata">The metadata extracted from the message.</param>
    /// <returns>An <see cref="EventSubEvent{TEventType}"/> instance implementing <see cref="IEventSubEvent"/>.</returns>
    private static EventSubEvent<TEventType> WrapEventSubEvent<TEventType>(object? payload, EventMetadata metadata) where TEventType : IEventSubType =>
        new()
        {
            Metadata = metadata,
            Payload = payload as EventPayload<TEventType>
        };

    /// <summary>
    /// Deserializes the body into an <see cref="EventSubEvent{TEventType}"/> with the specified metadata.
    /// </summary>
    /// <typeparam name="TEventType">The event payload type.</typeparam>
    /// <param name="bodyBytes">The raw webhook request body bytes.</param>
    /// <param name="metadata">The metadata extracted from request headers.</param>
    /// <returns>An <see cref="EventSubEvent{TEventType}"/> instance implementing <see cref="IEventSubEvent"/>.</returns>
    private static EventSubEvent<TEventType> DeserializeEventSubEvent<TEventType>(ReadOnlySpan<byte> bodyBytes, EventMetadata metadata) where TEventType : IEventSubType
    {
        EventPayload<TEventType>? payload = bodyBytes.Length > 0
            ? JsonSerializer.Deserialize<EventPayload<TEventType>>(bodyBytes, TwitchApi.SerializerOptions)
            : null;

        return new EventSubEvent<TEventType>
        {
            Metadata = metadata,
            Payload = payload
        };
    }

    /// <summary>
    /// Prunes expired message IDs from the duplicate detection cache if the prune interval has elapsed.
    /// </summary>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used to determine the current UTC time.</param>
    /// <remarks>
    /// <para>
    /// This method is amortized to execute at most once per <see cref="_pruneInterval"/>.
    /// </para>
    /// <para>
    /// Thread safety is ensured using <see cref="Interlocked.CompareExchange(ref long, long, long)"/> so that only one caller performs the cleanup, removing entries whose first-seen time is older than <see cref="_messageMaxAge"/>.
    /// </para>
    /// </remarks>
    private static void PruneSeenMessages(TimeProvider timeProvider)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        long nowTicks = utcNow.Ticks;
        long lastPruneTicks = Volatile.Read(ref _lastPruneTicks);

        if ((nowTicks - lastPruneTicks) < _pruneInterval.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastPruneTicks, nowTicks, lastPruneTicks) != lastPruneTicks)
        {
            return;
        }

        DateTime cutoff = utcNow - _messageMaxAge;
        foreach (KeyValuePair<string, DateTime> entry in _seenMessageIds)
        {
            if (entry.Value < cutoff)
            {
                _ = _seenMessageIds.TryRemove(entry);
            }
        }
    }

    #endregion Private Methods
}

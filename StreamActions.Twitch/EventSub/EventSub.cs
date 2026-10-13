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

using Microsoft.AspNetCore.Http;
using StreamActions.Common.Attributes;
using StreamActions.Common.Logging;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.EventSub.Common;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StreamActions.Twitch.EventSub;

/// <summary>
/// Handles processing of messages for EventSub, including creating websocket connections and processing webhooks.
/// </summary>
[DocParser("EventSub Subscription Types", 57, "https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/", "TwitchEventSubSubscriptionTypesParser")]
[DocParser("EventSub Fields", 57, "https://dev.twitch.tv/docs/eventsub/eventsub-reference/", "TwitchEventSubReferenceParser")]
[DocParser("EventSub WebSocket Messages", 57, "https://dev.twitch.tv/docs/eventsub/websocket-reference/", "TwitchEventSubWebSocketMessagesParser")]
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
        /// When you connect to the server, you must create a subscription within <see cref="EventSubSession.KeepAliveInterval"/> seconds or the connection is closed.
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
    private static readonly FrozenDictionary<string, Func<byte[], EventMetadata, object>> _subscriptionTypeCache = BuildSubscriptionTypeCache();

    #endregion Private Fields

    #region Public Methods

    /// <summary>
    /// Processes an EventSub message delivered via webhook from an ASP.NET Core server.
    /// </summary>
    /// <param name="request">The incoming HTTP request representing the webhook message.</param>
    /// <param name="secret">The secret string configured for the EventSub subscription to verify message signatures.</param>
    /// <returns>An <see cref="EventSubWebhookResult"/> containing the processing results and appropriate HTTP response information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>; or <paramref name="secret"/> is <see langword="null"/> or whitespace.</exception>
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
    /// 403 Forbidden: Returned when signature verification fails, required headers are missing, or the message timestamp is expired.
    /// </para>
    /// <para>
    /// Duplicate detection is maintained in-memory per process (not shared across multiple server instances) with a 10-minute window.
    /// </para>
    /// </remarks>
    public static async Task<EventSubWebhookResult> ProcessWebhook(HttpRequest request, string secret)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentNullException(nameof(secret)).Log(TwitchApi.GetLogger());
        }

        using MemoryStream memoryStream = new();
        await request.Body.CopyToAsync(memoryStream).ConfigureAwait(false);
        byte[] bodyBytes = memoryStream.ToArray();

        string? messageId = request.Headers[_messageIdHeader].FirstOrDefault();
        string? messageTimestamp = request.Headers[_messageTimestampHeader].FirstOrDefault();
        string? messageSignature = request.Headers[_messageSignatureHeader].FirstOrDefault();
        string? messageType = request.Headers[_messageTypeHeader].FirstOrDefault();
        string? subscriptionType = request.Headers[_subscriptionTypeHeader].FirstOrDefault();
        string? subscriptionVersion = request.Headers[_subscriptionVersionHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(messageTimestamp) ||
            string.IsNullOrWhiteSpace(messageSignature) ||
            string.IsNullOrWhiteSpace(messageType))
        {
            return new()
            {
                IsVerified = false,
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        if (!DateTime.TryParse(messageTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsedTimestamp))
        {
            return new()
            {
                IsVerified = false,
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        if (DateTime.UtcNow - parsedTimestamp > _messageMaxAge)
        {
            return new()
            {
                IsVerified = false,
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

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
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        if (string.Equals(messageType, "webhook_callback_verification", StringComparison.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(bodyBytes);
            string? challenge = document.RootElement.TryGetProperty("challenge", out JsonElement challengeElement)
                ? challengeElement.GetString()
                : null;

            return new()
            {
                IsVerified = true,
                StatusCode = StatusCodes.Status200OK,
                ContentType = "text/plain",
                Body = challenge,
                Event = null
            };
        }

        PruneSeenMessages();

        if (!_seenMessageIds.TryAdd(messageId, DateTime.UtcNow))
        {
            return new()
            {
                IsVerified = true,
                IsDuplicate = true,
                StatusCode = StatusCodes.Status202Accepted,
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

        object? eventData = null;
        if (!string.IsNullOrWhiteSpace(subscriptionType) &&
            _subscriptionTypeCache.TryGetValue(subscriptionType, out Func<byte[], EventMetadata, object>? factory))
        {
            eventData = factory(bodyBytes, metadata);
        }

        return new()
        {
            IsVerified = true,
            IsDuplicate = false,
            StatusCode = StatusCodes.Status202Accepted,
            Event = eventData
        };
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Builds the cache mapping subscription type strings to deserialization factory delegates.
    /// </summary>
    /// <returns>A frozen dictionary mapping subscription types to factory delegates.</returns>
    private static FrozenDictionary<string, Func<byte[], EventMetadata, object>> BuildSubscriptionTypeCache()
    {
        Dictionary<string, Func<byte[], EventMetadata, object>> cache = [];
        MethodInfo deserializeMethod = typeof(EventSub).GetMethod(nameof(DeserializeEventSubEvent), BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (Type type in typeof(EventSub).Assembly.GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && !type.IsGenericType && typeof(IEventSubType).IsAssignableFrom(type))
            {
                PropertyInfo? typeProperty = type.GetProperty(nameof(IEventSubType.Type), BindingFlags.Public | BindingFlags.Static);
                if (typeProperty?.GetValue(null) is string subscriptionType && !string.IsNullOrWhiteSpace(subscriptionType))
                {
                    MethodInfo genericMethod = deserializeMethod.MakeGenericMethod(type);
                    Func<byte[], EventMetadata, object> handler = genericMethod.CreateDelegate<Func<byte[], EventMetadata, object>>();
                    cache[subscriptionType] = handler;
                }
            }
        }

        return cache.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deserializes the body into an <see cref="EventSubEvent{TEventType}"/> with the specified metadata.
    /// </summary>
    /// <typeparam name="TEventType">The event payload type.</typeparam>
    /// <param name="bodyBytes">The raw webhook request body bytes.</param>
    /// <param name="metadata">The metadata extracted from request headers.</param>
    /// <returns>A boxed <see cref="EventSubEvent{TEventType}"/> instance.</returns>
    private static EventSubEvent<TEventType> DeserializeEventSubEvent<TEventType>(byte[] bodyBytes, EventMetadata metadata) where TEventType : IEventSubType
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
    /// <remarks>
    /// <para>
    /// This method is amortized to execute at most once per <see cref="_pruneInterval"/>.
    /// </para>
    /// <para>
    /// Thread safety is ensured using <see cref="Interlocked.CompareExchange(ref long, long, long)"/> so that only one caller performs the cleanup, removing entries whose first-seen time is older than <see cref="_messageMaxAge"/>.
    /// </para>
    /// </remarks>
    private static void PruneSeenMessages()
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        long lastPruneTicks = Volatile.Read(ref _lastPruneTicks);

        if ((nowTicks - lastPruneTicks) < _pruneInterval.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastPruneTicks, nowTicks, lastPruneTicks) != lastPruneTicks)
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow - _messageMaxAge;
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

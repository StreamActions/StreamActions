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
using Microsoft.AspNetCore.Http;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.EventSub.Channel;
using StreamActions.Twitch.EventSub.Common;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using EventSubService = StreamActions.Twitch.EventSub.EventSub;

namespace StreamActions.Twitch.Tests.EventSub;

/// <summary>
/// Performs unit and integration testing on EventSub webhook verification and processing functionality.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class EventSubWebhookTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubWebhookTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public EventSubWebhookTests(TwitchKestrelServerFixture fixture)
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

    #region ProcessWebhook

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await EventSubService.ProcessWebhook(null!, "secret").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("request");
    }

    [Theory]
    [Trait("Member", "ProcessWebhook")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessWebhook_WhenSecretIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidSecret)
    {
        DefaultHttpContext context = new();
        Func<Task> action = async () => await EventSubService.ProcessWebhook(context.Request, invalidSecret!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("secret");
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenVerificationDirectCall_ReturnsOkWithChallengeBody()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string challenge = "pogchamp-challenge-token-12345";
        string body = $$$"""{"challenge":"{{{challenge}}}","subscription":{"id":"sub-1","status":"webhook_callback_verification_pending","type":"channel.follow","version":"2","cost":0}}""";

        DefaultHttpContext context = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "webhook_callback_verification",
            body);

        EventSubWebhookResult result = await EventSubService.ProcessWebhook(context.Request, this._fixture.WebhookSecret).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsVerified.Should().BeTrue();
        _ = result.StatusCode.Should().Be(StatusCodes.Status200OK);
        _ = result.ContentType.Should().Be("text/plain");
        _ = result.Body.Should().Be(challenge);
        _ = result.Event.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenVerificationHttpPost_ReturnsOkWithChallengeBody()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string challenge = "pogchamp-challenge-token-67890";
        string body = $$$"""{"challenge":"{{{challenge}}}","subscription":{"id":"sub-1","status":"webhook_callback_verification_pending","type":"channel.follow","version":"2","cost":0}}""";

        using HttpClient client = new();
        using HttpRequestMessage request = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "webhook_callback_verification",
            body);

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = responseBody.Should().Be(challenge);
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenNotificationDirectCall_ReturnsAcceptedWithParsedEvent()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = $$"""
        {
          "subscription": {
            "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
            "type": "channel.follow",
            "version": "2",
            "status": "enabled",
            "cost": 0,
            "condition": {
              "broadcaster_user_id": "1337",
              "moderator_user_id": "1337"
            },
            "transport": {
              "method": "webhook",
              "callback": "https://example.com/webhooks/callback"
            },
            "created_at": "2023-04-11T10:11:12.123Z"
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
        """;

        DefaultHttpContext context = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "notification",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        EventSubWebhookResult result = await EventSubService.ProcessWebhook(context.Request, this._fixture.WebhookSecret).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsVerified.Should().BeTrue();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        _ = result.Event.Should().NotBeNull();
        _ = result.Event.Should().BeOfType<EventSubEvent<Follow>>();

        EventSubEvent<Follow>? followEvent = result.Event as EventSubEvent<Follow>;
        _ = followEvent.Should().NotBeNull();
        _ = followEvent.Payload.Should().NotBeNull();
        _ = followEvent.Payload.Event.Should().NotBeNull();
        _ = followEvent.Payload.Event.UserId.Should().Be("1234");
        _ = followEvent.Payload.Event.BroadcasterUserId.Should().Be("1337");
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenNotificationHttpPost_ReturnsAccepted()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = $$"""
        {
          "subscription": {
            "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
            "type": "channel.follow",
            "version": "2",
            "status": "enabled",
            "cost": 0,
            "condition": {
              "broadcaster_user_id": "1337",
              "moderator_user_id": "1337"
            },
            "transport": {
              "method": "webhook",
              "callback": "https://example.com/webhooks/callback"
            },
            "created_at": "2023-04-11T10:11:12.123Z"
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
        """;

        using HttpClient client = new();
        using HttpRequestMessage request = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "notification",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenRevocationDirectCall_ReturnsAcceptedWithSubscription()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = $$"""
        {
          "subscription": {
            "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
            "type": "channel.follow",
            "version": "2",
            "status": "authorization_revoked",
            "cost": 0,
            "condition": {
              "broadcaster_user_id": "1337",
              "moderator_user_id": "1337"
            },
            "transport": {
              "method": "webhook",
              "callback": "https://example.com/webhooks/callback"
            },
            "created_at": "2023-04-11T10:11:12.123Z"
          }
        }
        """;

        DefaultHttpContext context = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "revocation",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        EventSubWebhookResult result = await EventSubService.ProcessWebhook(context.Request, this._fixture.WebhookSecret).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.IsVerified.Should().BeTrue();
        _ = result.IsDuplicate.Should().BeFalse();
        _ = result.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        _ = result.Event.Should().NotBeNull();

        EventSubEvent? eventSubEvent = result.Event as EventSubEvent;
        _ = eventSubEvent.Should().NotBeNull();
        _ = eventSubEvent.Payload.Should().NotBeNull();
        _ = eventSubEvent.Payload.Subscription.Should().NotBeNull();
        _ = eventSubEvent.Payload.Subscription.Status.Should().Be(EventSubSubscription.SubscriptionStatus.AuthorizationRevoked);
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenRevocationHttpPost_ReturnsAccepted()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = $$"""
        {
          "subscription": {
            "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
            "type": "channel.follow",
            "version": "2",
            "status": "authorization_revoked",
            "cost": 0,
            "condition": {
              "broadcaster_user_id": "1337",
              "moderator_user_id": "1337"
            },
            "transport": {
              "method": "webhook",
              "callback": "https://example.com/webhooks/callback"
            },
            "created_at": "2023-04-11T10:11:12.123Z"
          }
        }
        """;

        using HttpClient client = new();
        using HttpRequestMessage request = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "revocation",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenDuplicateMessageIdDirectCall_ReturnsAcceptedWithNullEvent()
    {
        string duplicateMessageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = $$"""
        {
          "subscription": {
            "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
            "type": "channel.follow",
            "version": "2",
            "status": "enabled",
            "cost": 0,
            "condition": {
              "broadcaster_user_id": "1337",
              "moderator_user_id": "1337"
            },
            "transport": {
              "method": "webhook",
              "callback": "https://example.com/webhooks/callback"
            },
            "created_at": "2023-04-11T10:11:12.123Z"
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
        """;

        DefaultHttpContext context1 = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            duplicateMessageId,
            timestamp,
            "notification",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        EventSubWebhookResult firstResult = await EventSubService.ProcessWebhook(context1.Request, this._fixture.WebhookSecret).ConfigureAwait(true);
        _ = firstResult.IsDuplicate.Should().BeFalse();
        _ = firstResult.Event.Should().NotBeNull();

        DefaultHttpContext context2 = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            duplicateMessageId,
            timestamp,
            "notification",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        EventSubWebhookResult secondResult = await EventSubService.ProcessWebhook(context2.Request, this._fixture.WebhookSecret).ConfigureAwait(true);
        _ = secondResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        _ = secondResult.IsDuplicate.Should().BeTrue();
        _ = secondResult.Event.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenDuplicateMessageIdHttpPost_ReturnsAccepted()
    {
        string duplicateMessageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = $$"""
        {
          "subscription": {
            "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
            "type": "channel.follow",
            "version": "2",
            "status": "enabled",
            "cost": 0,
            "condition": {
              "broadcaster_user_id": "1337",
              "moderator_user_id": "1337"
            },
            "transport": {
              "method": "webhook",
              "callback": "https://example.com/webhooks/callback"
            },
            "created_at": "2023-04-11T10:11:12.123Z"
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
        """;

        using HttpClient client = new();

        using HttpRequestMessage request1 = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            duplicateMessageId,
            timestamp,
            "notification",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        HttpResponseMessage response1 = await client.SendAsync(request1, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = response1.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using HttpRequestMessage request2 = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            duplicateMessageId,
            timestamp,
            "notification",
            body,
            subscriptionType: "channel.follow",
            subscriptionVersion: "2");

        HttpResponseMessage response2 = await client.SendAsync(request2, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = response2.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Theory]
    [Trait("Member", "ProcessWebhook")]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public async Task ProcessWebhook_WhenMissingRequiredHeaderDirectCall_ReturnsForbidden(
        bool omitId,
        bool omitTimestamp,
        bool omitSignature,
        bool omitType)
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = "{\"test\":true}";

        DefaultHttpContext context = new();
        if (!omitId)
        {
            context.Request.Headers["Twitch-Eventsub-Message-Id"] = messageId;
        }

        if (!omitTimestamp)
        {
            context.Request.Headers["Twitch-Eventsub-Message-Timestamp"] = timestamp;
        }

        if (!omitType)
        {
            context.Request.Headers["Twitch-Eventsub-Message-Type"] = "notification";
        }

        if (!omitSignature)
        {
            context.Request.Headers["Twitch-Eventsub-Message-Signature"] = ComputeHmacSha256(this._fixture.WebhookSecret, messageId, timestamp, body);
        }

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);

        EventSubWebhookResult result = await EventSubService.ProcessWebhook(context.Request, this._fixture.WebhookSecret).ConfigureAwait(true);

        _ = result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _ = result.IsVerified.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenMissingRequiredHeaderHttpPost_ReturnsForbidden()
    {
        string body = "{\"test\":true}";

        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Post, $"{this._fixture.BaseUrl}/webhook");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenTimestampExpiredDirectCall_ReturnsForbidden()
    {
        string messageId = Guid.NewGuid().ToString();
        string expiredTimestamp = DateTime.UtcNow.AddMinutes(-15).ToString("o", CultureInfo.InvariantCulture);
        string body = "{\"test\":true}";

        DefaultHttpContext context = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            messageId,
            expiredTimestamp,
            "notification",
            body);

        EventSubWebhookResult result = await EventSubService.ProcessWebhook(context.Request, this._fixture.WebhookSecret).ConfigureAwait(true);

        _ = result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _ = result.IsVerified.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenTimestampExpiredHttpPost_ReturnsForbidden()
    {
        string messageId = Guid.NewGuid().ToString();
        string expiredTimestamp = DateTime.UtcNow.AddMinutes(-15).ToString("o", CultureInfo.InvariantCulture);
        string body = "{\"test\":true}";

        using HttpClient client = new();
        using HttpRequestMessage request = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            messageId,
            expiredTimestamp,
            "notification",
            body);

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenInvalidSignatureDirectCall_ReturnsForbidden()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = "{\"test\":true}";

        DefaultHttpContext context = CreateWebhookHttpContext(
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "notification",
            body,
            signatureOverride: "sha256=0000000000000000000000000000000000000000000000000000000000000000");

        EventSubWebhookResult result = await EventSubService.ProcessWebhook(context.Request, this._fixture.WebhookSecret).ConfigureAwait(true);

        _ = result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _ = result.IsVerified.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ProcessWebhook")]
    public async Task ProcessWebhook_WhenInvalidSignatureHttpPost_ReturnsForbidden()
    {
        string messageId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string body = "{\"test\":true}";

        using HttpClient client = new();
        using HttpRequestMessage request = CreateHttpRequestMessage(
            $"{this._fixture.BaseUrl}/webhook",
            this._fixture.WebhookSecret,
            messageId,
            timestamp,
            "notification",
            body,
            signatureOverride: "sha256=0000000000000000000000000000000000000000000000000000000000000000");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion ProcessWebhook

    #region Private Helper Methods

    /// <summary>
    /// Computes the HMAC-SHA256 signature for an EventSub webhook message.
    /// </summary>
    /// <param name="secret">The webhook secret key.</param>
    /// <param name="messageId">The message ID header value.</param>
    /// <param name="timestamp">The message timestamp header value.</param>
    /// <param name="body">The raw body string.</param>
    /// <returns>The formatted signature starting with <c>sha256=</c>.</returns>
    private static string ComputeHmacSha256(string secret, string messageId, string timestamp, string body)
    {
        using IncrementalHash hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret));
        hmac.AppendData(Encoding.UTF8.GetBytes(messageId));
        hmac.AppendData(Encoding.UTF8.GetBytes(timestamp));
        hmac.AppendData(Encoding.UTF8.GetBytes(body));
        byte[] hash = hmac.GetHashAndReset();
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Creates a simulated incoming <see cref="DefaultHttpContext"/> populated with standard EventSub headers.
    /// </summary>
    /// <param name="secret">The webhook secret key.</param>
    /// <param name="messageId">The message ID header value.</param>
    /// <param name="timestamp">The message timestamp header value.</param>
    /// <param name="messageType">The message type header value.</param>
    /// <param name="body">The raw body string.</param>
    /// <param name="subscriptionType">Optional subscription type header value.</param>
    /// <param name="subscriptionVersion">Optional subscription version header value.</param>
    /// <param name="signatureOverride">Optional signature override to test invalid signatures.</param>
    /// <returns>A configured <see cref="DefaultHttpContext"/> instance.</returns>
    private static DefaultHttpContext CreateWebhookHttpContext(
        string secret,
        string messageId,
        string timestamp,
        string messageType,
        string body,
        string? subscriptionType = null,
        string? subscriptionVersion = null,
        string? signatureOverride = null)
    {
        DefaultHttpContext context = new();
        context.Request.Headers["Twitch-Eventsub-Message-Id"] = messageId;
        context.Request.Headers["Twitch-Eventsub-Message-Timestamp"] = timestamp;
        context.Request.Headers["Twitch-Eventsub-Message-Type"] = messageType;

        if (subscriptionType is not null)
        {
            context.Request.Headers["Twitch-Eventsub-Subscription-Type"] = subscriptionType;
        }

        if (subscriptionVersion is not null)
        {
            context.Request.Headers["Twitch-Eventsub-Subscription-Version"] = subscriptionVersion;
        }

        string signature = signatureOverride ?? ComputeHmacSha256(secret, messageId, timestamp, body);
        context.Request.Headers["Twitch-Eventsub-Message-Signature"] = signature;

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.ContentType = "application/json";

        return context;
    }

    /// <summary>
    /// Creates an <see cref="HttpRequestMessage"/> configured with EventSub headers for sending to the test server.
    /// </summary>
    /// <param name="url">The destination URL.</param>
    /// <param name="secret">The webhook secret key.</param>
    /// <param name="messageId">The message ID header value.</param>
    /// <param name="timestamp">The message timestamp header value.</param>
    /// <param name="messageType">The message type header value.</param>
    /// <param name="body">The raw body string.</param>
    /// <param name="subscriptionType">Optional subscription type header value.</param>
    /// <param name="subscriptionVersion">Optional subscription version header value.</param>
    /// <param name="signatureOverride">Optional signature override to test invalid signatures.</param>
    /// <returns>A configured <see cref="HttpRequestMessage"/> instance.</returns>
    private static HttpRequestMessage CreateHttpRequestMessage(
        string url,
        string secret,
        string messageId,
        string timestamp,
        string messageType,
        string body,
        string? subscriptionType = null,
        string? subscriptionVersion = null,
        string? signatureOverride = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.Add("Twitch-Eventsub-Message-Id", messageId);
        request.Headers.Add("Twitch-Eventsub-Message-Timestamp", timestamp);
        request.Headers.Add("Twitch-Eventsub-Message-Type", messageType);

        if (subscriptionType is not null)
        {
            request.Headers.Add("Twitch-Eventsub-Subscription-Type", subscriptionType);
        }

        if (subscriptionVersion is not null)
        {
            request.Headers.Add("Twitch-Eventsub-Subscription-Version", subscriptionVersion);
        }

        string signature = signatureOverride ?? ComputeHmacSha256(secret, messageId, timestamp, body);
        request.Headers.Add("Twitch-Eventsub-Message-Signature", signature);

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }

    #endregion Private Helper Methods
}

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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using StreamActions.Twitch.Api.EventSub.Objects;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.EventSub;

/// <summary>
/// Provides unit tests for the <see cref="EventSubSubscription"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class EventSubSubscriptionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubSubscriptionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public EventSubSubscriptionTests(TwitchKestrelServerFixture fixture)
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

    #region GetEventSubSubscriptions

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.GetEventSubSubscriptions"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEventSubSubscriptions")]
    public async Task GetEventSubSubscriptions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await EventSubSubscription.GetEventSubSubscriptions(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.GetEventSubSubscriptions"/> throws <see cref="InvalidOperationException"/> when multiple filters are defined.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEventSubSubscriptions")]
    public async Task GetEventSubSubscriptions_WhenMultipleFiltersDefined_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await EventSubSubscription.GetEventSubSubscriptions(
            session,
            status: EventSubSubscription.SubscriptionStatus.Enabled,
            type: "channel.update").ConfigureAwait(true);

        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.GetEventSubSubscriptions"/> returns subscriptions when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEventSubSubscriptions")]
    public async Task GetEventSubSubscriptions_WhenCalledWithValidParameters_ReturnsSubscriptions()
    {
        Guid subId = Guid.Parse("f1c2a3b4-5678-90ab-cdef-1234567890ab");
        DateTime createdAt = new(2023, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("GET", "/helix/eventsub/subscriptions", new
        {
            data = new[]
            {
                new
                {
                    id = subId.ToString(),
                    status = "enabled",
                    type = "channel.update",
                    version = "2",
                    created_at = createdAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    cost = 1,
                    transport = new
                    {
                        method = "webhook",
                        callback = "https://example.com/callback"
                    }
                }
            },
            pagination = new
            {
                cursor = "cursor_next"
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<EventSubSubscription>? response = await EventSubSubscription.GetEventSubSubscriptions(
            session,
            status: EventSubSubscription.SubscriptionStatus.Enabled,
            after: "cursor_prev").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        EventSubSubscription sub = response.Data[0];
        _ = sub.Id.Should().Be(subId);
        _ = sub.Status.Should().Be(EventSubSubscription.SubscriptionStatus.Enabled);
        _ = sub.Type.Should().Be("channel.update");
        _ = sub.Version.Should().Be("2");
        _ = sub.CreatedAt.Should().Be(createdAt);
        _ = sub.Cost.Should().Be(1);
        _ = sub.Transport.Should().NotBeNull();
        _ = sub.Transport!.Method.Should().Be(EventSubTransport.TransportMethod.Webhook);
        _ = sub.Transport.Callback.Should().Be("https://example.com/callback");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/eventsub/subscriptions");
        _ = request.Query["status"].ToString().Should().Be("enabled");
        _ = request.Query["after"].ToString().Should().Be("cursor_prev");
    }

    #endregion GetEventSubSubscriptions

    #region CreateEventSubSubscription

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = "https://example.com/cb", Secret = "secret12345678" }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Type is null or empty.
    /// </summary>
    /// <param name="invalidType">The invalid type value.</param>
    [Theory]
    [Trait("Member", "CreateEventSubSubscription")]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateEventSubSubscription_WhenTypeIsNullOrEmpty_ThrowsArgumentNullException(string? invalidType)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = invalidType,
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = "https://example.com/cb", Secret = "secret12345678" }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Type");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Version is null or empty.
    /// </summary>
    /// <param name="invalidVersion">The invalid version value.</param>
    [Theory]
    [Trait("Member", "CreateEventSubSubscription")]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateEventSubSubscription_WhenVersionIsNullOrEmpty_ThrowsArgumentNullException(string? invalidVersion)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = invalidVersion,
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = "https://example.com/cb", Secret = "secret12345678" }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Version");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Condition is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenConditionIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = null,
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = "https://example.com/cb", Secret = "secret12345678" }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Condition");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Transport is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenTransportIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = null
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Transport");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Method is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenTransportMethodIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = null }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Method");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when ConduitId is null or empty for conduit transport.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenConduitTransportMissingConduitId_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters nullIdParams = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Conduit, ConduitId = null }
        };
        Func<Task> actionNull = async () => await EventSubSubscription.CreateEventSubSubscription(session, nullIdParams).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ConduitId");

        EventSubSubscriptionCreationParameters emptyIdParams = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Conduit, ConduitId = Guid.Empty }
        };
        Func<Task> actionEmpty = async () => await EventSubSubscription.CreateEventSubSubscription(session, emptyIdParams).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ConduitId");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Webhook transport is missing callback or secret.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenWebhookMissingCallbackOrSecret_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters missingCallback = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = null, Secret = "secret123456" }
        };
        Func<Task> actionCallback = async () => await EventSubSubscription.CreateEventSubSubscription(session, missingCallback).ConfigureAwait(true);
        _ = (await actionCallback.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Callback");

        EventSubSubscriptionCreationParameters missingSecret = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = "https://example.com/cb", Secret = null }
        };
        Func<Task> actionSecret = async () => await EventSubSubscription.CreateEventSubSubscription(session, missingSecret).ConfigureAwait(true);
        _ = (await actionSecret.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Secret");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentOutOfRangeException"/> when secret length is invalid.
    /// </summary>
    /// <param name="invalidSecret">The invalid secret value.</param>
    [Theory]
    [Trait("Member", "CreateEventSubSubscription")]
    [InlineData("123456789")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    public async Task CreateEventSubSubscription_WhenSecretLengthIsInvalid_ThrowsArgumentOutOfRangeException(string invalidSecret)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Webhook, Callback = "https://example.com/cb", Secret = invalidSecret }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Secret");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when Websocket transport is missing SessionId.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenWebsocketMissingSessionId_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new() { Method = EventSubTransport.TransportMethod.Websocket, SessionId = null }
        };

        Func<Task> action = async () => await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("SessionId");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.CreateEventSubSubscription"/> creates a subscription when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateEventSubSubscription")]
    public async Task CreateEventSubSubscription_WhenCalledWithValidParameters_ReturnsCreatedSubscription()
    {
        Guid subId = Guid.Parse("f1c2a3b4-5678-90ab-cdef-1234567890ab");
        DateTime createdAt = new(2023, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        this._fixture.RegisterJsonResponse("POST", "/helix/eventsub/subscriptions", new
        {
            data = new[]
            {
                new
                {
                    id = subId.ToString(),
                    status = "webhook_callback_verification_pending",
                    type = "channel.update",
                    version = "2",
                    created_at = createdAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    cost = 1,
                    transport = new
                    {
                        method = "webhook",
                        callback = "https://example.com/callback"
                    }
                }
            }
        }, HttpStatusCode.Accepted);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        EventSubSubscriptionCreationParameters parameters = new()
        {
            Type = "channel.update",
            Version = "2",
            Condition = new BroadcasterUserIdCondition { BroadcasterUserId = "12345" },
            Transport = new()
            {
                Method = EventSubTransport.TransportMethod.Webhook,
                Callback = "https://example.com/callback",
                Secret = "supersecret12345"
            }
        };

        ResponseData<EventSubSubscription>? response = await EventSubSubscription.CreateEventSubSubscription(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.Accepted);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        EventSubSubscription sub = response.Data[0];
        _ = sub.Id.Should().Be(subId);
        _ = sub.Status.Should().Be(EventSubSubscription.SubscriptionStatus.WebhookCallbackVerificationPending);
        _ = sub.Type.Should().Be("channel.update");
        _ = sub.Version.Should().Be("2");
        _ = sub.CreatedAt.Should().Be(createdAt);
        _ = sub.Cost.Should().Be(1);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/eventsub/subscriptions");
        _ = request.Body.Should().Contain("\"type\":\"channel.update\"");
        _ = request.Body.Should().Contain("\"version\":\"2\"");
        _ = request.Body.Should().Contain("\"broadcaster_user_id\":\"12345\"");
    }

    #endregion CreateEventSubSubscription

    #region DeleteEventSubSubscription

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.DeleteEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteEventSubSubscription")]
    public async Task DeleteEventSubSubscription_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await EventSubSubscription.DeleteEventSubSubscription(null!, Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.DeleteEventSubSubscription"/> throws <see cref="ArgumentNullException"/> when id is Guid.Empty.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteEventSubSubscription")]
    public async Task DeleteEventSubSubscription_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await EventSubSubscription.DeleteEventSubSubscription(session, Guid.Empty).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="EventSubSubscription.DeleteEventSubSubscription"/> deletes a subscription when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteEventSubSubscription")]
    public async Task DeleteEventSubSubscription_WhenCalledWithValidParameters_ReturnsSuccess()
    {
        Guid subId = Guid.Parse("f1c2a3b4-5678-90ab-cdef-1234567890ab");
        this._fixture.RegisterResponse("DELETE", "/helix/eventsub/subscriptions", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        JsonApiResponse? response = await EventSubSubscription.DeleteEventSubSubscription(session, subId).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/eventsub/subscriptions");
        _ = request.Query["id"].ToString().Should().Be(subId.ToString());
    }

    #endregion DeleteEventSubSubscription
}

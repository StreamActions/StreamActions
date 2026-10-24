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
using StreamActions.Twitch.Api.Subscriptions;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.Subscriptions;

/// <summary>
/// Provides unit tests for the <see cref="Subscription"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class SubscriptionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public SubscriptionTests(TwitchKestrelServerFixture fixture)
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

    #region GetBroadcasterSubscriptions

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(null!, "141981764").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBroadcasterSubscriptions_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadSubscriptions);
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, invalidBroadcasterId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="ArgumentOutOfRangeException"/> when userId exceeds 100 items.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenUserIdExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadSubscriptions);
        List<string> userIds = Enumerable.Range(1, 101).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, "141981764", userId: userIds).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="InvalidOperationException"/> when userId and after are both specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenUserIdAndAfterSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadSubscriptions);
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, "141981764", userId: ["123"], after: "cur1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="InvalidOperationException"/> when userId and before are both specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenUserIdAndBeforeSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadSubscriptions);
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, "141981764", userId: ["123"], before: "cur1").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="InvalidOperationException"/> when before and after are both specified.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenBeforeAndAfterSpecified_ThrowsInvalidOperationException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadSubscriptions);
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, "141981764", before: "cur1", after: "cur2").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> throws <see cref="TwitchScopeMissingException"/> when ChannelReadSubscriptions scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await Subscription.GetBroadcasterSubscriptions(session, "141981764").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.GetBroadcasterSubscriptions"/> returns subscriptions on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "GetBroadcasterSubscriptions")]
    public async Task GetBroadcasterSubscriptions_WhenRequestSucceeds_ReturnsSubscriptions()
    {
        this._fixture.RegisterEndpoint("/helix/subscriptions", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""broadcaster_id"": ""141981764"",
      ""broadcaster_login"": ""twitchdev"",
      ""broadcaster_name"": ""TwitchDev"",
      ""gifter_id"": ""12826"",
      ""gifter_login"": ""twitch"",
      ""gifter_name"": ""Twitch"",
      ""is_gift"": true,
      ""tier"": 1000,
      ""plan_name"": ""Channel Subscription (twitchdev)"",
      ""user_id"": ""527115020"",
      ""user_name"": ""twitchgaming"",
      ""user_login"": ""twitchgaming""
    }
  ],
  ""pagination"": {
    ""cursor"": ""cursorSub""
  },
  ""total"": 13,
  ""points"": 13
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadSubscriptions);
        SubscriptionResponse? response = await Subscription.GetBroadcasterSubscriptions(session, "141981764", first: 20, after: "cursorAfter").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Total.Should().Be(13);
        _ = response.Points.Should().Be(13);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Subscription sub = response.Data[0];
        _ = sub.BroadcasterId.Should().Be("141981764");
        _ = sub.BroadcasterLogin.Should().Be("twitchdev");
        _ = sub.BroadcasterName.Should().Be("TwitchDev");
        _ = sub.GifterId.Should().Be("12826");
        _ = sub.GifterLogin.Should().Be("twitch");
        _ = sub.GifterName.Should().Be("Twitch");
        _ = sub.IsGift.Should().BeTrue();
        _ = sub.Tier.Should().Be(Subscription.SubscriptionTier.Tier1);
        _ = sub.PlanName.Should().Be("Channel Subscription (twitchdev)");
        _ = sub.UserId.Should().Be("527115020");
        _ = sub.UserName.Should().Be("twitchgaming");
        _ = sub.UserLogin.Should().Be("twitchgaming");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/subscriptions");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain("after=cursorAfter");
    }

    #endregion GetBroadcasterSubscriptions

    #region CheckUserSubscription

    /// <summary>
    /// Tests that <see cref="Subscription.CheckUserSubscription"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CheckUserSubscription")]
    public async Task CheckUserSubscription_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Subscription.CheckUserSubscription(null!, "141981764", "527115020").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Subscription.CheckUserSubscription"/> throws <see cref="ArgumentNullException"/> when broadcasterId is null or whitespace.
    /// </summary>
    /// <param name="invalidBroadcasterId">The invalid broadcaster ID.</param>
    [Theory]
    [Trait("Member", "CheckUserSubscription")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckUserSubscription_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidBroadcasterId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadSubscriptions);
        Func<Task> action = async () => await Subscription.CheckUserSubscription(session, invalidBroadcasterId!, "527115020").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    /// <summary>
    /// Tests that <see cref="Subscription.CheckUserSubscription"/> throws <see cref="ArgumentNullException"/> when userId is null or whitespace.
    /// </summary>
    /// <param name="invalidUserId">The invalid user ID.</param>
    [Theory]
    [Trait("Member", "CheckUserSubscription")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckUserSubscription_WhenUserIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidUserId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadSubscriptions);
        Func<Task> action = async () => await Subscription.CheckUserSubscription(session, "141981764", invalidUserId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("userId");
    }

    /// <summary>
    /// Tests that <see cref="Subscription.CheckUserSubscription"/> throws <see cref="TokenTypeException"/> when session is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "CheckUserSubscription")]
    public async Task CheckUserSubscription_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Subscription.CheckUserSubscription(session, "141981764", "527115020").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.CheckUserSubscription"/> throws <see cref="TwitchScopeMissingException"/> when UserReadSubscriptions scope is missing.
    /// </summary>
    [Fact]
    [Trait("Member", "CheckUserSubscription")]
    public async Task CheckUserSubscription_WhenScopeIsMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadEmail);
        Func<Task> action = async () => await Subscription.CheckUserSubscription(session, "141981764", "527115020").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Subscription.CheckUserSubscription"/> returns user subscription on a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "CheckUserSubscription")]
    public async Task CheckUserSubscription_WhenRequestSucceeds_ReturnsSubscription()
    {
        this._fixture.RegisterEndpoint("/helix/subscriptions/user", HttpStatusCode.OK, @"{
  ""data"": [
    {
      ""broadcaster_id"": ""141981764"",
      ""broadcaster_login"": ""twitchdev"",
      ""broadcaster_name"": ""TwitchDev"",
      ""is_gift"": false,
      ""tier"": 1000
    }
  ]
}");

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.UserReadSubscriptions);
        SubscriptionResponse? response = await Subscription.CheckUserSubscription(session, "141981764", "527115020").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response!.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data!.Count.Should().Be(1);

        Subscription sub = response.Data[0];
        _ = sub.BroadcasterId.Should().Be("141981764");
        _ = sub.BroadcasterLogin.Should().Be("twitchdev");
        _ = sub.BroadcasterName.Should().Be("TwitchDev");
        _ = sub.IsGift.Should().BeFalse();
        _ = sub.Tier.Should().Be(Subscription.SubscriptionTier.Tier1);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/subscriptions/user");
        _ = request.QueryString.Should().Contain("broadcaster_id=141981764");
        _ = request.QueryString.Should().Contain("user_id=527115020");
    }

    #endregion CheckUserSubscription
}

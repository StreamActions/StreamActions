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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Objects;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using Xunit;

namespace StreamActions.Twitch.Tests.Api.EventSub;

/// <summary>
/// Provides unit tests for the <see cref="ConduitShard"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ConduitShardTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConduitShardTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ConduitShardTests(TwitchKestrelServerFixture fixture)
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

    #region GetConduitShards

    /// <summary>
    /// Tests that <see cref="ConduitShard.GetConduitShards"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduitShards")]
    public async Task GetConduitShards_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await ConduitShard.GetConduitShards(null!, Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.GetConduitShards"/> throws <see cref="ArgumentNullException"/> when conduitId is Guid.Empty.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduitShards")]
    public async Task GetConduitShards_WhenConduitIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ConduitShard.GetConduitShards(session, Guid.Empty).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("conduitId");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.GetConduitShards"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduitShards")]
    public async Task GetConduitShards_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await ConduitShard.GetConduitShards(session, Guid.NewGuid()).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.GetConduitShards"/> returns shards when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduitShards")]
    public async Task GetConduitShards_WhenCalledWithValidParameters_ReturnsShards()
    {
        Guid conduitId = Guid.Parse("bf17725e-3991-4d1a-9694-845b412a8069");
        this._fixture.RegisterJsonResponse("GET", "/helix/eventsub/conduits/shards", new
        {
            data = new[]
            {
                new
                {
                    id = "0",
                    status = "enabled",
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
        ResponseData<ConduitShard>? response = await ConduitShard.GetConduitShards(
            session,
            conduitId,
            status: ConduitShard.ShardStatus.Enabled,
            after: "cursor_prev").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ConduitShard shard = response.Data[0];
        _ = shard.Id.Should().Be("0");
        _ = shard.Status.Should().Be(ConduitShard.ShardStatus.Enabled);
        _ = shard.Transport.Should().NotBeNull();
        _ = shard.Transport.Method.Should().Be(ConduitShardTransport.TransportMethod.Webhook);
        _ = shard.Transport.Callback.Should().Be("https://example.com/callback");
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor_next");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/eventsub/conduits/shards");
        _ = request.Query["conduit_id"].ToString().Should().Be(conduitId.ToString());
        _ = request.Query["status"].ToString().Should().Be("enabled");
        _ = request.Query["after"].ToString().Should().Be("cursor_prev");
    }

    #endregion GetConduitShards

    #region UpdateConduitShards

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = Guid.NewGuid(),
            Shards = []
        };

        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentNullException"/> when ConduitId is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenConduitIdIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitShardUpdateParameters nullParams = new()
        {
            ConduitId = null,
            Shards = []
        };
        Func<Task> actionNull = async () => await ConduitShard.UpdateConduitShards(session, nullParams).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ConduitId");

        ConduitShardUpdateParameters emptyParams = new()
        {
            ConduitId = Guid.Empty,
            Shards = []
        };
        Func<Task> actionEmpty = async () => await ConduitShard.UpdateConduitShards(session, emptyParams).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ConduitId");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentNullException"/> when Shards is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenShardsIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = Guid.NewGuid(),
            Shards = null
        };
        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Shards");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentOutOfRangeException"/> when Shards count exceeds 100.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenShardsCountExceeds100_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        List<ConduitShardUpdateShardParameters> shards = Enumerable.Range(0, 101).Select(i => new ConduitShardUpdateShardParameters
        {
            Id = i.ToString(CultureInfo.InvariantCulture),
            Transport = new() { Method = ConduitShardTransport.TransportMethod.Webhook, Callback = "https://example.com/callback" }
        }).ToList();

        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = Guid.NewGuid(),
            Shards = shards
        };

        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Shards");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentNullException"/> when a shard is missing an ID.
    /// </summary>
    /// <param name="invalidId">The invalid shard ID.</param>
    [Theory]
    [Trait("Member", "UpdateConduitShards")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateConduitShards_WhenShardIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = Guid.NewGuid(),
            Shards =
            [
                new()
                {
                    Id = invalidId,
                    Transport = new() { Method = ConduitShardTransport.TransportMethod.Webhook, Callback = "https://example.com/callback" }
                }
            ]
        };

        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="ArgumentNullException"/> when a shard is missing transport.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenShardTransportIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = Guid.NewGuid(),
            Shards =
            [
                new()
                {
                    Id = "0",
                    Transport = null
                }
            ]
        };

        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Transport");
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = Guid.NewGuid(),
            Shards =
            [
                new()
                {
                    Id = "0",
                    Transport = new() { Method = ConduitShardTransport.TransportMethod.Webhook, Callback = "https://example.com/callback" }
                }
            ]
        };

        Func<Task> action = async () => await ConduitShard.UpdateConduitShards(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="ConduitShard.UpdateConduitShards"/> updates shards when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduitShards")]
    public async Task UpdateConduitShards_WhenCalledWithValidParameters_ReturnsUpdatedShards()
    {
        Guid conduitId = Guid.Parse("bf17725e-3991-4d1a-9694-845b412a8069");
        this._fixture.RegisterJsonResponse("PATCH", "/helix/eventsub/conduits/shards", new
        {
            data = new[]
            {
                new
                {
                    id = "0",
                    status = "enabled",
                    transport = new
                    {
                        method = "webhook",
                        callback = "https://example.com/callback"
                    }
                }
            },
            errors = new[]
            {
                new
                {
                    id = "1",
                    message = "The callback URL is invalid.",
                    code = "invalid_callback"
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitShardUpdateParameters parameters = new()
        {
            ConduitId = conduitId,
            Shards =
            [
                new()
                {
                    Id = "0",
                    Transport = new() { Method = ConduitShardTransport.TransportMethod.Webhook, Callback = "https://example.com/callback" }
                }
            ]
        };

        ConduitShardUpdateResponse? response = await ConduitShard.UpdateConduitShards(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        ConduitShard shard = response.Data[0];
        _ = shard.Id.Should().Be("0");
        _ = shard.Status.Should().Be(ConduitShard.ShardStatus.Enabled);
        _ = shard.Transport.Should().NotBeNull();
        _ = shard.Transport.Method.Should().Be(ConduitShardTransport.TransportMethod.Webhook);

        _ = response.Errors.Should().NotBeNull();
        _ = response.Errors.Count.Should().Be(1);
        ConduitShardUpdateError error = response.Errors[0];
        _ = error.Id.Should().Be("1");
        _ = error.Message.Should().Be("The callback URL is invalid.");
        _ = error.Code.Should().Be("invalid_callback");

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/eventsub/conduits/shards");
        _ = request.Body.Should().Contain(conduitId.ToString());
        _ = request.Body.Should().Contain("\"id\":\"0\"");
    }

    #endregion UpdateConduitShards
}

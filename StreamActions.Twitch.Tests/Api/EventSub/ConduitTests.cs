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
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.EventSub;

/// <summary>
/// Provides unit tests for the <see cref="Conduit"/> class.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class ConduitTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConduitTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public ConduitTests(TwitchKestrelServerFixture fixture)
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

    #region GetConduits

    /// <summary>
    /// Tests that <see cref="Conduit.GetConduits"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduits")]
    public async Task GetConduits_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Conduit.GetConduits(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.GetConduits"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduits")]
    public async Task GetConduits_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Conduit.GetConduits(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Conduit.GetConduits"/> returns conduits when called with a valid app session.
    /// </summary>
    [Fact]
    [Trait("Member", "GetConduits")]
    public async Task GetConduits_WhenCalledWithValidAppSession_ReturnsConduits()
    {
        Guid conduitId = Guid.Parse("bf17725e-3991-4d1a-9694-845b412a8069");
        this._fixture.RegisterJsonResponse("GET", "/helix/eventsub/conduits", new
        {
            data = new[]
            {
                new
                {
                    id = conduitId.ToString(),
                    shard_count = 5
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ResponseData<Conduit>? response = await Conduit.GetConduits(session).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Conduit conduit = response.Data[0];
        _ = conduit.Id.Should().Be(conduitId);
        _ = conduit.ShardCount.Should().Be(5);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/eventsub/conduits");
    }

    #endregion GetConduits

    #region CreateConduits

    /// <summary>
    /// Tests that <see cref="Conduit.CreateConduits"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateConduits")]
    public async Task CreateConduits_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ConduitCreationParameters parameters = new() { ShardCount = 5 };
        Func<Task> action = async () => await Conduit.CreateConduits(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.CreateConduits"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateConduits")]
    public async Task CreateConduits_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Conduit.CreateConduits(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.CreateConduits"/> throws <see cref="ArgumentNullException"/> when ShardCount is null.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateConduits")]
    public async Task CreateConduits_WhenShardCountIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitCreationParameters parameters = new() { ShardCount = null };
        Func<Task> action = async () => await Conduit.CreateConduits(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ShardCount");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.CreateConduits"/> throws <see cref="ArgumentOutOfRangeException"/> when ShardCount is less than 1.
    /// </summary>
    [Theory]
    [Trait("Member", "CreateConduits")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateConduits_WhenShardCountIsLessThanOne_ThrowsArgumentOutOfRangeException(int invalidShardCount)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitCreationParameters parameters = new() { ShardCount = invalidShardCount };
        Func<Task> action = async () => await Conduit.CreateConduits(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("ShardCount");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.CreateConduits"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateConduits")]
    public async Task CreateConduits_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ConduitCreationParameters parameters = new() { ShardCount = 5 };
        Func<Task> action = async () => await Conduit.CreateConduits(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Conduit.CreateConduits"/> creates a conduit when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "CreateConduits")]
    public async Task CreateConduits_WhenCalledWithValidParameters_ReturnsCreatedConduit()
    {
        Guid conduitId = Guid.Parse("bf17725e-3991-4d1a-9694-845b412a8069");
        this._fixture.RegisterJsonResponse("POST", "/helix/eventsub/conduits", new
        {
            data = new[]
            {
                new
                {
                    id = conduitId.ToString(),
                    shard_count = 10
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitCreationParameters parameters = new() { ShardCount = 10 };
        ResponseData<Conduit>? response = await Conduit.CreateConduits(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Conduit conduit = response.Data[0];
        _ = conduit.Id.Should().Be(conduitId);
        _ = conduit.ShardCount.Should().Be(10);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/eventsub/conduits");
        _ = request.Body.Should().Contain("\"shard_count\":10");
    }

    #endregion CreateConduits

    #region UpdateConduits

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduits")]
    public async Task UpdateConduits_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        ConduitUpdateParameters parameters = new() { Id = Guid.NewGuid(), ShardCount = 8 };
        Func<Task> action = async () => await Conduit.UpdateConduits(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> throws <see cref="ArgumentNullException"/> when parameters is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduits")]
    public async Task UpdateConduits_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Conduit.UpdateConduits(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> throws <see cref="ArgumentNullException"/> when Id is null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduits")]
    public async Task UpdateConduits_WhenIdIsNullOrEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitUpdateParameters nullIdParams = new() { Id = null, ShardCount = 5 };
        Func<Task> actionNull = async () => await Conduit.UpdateConduits(session, nullIdParams).ConfigureAwait(true);
        _ = (await actionNull.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");

        ConduitUpdateParameters emptyIdParams = new() { Id = Guid.Empty, ShardCount = 5 };
        Func<Task> actionEmpty = async () => await Conduit.UpdateConduits(session, emptyIdParams).ConfigureAwait(true);
        _ = (await actionEmpty.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> throws <see cref="ArgumentNullException"/> when ShardCount is null.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduits")]
    public async Task UpdateConduits_WhenShardCountIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitUpdateParameters parameters = new() { Id = Guid.NewGuid(), ShardCount = null };
        Func<Task> action = async () => await Conduit.UpdateConduits(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("ShardCount");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> throws <see cref="ArgumentOutOfRangeException"/> when ShardCount is less than 1.
    /// </summary>
    [Theory]
    [Trait("Member", "UpdateConduits")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateConduits_WhenShardCountIsLessThanOne_ThrowsArgumentOutOfRangeException(int invalidShardCount)
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitUpdateParameters parameters = new() { Id = Guid.NewGuid(), ShardCount = invalidShardCount };
        Func<Task> action = async () => await Conduit.UpdateConduits(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("ShardCount");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduits")]
    public async Task UpdateConduits_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        ConduitUpdateParameters parameters = new() { Id = Guid.NewGuid(), ShardCount = 5 };
        Func<Task> action = async () => await Conduit.UpdateConduits(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Conduit.UpdateConduits"/> updates a conduit when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "UpdateConduits")]
    public async Task UpdateConduits_WhenCalledWithValidParameters_ReturnsUpdatedConduit()
    {
        Guid conduitId = Guid.Parse("bf17725e-3991-4d1a-9694-845b412a8069");
        this._fixture.RegisterJsonResponse("PATCH", "/helix/eventsub/conduits", new
        {
            data = new[]
            {
                new
                {
                    id = conduitId.ToString(),
                    shard_count = 15
                }
            }
        });

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        ConduitUpdateParameters parameters = new() { Id = conduitId, ShardCount = 15 };
        ResponseData<Conduit>? response = await Conduit.UpdateConduits(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Count.Should().Be(1);

        Conduit conduit = response.Data[0];
        _ = conduit.Id.Should().Be(conduitId);
        _ = conduit.ShardCount.Should().Be(15);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/eventsub/conduits");
        _ = request.Body.Should().Contain(conduitId.ToString());
        _ = request.Body.Should().Contain("\"shard_count\":15");
    }

    #endregion UpdateConduits

    #region DeleteConduit

    /// <summary>
    /// Tests that <see cref="Conduit.DeleteConduit"/> throws <see cref="ArgumentNullException"/> when session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteConduit")]
    public async Task DeleteConduit_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Conduit.DeleteConduit(null!, Guid.NewGuid()).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.DeleteConduit"/> throws <see cref="ArgumentNullException"/> when id is Guid.Empty.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteConduit")]
    public async Task DeleteConduit_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Conduit.DeleteConduit(session, Guid.Empty).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    /// <summary>
    /// Tests that <see cref="Conduit.DeleteConduit"/> throws <see cref="TokenTypeException"/> when session is not an app token.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteConduit")]
    public async Task DeleteConduit_WhenSessionIsNotAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await Conduit.DeleteConduit(session, Guid.NewGuid()).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Conduit.DeleteConduit"/> deletes a conduit when called with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "DeleteConduit")]
    public async Task DeleteConduit_WhenCalledWithValidParameters_ReturnsSuccess()
    {
        Guid conduitId = Guid.Parse("bf17725e-3991-4d1a-9694-845b412a8069");
        this._fixture.RegisterResponse("DELETE", "/helix/eventsub/conduits", HttpStatusCode.NoContent);

        using TwitchSession session = TestTokenFactory.CreateAppSession();
        JsonApiResponse? response = await Conduit.DeleteConduit(session, conduitId).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.NoContent);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("DELETE");
        _ = request.Path.Should().Be("/helix/eventsub/conduits");
        _ = request.Query["id"].ToString().Should().Be(conduitId.ToString());
    }

    #endregion DeleteConduit
}

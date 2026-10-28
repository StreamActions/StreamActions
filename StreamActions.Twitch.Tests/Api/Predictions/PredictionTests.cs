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

using StreamActions.Twitch.Api.Predictions;
using StreamActions.Twitch.Api.Predictions.Objects;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Globalization;
using System.Net;

namespace StreamActions.Twitch.Tests.Api.Predictions;

/// <summary>
/// Performs unit tests on <see cref="Prediction"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class PredictionTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictionTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public PredictionTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region PredictionWindow

    [Fact]
    [Trait("Member", "PredictionWindow")]
    public void PredictionWindow_WhenPredictionWindowSecondsIsNull_ReturnsNull()
    {
        Prediction prediction = new() { PredictionWindowSeconds = null };
        _ = prediction.PredictionWindow.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "PredictionWindow")]
    public void PredictionWindow_WhenPredictionWindowSecondsHasValue_ReturnsTimeSpan()
    {
        Prediction prediction = new() { PredictionWindowSeconds = 120 };
        _ = prediction.PredictionWindow.Should().Be(TimeSpan.FromSeconds(120));
    }

    #endregion PredictionWindow

    #region GetPredictions

    [Fact]
    [Trait("Member", "GetPredictions")]
    public async Task GetPredictions_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Prediction.GetPredictions(null!, "1234").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Theory]
    [Trait("Member", "GetPredictions")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPredictions_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadPredictions);
        Func<Task> action = async () => await Prediction.GetPredictions(session, invalidId!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("broadcasterId");
    }

    [Fact]
    [Trait("Member", "GetPredictions")]
    public async Task GetPredictions_WhenIdCountExceeds25_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadPredictions);
        List<Guid> ids = [.. Enumerable.Range(1, 26).Select(static _ => Guid.NewGuid())];
        Func<Task> action = async () => await Prediction.GetPredictions(session, "1234", id: ids).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("id");
    }

    [Fact]
    [Trait("Member", "GetPredictions")]
    public async Task GetPredictions_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelReadPredictions);
        Func<Task> action = async () => await Prediction.GetPredictions(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetPredictions")]
    public async Task GetPredictions_WhenTokenHasEmptyOAuth_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                Scopes = [Scope.ChannelReadPredictions]
            }
        };

        Func<Task> action = async () => await Prediction.GetPredictions(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "GetPredictions")]
    public async Task GetPredictions_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        Func<Task> action = async () => await Prediction.GetPredictions(session, "1234").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "GetPredictions")]
    [InlineData("channel:read:predictions")]
    [InlineData("channel:manage:predictions")]
    public async Task GetPredictions_WhenValidRequest_ReturnsPredictions(string scopeName)
    {
        Guid predictionId = Guid.Parse("bc6376b9-423b-49f2-8922-ecab22637b1d");
        Guid outcome1Id = Guid.Parse("7308fd70-a6ea-4690-958d-7e1e69573630");
        Guid outcome2Id = Guid.Parse("906b70ba-1f12-47ea-9e95-e5f93d20e377");

        string json = $"{{\"data\":[{{\"id\":\"{predictionId}\",\"broadcaster_id\":\"1234\",\"broadcaster_name\":\"Streamer\",\"broadcaster_login\":\"streamer\",\"title\":\"Will they win?\",\"winning_outcome_id\":null,\"outcomes\":[{{\"id\":\"{outcome1Id}\",\"title\":\"Yes\",\"users\":10,\"channel_points\":100,\"color\":\"BLUE\",\"top_predictors\":[{{\"user_id\":\"u1\",\"user_name\":\"Predictor\",\"user_login\":\"predictor\",\"channel_points_used\":50,\"channel_points_won\":100}}]}},{{\"id\":\"{outcome2Id}\",\"title\":\"No\",\"users\":5,\"channel_points\":50,\"color\":\"PINK\",\"top_predictors\":null}}],\"prediction_window\":120,\"status\":\"ACTIVE\",\"created_at\":\"2023-01-01T00:00:00Z\",\"ended_at\":null,\"locked_at\":null}}],\"pagination\":{{\"cursor\":\"cursor123\"}}}}";
        this._fixture.RegisterResponse("GET", "/predictions", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.Scopes[scopeName]);
        ResponseData<Prediction>? response = await Prediction.GetPredictions(session, "1234", id: [predictionId], first: 20, after: "cursor123").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);
        _ = response.Pagination.Should().NotBeNull();
        _ = response.Pagination.Cursor.Should().Be("cursor123");

        Prediction item = response.Data[0];
        _ = item.Id.Should().Be(predictionId);
        _ = item.BroadcasterId.Should().Be("1234");
        _ = item.BroadcasterName.Should().Be("Streamer");
        _ = item.BroadcasterLogin.Should().Be("streamer");
        _ = item.Title.Should().Be("Will they win?");
        _ = item.WinningOutcomeId.Should().BeNull();
        _ = item.Status.Should().Be(Prediction.PredictionStatus.Active);
        _ = item.PredictionWindowSeconds.Should().Be(120);
        _ = item.PredictionWindow.Should().Be(TimeSpan.FromSeconds(120));
        _ = item.CreatedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.EndedAt.Should().BeNull();
        _ = item.LockedAt.Should().BeNull();
        _ = item.Outcomes.Should().NotBeNull();
        _ = item.Outcomes.Should().HaveCount(2);

        PredictionOutcome outcome1 = item.Outcomes[0];
        _ = outcome1.Id.Should().Be(outcome1Id);
        _ = outcome1.Title.Should().Be("Yes");
        _ = outcome1.Users.Should().Be(10);
        _ = outcome1.ChannelPoints.Should().Be(100);
        _ = outcome1.Color.Should().Be(PredictionOutcome.PredictionOutcomeColors.Blue);
        _ = outcome1.TopPredictors.Should().NotBeNull();
        _ = outcome1.TopPredictors.Should().HaveCount(1);

        PredictionPredictor predictor = outcome1.TopPredictors[0];
        _ = predictor.UserId.Should().Be("u1");
        _ = predictor.UserName.Should().Be("Predictor");
        _ = predictor.UserLogin.Should().Be("predictor");
        _ = predictor.ChannelPointsUsed.Should().Be(50);
        _ = predictor.ChannelPointsWon.Should().Be(100);

        PredictionOutcome outcome2 = item.Outcomes[1];
        _ = outcome2.Id.Should().Be(outcome2Id);
        _ = outcome2.Title.Should().Be("No");
        _ = outcome2.Users.Should().Be(5);
        _ = outcome2.ChannelPoints.Should().Be(50);
        _ = outcome2.Color.Should().Be(PredictionOutcome.PredictionOutcomeColors.Pink);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/predictions");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.QueryString.Should().Contain($"id={predictionId}");
        _ = request.QueryString.Should().Contain("after=cursor123");
    }

    /// <summary>
    /// Tests that <see cref="Prediction.GetPredictions"/> with default optional parameters omits id and after from query parameters.
    /// </summary>
    [Fact]
    [Trait("Member", "GetPredictions")]
    public async Task GetPredictions_WhenDefaultOptionalParameters_OmitsOptionalQueryParameters()
    {
        const string json = "{\"data\":[]}";
        this._fixture.RegisterResponse("GET", "/predictions", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadPredictions);
        ResponseData<Prediction>? response = await Prediction.GetPredictions(session, "1234").ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("GET");
        _ = request.Path.Should().Be("/helix/predictions");
        _ = request.QueryString.Should().Contain("broadcaster_id=1234");
        _ = request.QueryString.Should().Contain("first=20");
        _ = request.Query.ContainsKey("id").Should().BeFalse();
        _ = request.QueryString.Should().NotContain("after=");
    }

    /// <summary>
    /// Tests that <see cref="Prediction.GetPredictions"/> clamps first to [1, 25].
    /// </summary>
    /// <param name="first">The first parameter to test.</param>
    /// <param name="expectedQuery">The expected query string fragment.</param>
    [Theory]
    [Trait("Member", "GetPredictions")]
    [InlineData(0, "first=1")]
    [InlineData(30, "first=25")]
    public async Task GetPredictions_WhenFirstOutOfRange_ClampsFirst(int first, string expectedQuery)
    {
        const string json = "{\"data\":[]}";
        this._fixture.RegisterResponse("GET", "/predictions", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelReadPredictions);
        ResponseData<Prediction>? response = await Prediction.GetPredictions(session, "1234", first: first).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.QueryString.Should().Contain(expectedQuery);
    }

    #endregion GetPredictions

    #region CreatePrediction

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        Func<Task> action = async () => await Prediction.CreatePrediction(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "CreatePrediction")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePrediction_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = invalidId,
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("BroadcasterId");
    }

    [Theory]
    [Trait("Member", "CreatePrediction")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePrediction_WhenTitleIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidTitle)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = invalidTitle,
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenPredictionWindowSecondsIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = null,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("PredictionWindowSeconds");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenOutcomesIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = null
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Outcomes");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenTitleExceeds45Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = new string('a', 46),
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Title");
    }

    [Theory]
    [Trait("Member", "CreatePrediction")]
    [InlineData(1)]
    [InlineData(11)]
    public async Task CreatePrediction_WhenOutcomesCountOutOfRange_ThrowsArgumentOutOfRangeException(int count)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        List<PredictionCreationOutcome> outcomes = [.. Enumerable.Range(1, count).Select(static i => new PredictionCreationOutcome { Title = $"Outcome {i}" })];
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = outcomes
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Outcomes");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenOutcomeTitleIsNullOrWhitespace_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "  " }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Outcomes");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenOutcomeTitleExceeds25Chars_ThrowsArgumentOutOfRangeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = new string('x', 26) }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Outcomes");
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Prediction Title",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        Func<Task> action = async () => await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "CreatePrediction")]
    public async Task CreatePrediction_WhenValidRequest_PostsAndReturnsPrediction()
    {
        Guid predictionId = Guid.NewGuid();
        Guid outcome1Id = Guid.NewGuid();
        Guid outcome2Id = Guid.NewGuid();
        string json = $"{{\"data\":[{{\"id\":\"{predictionId}\",\"broadcaster_id\":\"1234\",\"broadcaster_name\":\"Streamer\",\"broadcaster_login\":\"streamer\",\"title\":\"Will we win?\",\"winning_outcome_id\":null,\"outcomes\":[{{\"id\":\"{outcome1Id}\",\"title\":\"Yes\",\"users\":0,\"channel_points\":0,\"color\":\"BLUE\"}},{{\"id\":\"{outcome2Id}\",\"title\":\"No\",\"users\":0,\"channel_points\":0,\"color\":\"PINK\"}}],\"prediction_window\":120,\"status\":\"ACTIVE\",\"created_at\":\"2023-01-01T00:00:00Z\",\"ended_at\":null,\"locked_at\":null}}]}}";
        this._fixture.RegisterResponse("POST", "/helix/predictions", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Will we win?",
            PredictionWindowSeconds = 120,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        ResponseData<Prediction>? response = await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Prediction item = response.Data[0];
        _ = item.Id.Should().Be(predictionId);
        _ = item.BroadcasterId.Should().Be("1234");
        _ = item.BroadcasterName.Should().Be("Streamer");
        _ = item.BroadcasterLogin.Should().Be("streamer");
        _ = item.Title.Should().Be("Will we win?");
        _ = item.WinningOutcomeId.Should().BeNull();
        _ = item.Status.Should().Be(Prediction.PredictionStatus.Active);
        _ = item.PredictionWindowSeconds.Should().Be(120);
        _ = item.PredictionWindow.Should().Be(TimeSpan.FromSeconds(120));
        _ = item.CreatedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.EndedAt.Should().BeNull();
        _ = item.LockedAt.Should().BeNull();
        _ = item.Outcomes.Should().NotBeNull();
        _ = item.Outcomes.Should().HaveCount(2);

        PredictionOutcome outcome1 = item.Outcomes[0];
        _ = outcome1.Id.Should().Be(outcome1Id);
        _ = outcome1.Title.Should().Be("Yes");
        _ = outcome1.Users.Should().Be(0);
        _ = outcome1.ChannelPoints.Should().Be(0);
        _ = outcome1.Color.Should().Be(PredictionOutcome.PredictionOutcomeColors.Blue);

        PredictionOutcome outcome2 = item.Outcomes[1];
        _ = outcome2.Id.Should().Be(outcome2Id);
        _ = outcome2.Title.Should().Be("No");
        _ = outcome2.Users.Should().Be(0);
        _ = outcome2.ChannelPoints.Should().Be(0);
        _ = outcome2.Color.Should().Be(PredictionOutcome.PredictionOutcomeColors.Pink);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Path.Should().Be("/helix/predictions");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"1234\"");
        _ = request.Body.Should().Contain("\"title\":\"Will we win?\"");
        _ = request.Body.Should().Contain("\"prediction_window\":120");
        _ = request.Body.Should().Contain("\"outcomes\"");
        _ = request.Body.Should().Contain("\"Yes\"");
        _ = request.Body.Should().Contain("\"No\"");
    }

    /// <summary>
    /// Tests that <see cref="Prediction.CreatePrediction"/> clamps prediction window seconds to [30, 1800].
    /// </summary>
    /// <param name="seconds">The seconds value to test.</param>
    /// <param name="expectedSeconds">The expected clamped seconds value.</param>
    [Theory]
    [Trait("Member", "CreatePrediction")]
    [InlineData(10, 30)]
    [InlineData(2000, 1800)]
    public async Task CreatePrediction_WhenPredictionWindowSecondsOutOfRange_ClampsValue(int seconds, int expectedSeconds)
    {
        Guid predictionId = Guid.NewGuid();
        string json = $"{{\"data\":[{{\"id\":\"{predictionId}\",\"broadcaster_id\":\"1234\",\"title\":\"Will we win?\"}}]}}";
        this._fixture.RegisterResponse("POST", "/helix/predictions", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionCreationParameters parameters = new()
        {
            BroadcasterId = "1234",
            Title = "Will we win?",
            PredictionWindowSeconds = seconds,
            Outcomes = [new() { Title = "Yes" }, new() { Title = "No" }]
        };

        ResponseData<Prediction>? response = await Prediction.CreatePrediction(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain($"\"prediction_window\":{expectedSeconds}");
    }

    #endregion CreatePrediction

    #region EndPrediction

    [Fact]
    [Trait("Member", "EndPrediction")]
    public async Task EndPrediction_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = Guid.NewGuid(),
            Status = Prediction.PredictionStatus.Canceled
        };

        Func<Task> action = async () => await Prediction.EndPrediction(null!, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    [Fact]
    [Trait("Member", "EndPrediction")]
    public async Task EndPrediction_WhenParametersIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        Func<Task> action = async () => await Prediction.EndPrediction(session, null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("parameters");
    }

    [Theory]
    [Trait("Member", "EndPrediction")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EndPrediction_WhenBroadcasterIdIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidId)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = invalidId,
            Id = Guid.NewGuid(),
            Status = Prediction.PredictionStatus.Canceled
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("BroadcasterId");
    }

    [Fact]
    [Trait("Member", "EndPrediction")]
    public async Task EndPrediction_WhenIdIsNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = null,
            Status = Prediction.PredictionStatus.Canceled
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");
    }

    [Fact]
    [Trait("Member", "EndPrediction")]
    public async Task EndPrediction_WhenIdIsEmptyGuid_ThrowsArgumentNullException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = Guid.Empty,
            Status = Prediction.PredictionStatus.Canceled
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("Id");
    }

    [Theory]
    [Trait("Member", "EndPrediction")]
    [InlineData(null)]
    [InlineData(Prediction.PredictionStatus.Invalid)]
    [InlineData(Prediction.PredictionStatus.Active)]
    public async Task EndPrediction_WhenStatusInvalid_ThrowsArgumentOutOfRangeException(Prediction.PredictionStatus? status)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = Guid.NewGuid(),
            Status = status
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(true))
            .WithParameterName("Status");
    }

    [Theory]
    [Trait("Member", "EndPrediction")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndPrediction_WhenStatusIsResolvedAndWinningOutcomeIdMissing_ThrowsArgumentNullException(bool isEmptyGuid)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = Guid.NewGuid(),
            Status = Prediction.PredictionStatus.Resolved,
            WinningOutcomeId = isEmptyGuid ? Guid.Empty : null
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("WinningOutcomeId");
    }

    [Fact]
    [Trait("Member", "EndPrediction")]
    public async Task EndPrediction_WhenAppToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = Guid.NewGuid(),
            Status = Prediction.PredictionStatus.Canceled
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "EndPrediction")]
    public async Task EndPrediction_WhenScopeMissing_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.BitsRead);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = Guid.NewGuid(),
            Status = Prediction.PredictionStatus.Canceled
        };

        Func<Task> action = async () => await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TwitchScopeMissingException>().ConfigureAwait(true);
    }

    [Theory]
    [Trait("Member", "EndPrediction")]
    [InlineData(Prediction.PredictionStatus.Canceled)]
    [InlineData(Prediction.PredictionStatus.Locked)]
    [InlineData(Prediction.PredictionStatus.Resolved)]
    public async Task EndPrediction_WhenValidRequest_PatchesAndReturnsPrediction(Prediction.PredictionStatus status)
    {
        Guid predictionId = Guid.NewGuid();
        Guid outcomeId = Guid.NewGuid();
        string statusStr = status.ToString().ToUpperInvariant();
        string winningOutcomeJson = status == Prediction.PredictionStatus.Resolved ? $"\"{outcomeId}\"" : "null";
        string json = $"{{\"data\":[{{\"id\":\"{predictionId}\",\"broadcaster_id\":\"1234\",\"broadcaster_name\":\"Streamer\",\"broadcaster_login\":\"streamer\",\"title\":\"Will we win?\",\"winning_outcome_id\":{winningOutcomeJson},\"outcomes\":[],\"status\":\"{statusStr}\",\"prediction_window\":120,\"created_at\":\"2023-01-01T00:00:00Z\",\"ended_at\":\"2023-01-01T00:05:00Z\",\"locked_at\":\"2023-01-01T00:02:00Z\"}}]}}";
        this._fixture.RegisterResponse("PATCH", "/helix/predictions", json);

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChannelManagePredictions);
        PredictionEndParameters parameters = new()
        {
            BroadcasterId = "1234",
            Id = predictionId,
            Status = status,
            WinningOutcomeId = status == Prediction.PredictionStatus.Resolved ? outcomeId : null
        };

        ResponseData<Prediction>? response = await Prediction.EndPrediction(session, parameters).ConfigureAwait(true);

        _ = response.Should().NotBeNull();
        _ = response.Status.Should().Be(HttpStatusCode.OK);
        _ = response.IsSuccessStatusCode.Should().BeTrue();
        _ = response.Data.Should().NotBeNull();
        _ = response.Data.Should().HaveCount(1);

        Prediction item = response.Data[0];
        _ = item.Id.Should().Be(predictionId);
        _ = item.BroadcasterId.Should().Be("1234");
        _ = item.BroadcasterName.Should().Be("Streamer");
        _ = item.BroadcasterLogin.Should().Be("streamer");
        _ = item.Title.Should().Be("Will we win?");
        if (status == Prediction.PredictionStatus.Resolved)
        {
            _ = item.WinningOutcomeId.Should().Be(outcomeId);
        }
        else
        {
            _ = item.WinningOutcomeId.Should().BeNull();
        }

        _ = item.Outcomes.Should().NotBeNull();
        _ = item.Outcomes.Should().BeEmpty();
        _ = item.PredictionWindowSeconds.Should().Be(120);
        _ = item.PredictionWindow.Should().Be(TimeSpan.FromSeconds(120));
        _ = item.Status.Should().Be(status);
        _ = item.CreatedAt.Should().Be(DateTime.Parse("2023-01-01T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.EndedAt.Should().Be(DateTime.Parse("2023-01-01T00:05:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
        _ = item.LockedAt.Should().Be(DateTime.Parse("2023-01-01T00:02:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("PATCH");
        _ = request.Path.Should().Be("/helix/predictions");
        _ = request.Body.Should().Contain("\"broadcaster_id\":\"1234\"");
        _ = request.Body.Should().Contain($"\"id\":\"{predictionId}\"");
        _ = request.Body.Should().Contain($"\"status\":\"{statusStr}\"");
        if (status == Prediction.PredictionStatus.Resolved)
        {
            _ = request.Body.Should().Contain($"\"winning_outcome_id\":\"{outcomeId}\"");
        }
        else
        {
            _ = request.Body.Should().NotContain($"\"winning_outcome_id\":\"{outcomeId}\"");
        }
    }

    #endregion EndPrediction
}

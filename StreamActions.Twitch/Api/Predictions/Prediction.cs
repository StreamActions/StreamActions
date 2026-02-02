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
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Linq;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Predictions;

/// <summary>
/// Represents a prediction that viewers can participate in.
/// </summary>
public sealed record Prediction
{
    /// <summary>
    /// An ID that identifies the prediction.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that created the prediction.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The question that the broadcaster is asking.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The ID of the winning outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is <see langword="null"/> until the prediction is <see cref="PredictionStatus.Resolved"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("winning_outcome_id")]
    public Guid? WinningOutcomeId { get; init; }

    /// <summary>
    /// A list of possible outcomes for the prediction.
    /// </summary>
    [JsonPropertyName("outcomes")]
    public IReadOnlyList<PredictionOutcome>? Outcomes { get; init; }

    /// <summary>
    /// The length of time (in seconds) that the prediction will run for.
    /// </summary>
    [JsonPropertyName("prediction_window")]
    public int? PredictionWindowSeconds { get; init; }

    /// <summary>
    /// The length of time that the prediction will run for.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? PredictionWindow => this.PredictionWindowSeconds.HasValue ? TimeSpan.FromSeconds(this.PredictionWindowSeconds.Value) : null;

    /// <summary>
    /// The prediction's status.
    /// </summary>
    [JsonPropertyName("status")]
    public PredictionStatus? Status { get; init; }

    /// <summary>
    /// The UTC date and time of when the prediction began.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The UTC date and time of when the prediction ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is <see langword="null"/> if the prediction is <see cref="PredictionStatus.Active"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; init; }

    /// <summary>
    /// The UTC date and time of when the prediction was locked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is <see langword="null"/> if the prediction is <see cref="PredictionStatus.Active"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("locked_at")]
    public DateTime? LockedAt { get; init; }

    /// <summary>
    /// The prediction's status.
    /// </summary>
    [JsonConverter(typeof(JsonUpperCaseEnumConverter<PredictionStatus>))]
    public enum PredictionStatus
    {
        /// <summary>
        /// Something went wrong while determining the state.
        /// </summary>
        Invalid,
        /// <summary>
        /// The prediction is running.
        /// </summary>
        Active,
        /// <summary>
        /// The prediction was canceled and Channel Points were refunded to predictors.
        /// </summary>
        Canceled,
        /// <summary>
        /// The prediction is locked and viewers can no longer participate.
        /// </summary>
        Locked,
        /// <summary>
        /// The prediction has been resolved and Channel Points have been awarded to the winners.
        /// </summary>
        Resolved
    }

    /// <summary>
    /// Gets a list of predictions that the broadcaster created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that created the predictions. This ID must match the user ID in the user access token.</param>
    /// <param name="id">A list of IDs that identify the predictions to return. Specify this parameter only if you want to filter the list that the request returns. The endpoint ignores duplicate IDs and those not owned by this broadcaster. Maximum: 25</param>
    /// <param name="first">The maximum number of items to return per page in the response. Maximum: 25. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Prediction"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not <see langword="null"/> and has more than 25 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadPredictions"/> or <see cref="Scope.ChannelManagePredictions"/>.</exception>
    /// <remarks>
    /// <para>
    /// Predictions are available for 90 days after they're created.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's predictions.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Prediction>?> GetPredictions(TwitchSession session, string broadcasterId, IEnumerable<Guid>? id = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 25").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelReadPredictions, Scope.ChannelManagePredictions);

        first = Math.Clamp(first, 1, 25);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (id is not null && id.Any())
        {
            List<string> ids = [];
            foreach (Guid predictionId in id)
            {
                ids.Add(predictionId.ToString());
            }

            if (ids.Count != 0)
            {
                queryParams.Add("id", ids);
            }
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/predictions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Prediction>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a prediction that viewers in the broadcaster's channel can participate in.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="PredictionCreationParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Prediction"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PredictionCreationParameters.BroadcasterId"/>, <see cref="PredictionCreationParameters.Title"/>, or a <see cref="PredictionCreationOutcome.Title"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PredictionCreationParameters.PredictionWindowSeconds"/> is <see langword="null"/>; <see cref="PredictionCreationParameters.Outcomes"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PredictionCreationParameters.Title"/> has more than 45 characters; <see cref="PredictionCreationParameters.Outcomes"/> has less than 2 or more than 10 elements; a <see cref="PredictionCreationOutcome.Title"/> has more than 25 characters.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManagePredictions"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully created the prediction.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The broadcaster is not allowed to create predictions.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<Prediction>?> CreatePrediction(TwitchSession session, PredictionCreationParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManagePredictions);

        if (string.IsNullOrWhiteSpace(parameters.BroadcasterId))
        {
            throw new ArgumentNullException(nameof(parameters.BroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Title))
        {
            throw new ArgumentNullException(nameof(parameters.Title)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.PredictionWindowSeconds.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.PredictionWindowSeconds)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Outcomes is null || !parameters.Outcomes.Any())
        {
            throw new ArgumentNullException(nameof(parameters.Outcomes)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Title.Length > 45)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Title), parameters.Title.Length, "must be 45 characters or less").Log(TwitchApi.GetLogger());
        }

        if (parameters.Outcomes.Count is < 2 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Outcomes), parameters.Outcomes.Count, "must provide 2-10 outcomes").Log(TwitchApi.GetLogger());
        }

        foreach (PredictionCreationOutcome outcome in parameters.Outcomes)
        {
            if (string.IsNullOrWhiteSpace(outcome.Title))
            {
                throw new ArgumentNullException(nameof(parameters.Outcomes), "an outcome may not be null, empty, or whitespace").Log(TwitchApi.GetLogger());
            }
            else if (outcome.Title.Length > 25)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters.Outcomes), outcome.Title, "an outcome length must be <= 25").Log(TwitchApi.GetLogger());
            }
        }

        if (Math.Clamp(parameters.PredictionWindowSeconds.Value, 30, 1800) != parameters.PredictionWindowSeconds.Value)
        {
            parameters = parameters with { PredictionWindowSeconds = Math.Clamp(parameters.PredictionWindowSeconds.Value, 30, 1800) };
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/predictions"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Prediction>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends an active prediction.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="PredictionEndParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Prediction"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PredictionEndParameters.BroadcasterId"/>, <see cref="PredictionEndParameters.Id"/>, or <see cref="PredictionEndParameters.Status"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PredictionEndParameters.Status"/> is not a valid value.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PredictionEndParameters.WinningOutcomeId"/> is <see langword="null"/> when <see cref="PredictionEndParameters.Status"/> is <see cref="PredictionStatus.Resolved"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManagePredictions"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the prediction.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The broadcaster is not allowed to end predictions.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<Prediction>?> EndPrediction(TwitchSession session, PredictionEndParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManagePredictions);

        if (string.IsNullOrWhiteSpace(parameters.BroadcasterId))
        {
            throw new ArgumentNullException(nameof(parameters.BroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Id == null || parameters.Id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(parameters.Id)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Status is null || parameters.Status == PredictionStatus.Invalid || parameters.Status == PredictionStatus.Active)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Status), parameters.Status, "must be one of Resolved, Canceled, or Locked").Log(TwitchApi.GetLogger());
        }

        if (parameters.Status == PredictionStatus.Resolved && (parameters.WinningOutcomeId == null || parameters.WinningOutcomeId == Guid.Empty))
        {
            throw new ArgumentNullException(nameof(parameters.WinningOutcomeId), "must be provided when resolving a prediction").Log(TwitchApi.GetLogger());
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, new("/predictions"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Prediction>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}

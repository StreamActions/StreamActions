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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Conduits;

/// <summary>
/// Represents a conduit used by EventSub to deliver notifications.
/// </summary>
public sealed record Conduit
{
    /// <summary>
    /// The ID that uniquely identifies the conduit.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// The number of shards associated with this conduit.
    /// </summary>
    [JsonPropertyName("shard_count")]
    public int? ShardCount { get; init; }

    /// <summary>
    /// Gets a list of conduits that the client in the access token created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Conduit"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved conduits.</description>
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
    public static async Task<ResponseData<Conduit>?> GetConduits(TwitchSession session)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        Uri uri = Util.BuildUri(new("/eventsub/conduits"));
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Conduit>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a conduit.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The parameters for the new conduit.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Conduit"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <see cref="ConduitCreationParameters.ShardCount"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ConduitCreationParameters.ShardCount"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully created the conduit.</description>
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
    public static async Task<ResponseData<Conduit>?> CreateConduits(TwitchSession session, ConduitCreationParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.ShardCount.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.ShardCount)).Log(TwitchApi.GetLogger());
        }

        if (parameters.ShardCount.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.ShardCount), parameters.ShardCount.Value, "must be >= 1").Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        Uri uri = Util.BuildUri(new("/eventsub/conduits"));
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Conduit>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a conduit's shard count.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The parameters for the update.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Conduit"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <see cref="ConduitUpdateParameters.Id"/> or <see cref="ConduitUpdateParameters.ShardCount"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ConduitUpdateParameters.ShardCount"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the conduit.</description>
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
    /// <term>404 Not Found</term>
    /// <description>The specified conduit was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Conduit>?> UpdateConduits(TwitchSession session, ConduitUpdateParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.Id.HasValue || parameters.Id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(parameters.Id)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.ShardCount.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.ShardCount)).Log(TwitchApi.GetLogger());
        }

        if (parameters.ShardCount.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.ShardCount), parameters.ShardCount.Value, "must be >= 1").Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        Uri uri = Util.BuildUri(new("/eventsub/conduits"));
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Conduit>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a conduit.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">The ID of the conduit to delete.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully deleted the conduit.</description>
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
    /// <term>404 Not Found</term>
    /// <description>The specified conduit was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> DeleteConduit(TwitchSession session, Guid id)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        NameValueCollection queryParams = new()
        {
            { "id", id.ToString("D", CultureInfo.InvariantCulture) }
        };

        Uri uri = Util.BuildUri(new("/eventsub/conduits"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}

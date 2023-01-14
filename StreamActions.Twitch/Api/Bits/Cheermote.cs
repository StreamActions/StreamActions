/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Extensions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Sends and represents a response for a request for a list of cheermotes.
/// </summary>
public sealed record Cheermote
{
    /// <summary>
    /// The string used to Cheer that precedes the Bits amount.
    /// </summary>
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    /// <summary>
    /// A list of <see cref="CheermoteTier"/> with their metadata.
    /// </summary>
    [JsonPropertyName("tiers")]
    public IReadOnlyList<CheermoteTier>? Tiers { get; init; }

    /// <summary>
    /// Shows the type of emote.
    /// </summary>
    [JsonPropertyName("type")]
    public CheermoteType? Type { get; init; }

    /// <summary>
    /// Order of the emotes as shown in the bits card, in ascending order.
    /// </summary>
    [JsonPropertyName("order")]
    public int? Order { get; init; }

    /// <summary>
    /// The date when this Cheermote was last updated.
    /// </summary>
    [JsonPropertyName("last_updated")]
    public DateTime? LastUpdated { get; init; }

    /// <summary>
    /// Indicates whether or not this emote provides a charity contribution match during charity campaigns.
    /// </summary>
    [JsonPropertyName("is_charitable")]
    public bool? IsCharitable { get; init; }

    /// <summary>
    /// The types of cheermotes.
    /// </summary>
    public enum CheermoteType
    {
        /// <summary>
        /// A global cheermote that belongs to Twitch.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Global_first_party,
        /// <summary>
        /// A global cheermote that belongs to a third-party company.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Global_third_party,
        /// <summary>
        /// A custom cheermote uploaded by the broadcaster.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Channel_custom,
        /// <summary>
        /// A display-only cheermote.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Display_only,
        /// <summary>
        /// A sponsored cheermote.
        /// </summary>
        Sponsored
    }

    /// <summary>
    /// Retrieves the list of available Cheermotes, animated emotes to which viewers can assign Bits, to cheer in chat. Cheermotes returned are available throughout Twitch, in all Bits-enabled channels.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">ID for the broadcaster who might own specialized Cheermotes.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Cheermote"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<ResponseData<Cheermote>?> GetCheermotes(TwitchSession session, string? broadcasterId = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        Dictionary<string, IEnumerable<string>> queryParams = new();

        if (!string.IsNullOrWhiteSpace(broadcasterId))
        {
            queryParams.Add("broadcaster_id", new List<string> { broadcasterId });
        }

        Uri uri = Util.BuildUri(new("/bits/cheermotes"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Cheermote>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}

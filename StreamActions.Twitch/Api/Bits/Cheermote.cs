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
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Sends and represents a response for a request for a list of cheermotes.
/// </summary>
public sealed record Cheermote
{
    /// <summary>
    /// The name portion of the Cheermote string that you use in chat to cheer Bits. The full Cheermote string is the concatenation of <c>{prefix} + {number of Bits}</c>.
    /// </summary>
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    /// <summary>
    /// A list of <see cref="CheermoteTier"/> with their metadata.
    /// </summary>
    [JsonPropertyName("tiers")]
    public IReadOnlyList<CheermoteTier>? Tiers { get; init; }

    /// <summary>
    /// The type of Cheermote.
    /// </summary>
    [JsonPropertyName("type")]
    public CheermoteType? Type { get; init; }

    /// <summary>
    /// The order that the Cheermotes are shown in the Bits card. The numbers may not be consecutive. The order numbers are unique within a Cheermote type (for example, <see cref="CheermoteType.Global_first_party"/>) but may not be unique amongst all Cheermotes in the response.
    /// </summary>
    [JsonPropertyName("order")]
    public int? Order { get; init; }

    /// <summary>
    /// The date and time when this Cheermote was last updated.
    /// </summary>
    [JsonPropertyName("last_updated")]
    public DateTime? LastUpdated { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether this Cheermote provides a charitable contribution match during charity campaigns.
    /// </summary>
    [JsonPropertyName("is_charitable")]
    public bool? IsCharitable { get; init; }

    /// <summary>
    /// The type of Cheermote.
    /// </summary>
    public enum CheermoteType
    {
        /// <summary>
        /// A Twitch-defined Cheermote that is shown in the Bits card.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Global_first_party,
        /// <summary>
        /// A Twitch-defined Cheermote that is not shown in the Bits card.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Global_third_party,
        /// <summary>
        /// A broadcaster-defined Cheermote.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Channel_custom,
        /// <summary>
        /// Do not use; for internal use only.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Display_only,
        /// <summary>
        /// A sponsor-defined Cheermote. When used, the sponsor adds additional Bits to the amount that the user cheered. For example, if the user cheered Terminator100, the broadcaster might receive 110 Bits, which includes the sponsor's 10 Bits contribution.
        /// </summary>
        Sponsored
    }

    /// <summary>
    /// Gets a list of Cheermotes that users can use to cheer Bits in any Bits-enabled channel's chat room. Cheermotes are animated emotes that viewers can assign Bits to.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose custom Cheermotes you want to get. If not specified, the response contains only global Cheermotes.
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Cheermote"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the Cheermotes.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
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

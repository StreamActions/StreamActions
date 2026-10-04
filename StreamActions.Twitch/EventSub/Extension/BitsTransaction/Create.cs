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

using System.Text.Json.Serialization;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using StreamActions.Twitch.EventSub.Extension.BitsTransaction.Objects;

namespace StreamActions.Twitch.EventSub.Extension.BitsTransaction;

/// <summary>
/// An event that is sent when a Twitch Extension Bits transaction occurs.
/// </summary>
public sealed record Create : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(ExtensionClientIdCondition);

    /// <inheritdoc/>
    public static string Type => "extension.bits_transaction.create";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// Client ID of the extension.
    /// </summary>
    [JsonPropertyName("extension_client_id")]
    public string? ExtensionClientId { get; init; }

    /// <summary>
    /// Transaction ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The transactions broadcaster ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The transactions broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The transactions broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The transactions user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The transactions user login.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The transactions user display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// Additional extension product information.
    /// </summary>
    [JsonPropertyName("product")]
    public Product? Product { get; init; }
}

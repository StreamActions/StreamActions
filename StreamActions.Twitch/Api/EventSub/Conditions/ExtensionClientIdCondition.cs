﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.EventSub.Conditions;

/// <summary>
/// An <see cref="EventSubCondition{T}"/> containing the <c>extension_client_id</c> field.
/// </summary>
public sealed record ExtensionClientIdCondition : EventSubCondition<ExtensionClientIdCondition>
{
    /// <summary>
    /// The client ID of the extension to receive notifications for.
    /// </summary>
    [JsonPropertyName("extension_client_id")]
    public string? ExtensionClientId { get; init; }
}

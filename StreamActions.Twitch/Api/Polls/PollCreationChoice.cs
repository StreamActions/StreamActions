/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.Polls;

/// <summary>
/// Represents a choice in <see cref="PollCreationParameters.Choices"/>.
/// </summary>
public record PollCreationChoice
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="title">Text displayed for the choice. Maximum: 25 characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="title"/> is longer than 25 characters.</exception>
    public PollCreationChoice(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentNullException(nameof(title));
        }

        if (title.Length > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(title), title.Length, "Must be 25 characters or less");
        }

        this.Title = title;
    }

    /// <summary>
    /// Text displayed for the choice. Maximum: 25 characters.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; private init; }
}

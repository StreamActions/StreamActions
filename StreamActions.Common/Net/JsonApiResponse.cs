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

using StreamActions.Common.Extensions;
using System.Net;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Net;

/// <summary>
/// Base record for JSON API responses. Indicates request status code and error message if not success.
/// </summary>
public record JsonApiResponse
{
    /// <summary>
    /// The HTTP status code as indicated by the response JSON.
    /// </summary>
    [JsonPropertyName("status")]
    public HttpStatusCode Status
    {
        get => this._status;
        init => this._status = value;
    }

    /// <summary>
    /// The string name of <see cref="Status"/>.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error
    {
        get => this._error == null || this._error.Length == 0 ? Enum.GetName(this.Status) ?? "Unknown" : this._error;
        init => this._error = value;
    }

    /// <summary>
    /// The message returned with the error, if any.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    /// <summary>
    /// Indicates if <see cref="Status"/> is in the 200-299 range.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessStatusCode => (int)this.Status is >= 200 and <= 299;

    /// <summary>
    /// Indicates if <see cref="Status"/> is in the 400-499 range.
    /// </summary>
    [JsonIgnore]
    public bool IsClientErrorStatusCode => (int)this.Status is >= 400 and <= 499;

    /// <summary>
    /// Indicates if <see cref="Status"/> is in the 500-599 range.
    /// </summary>
    [JsonIgnore]
    public bool IsServerErrorStatusCode => (int)this.Status is >= 500 and <= 599;

    /// <summary>
    /// Backing field for <see cref="Error"/>.
    /// </summary>
    [JsonIgnore]
    private string? _error;

    /// <summary>
    /// Backing field for <see cref="Status"/>.
    /// </summary>
    [JsonIgnore]
    private HttpStatusCode _status;

    /// <summary>
    /// Internal method allowing <see cref="HttpResponseMessageExtensions.ReadFromJsonAsync{T}(HttpResponseMessage, System.Text.Json.JsonSerializerOptions?, CancellationToken)"/> to update the status.
    /// </summary>
    /// <param name="statusCode">The new status code.</param>
    internal void SetStatus(HttpStatusCode statusCode) => this._status = statusCode;
}

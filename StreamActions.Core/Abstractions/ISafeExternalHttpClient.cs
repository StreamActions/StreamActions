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

using StreamActions.Core.Models;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Executes SSRF-hardened outbound HTTP requests with workspace secret injection, bounded response reads, and secret redaction.
/// </summary>
public interface ISafeExternalHttpClient
{
    /// <summary>
    /// Sends an outbound HTTP request after validating scheme, DNS/IP targets, secret host allowlists, redirects, size limits, and timeouts.
    /// </summary>
    /// <param name="request">The outbound HTTP request parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The scrubbed <see cref="SafeExternalHttpResponse"/>.</returns>
    ValueTask<SafeExternalHttpResponse> SendAsync(
        SafeExternalHttpRequest request,
        CancellationToken cancellationToken = default);
}


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

namespace StreamActions.Common.Extensions;

/// <summary>
/// Contains extension members to <see cref="CancellationToken"/>.
/// </summary>
public static class CancellationTokenExtensions
{
    #region Public Methods

    /// <summary>
    /// Waits for a <see cref="CancellationToken"/> in a background Task thread.
    /// </summary>
    /// <param name="token">The <see cref="CancellationToken"/> to wait for.</param>
    /// <returns>A Task that can be awaited.</returns>
    public static Task WaitAsync(this CancellationToken token) => Task.Run(() => _ = token.WaitHandle.WaitOne());

    #endregion Public Methods
}

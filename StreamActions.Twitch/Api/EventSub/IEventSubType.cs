﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using StreamActions.Twitch.Api.EventSub.Conditions;

namespace StreamActions.Twitch.Api.EventSub;

/// <summary>
/// An EventSub subscription type.
/// </summary>
public interface IEventSubType
{
    #region Public Properties

    /// <summary>
    /// The <see cref="EventSubCondition"/> that this type uses.
    /// </summary>
    static abstract Type EventSubConditionType { get; }

    /// <summary>
    /// The <see cref="EventSubSubscription.Type"/>.
    /// </summary>
    static abstract string Type { get; }

    #endregion Public Properties
}

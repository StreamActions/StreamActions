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

using StreamActions.Core.Templates;
using StreamActions.Twitch.UI.Templates.Handlers;

namespace StreamActions.Twitch.UI.Templates;

/// <summary>
/// Creates the default set of Twitch-scoped <see cref="ICommandTagHandler"/> instances and layered <see cref="TagRegistry"/>.
/// </summary>
public static class TwitchTagRegistryFactory
{
    #region Public Methods

    /// <summary>
    /// Creates all Twitch platform <see cref="ICommandTagHandler"/> instances (<c>PlatformId = "twitch"</c>).
    /// </summary>
    /// <returns>An immutable read-only list of Twitch tag handlers.</returns>
    public static IReadOnlyList<ICommandTagHandler> CreateDefaultHandlers()
    {
        List<ICommandTagHandler> handlers =
        [
            new TwitchGameTagHandler(),
            new TwitchTitleTagHandler(),
            new TwitchUptimeTagHandler(),
            new TwitchDowntimeTagHandler(),
            new TwitchPlaytimeTagHandler(),
            new TwitchGamesPlayedTagHandler(),
            new TwitchViewersTagHandler(),
            new TwitchFollowersTagHandler(),
            new TwitchSubscribersTagHandler(),
            new TwitchFollowageTagHandler(),
            new TwitchFollowdateTagHandler(),
            new TwitchHoursTagHandler(),
            new TwitchAccountAgeTagHandler(),

            new TwitchTeamNameTagHandler(),
            new TwitchTeamUrlTagHandler(),
            new TwitchTeamMembersTagHandler(),
            new TwitchTeamRandomMemberTagHandler(),
            new TwitchTeamMemberGameTagHandler(),
            new TwitchTeamMemberFollowersTagHandler(),
            new TwitchTeamMemberUrlTagHandler(),

            new CpTitleTagHandler(),
            new CpPromptTagHandler(),
            new CpCostTagHandler(),
            new CpInputTagHandler(),
            new CpUserTagHandler(),
            new CpUsernameTagHandler(),
            new CpUserIdTagHandler(),
            new CpRedemptionIdTagHandler(),
            new CpRewardIdTagHandler(),
            new CpStatusTagHandler(),
            new CpFulfillTagHandler(),
            new CpCancelTagHandler(),
        ];

        return handlers.AsReadOnly();
    }

    /// <summary>
    /// Creates a new <see cref="TagRegistry"/> populated with all universal Core handlers and Twitch platform handlers.
    /// </summary>
    /// <returns>A configured <see cref="TagRegistry"/>.</returns>
    public static TagRegistry CreateDefaultRegistry()
    {
        TagRegistry registry = CoreTagRegistryFactory.CreateDefaultRegistry();
        foreach (ICommandTagHandler handler in CreateDefaultHandlers())
        {
            registry.Register(handler);
        }

        return registry;
    }

    #endregion Public Methods
}

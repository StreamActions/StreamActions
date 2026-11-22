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
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.Api.Teams;
using StreamActions.Twitch.Api.Teams.Objects;
using StreamActions.Twitch.UI.Abstractions;
using System.Globalization;
using System.Security.Cryptography;

namespace StreamActions.Twitch.UI.Templates.Handlers;

/// <summary>
/// Shared helper methods for Twitch Helix team tag handlers.
/// </summary>
internal static class TwitchTeamTagSupport
{
    #region Internal Methods

    /// <summary>
    /// Evaluates the required <c>&lt;team&gt;</c> argument and resolves the memoized <see cref="Team"/> along with the workspace session lease.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated AST arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved session lease, normalized team query, and <see cref="Team"/>, or <see langword="null"/> values when incomplete.</returns>
    internal static async ValueTask<(TwitchWorkspaceSessionLease? Lease, string? NormalizedTeamName, Team? Team)> ResolveTeamAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        string? rawTeam = await TwitchHelixTagHelper
            .EvaluateRequiredStringAsync(context, unevaluatedArgs, 0, cancellationToken)
            .ConfigureAwait(false);
        string normalizedTeam = TwitchHelixTagHelper.CleanHandle(rawTeam);
        if (string.IsNullOrEmpty(normalizedTeam))
        {
            return (null, null, null);
        }

        TwitchWorkspaceSessionLease? lease = await TwitchHelixTagHelper
            .GetSessionLeaseAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return (null, normalizedTeam, null);
        }

        MemoizedLookup<Team> teamLookup = await TwitchHelixTagHelper
            .GetTeamByNameAsync(context, lease.TwitchSession, normalizedTeam, cancellationToken)
            .ConfigureAwait(false);

        return (lease, normalizedTeam, teamLookup.IsSuccess ? teamLookup.Value : null);
    }

    /// <summary>
    /// Evaluates the <c>&lt;team&gt;</c> and <c>&lt;user&gt;</c> arguments and finds the matching <see cref="TeamMember"/>.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated AST arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved session lease and matching <see cref="TeamMember"/>, or <see langword="null"/> values when missing.</returns>
    internal static async ValueTask<(TwitchWorkspaceSessionLease? Lease, TeamMember? Member)> ResolveTeamMemberAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        if (unevaluatedArgs.Count < 2)
        {
            return (null, null);
        }

        (TwitchWorkspaceSessionLease? lease, _, Team? team) = await ResolveTeamAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null || team?.Users is null || team.Users.Count == 0)
        {
            return (null, null);
        }

        string? rawUser = await TwitchHelixTagHelper
            .EvaluateRequiredStringAsync(context, unevaluatedArgs, 1, cancellationToken)
            .ConfigureAwait(false);
        string cleanedUser = TwitchHelixTagHelper.CleanHandle(rawUser);
        if (string.IsNullOrEmpty(cleanedUser))
        {
            return (null, null);
        }

        TeamMember? match = team.Users.FirstOrDefault(member =>
            string.Equals(member.UserLogin?.Trim(), cleanedUser, StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.UserName?.Trim(), cleanedUser, StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.UserId?.Trim(), cleanedUser, StringComparison.OrdinalIgnoreCase));

        return (lease, match);
    }

    /// <summary>
    /// Formats a <see cref="TeamMember"/> display name with fallback to login name.
    /// </summary>
    /// <param name="member">The team member.</param>
    /// <returns>The formatted member name, or <see langword="null"/> if both fields are blank.</returns>
    internal static string? GetMemberDisplayName(TeamMember member) =>
        !string.IsNullOrWhiteSpace(member.UserName)
            ? member.UserName.Trim()
            : !string.IsNullOrWhiteSpace(member.UserLogin)
                ? member.UserLogin.Trim()
                : null;

    #endregion Internal Methods
}

/// <summary>
/// Handles the Twitch <c>{team_name &lt;team&gt;}</c> tag using memoized <see cref="Team.GetTeams"/>.
/// </summary>
public sealed class TwitchTeamNameTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_name";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (_, _, Team? team) = await TwitchTeamTagSupport
            .ResolveTeamAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (team is null)
        {
            return TagResult.Incomplete;
        }

        string? displayName = !string.IsNullOrWhiteSpace(team.TeamDisplayName)
            ? team.TeamDisplayName.Trim()
            : team.TeamName?.Trim();

        return TagResult.FromNonEmpty(displayName);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{team_url &lt;team&gt;}</c> tag using memoized <see cref="Team.GetTeams"/>.
/// </summary>
public sealed class TwitchTeamUrlTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_url";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (_, string? normalizedTeamName, Team? team) = await TwitchTeamTagSupport
            .ResolveTeamAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (team is null)
        {
            return TagResult.Incomplete;
        }

        string? slug = !string.IsNullOrWhiteSpace(team.TeamName)
            ? team.TeamName.Trim()
            : normalizedTeamName;

        return string.IsNullOrWhiteSpace(slug)
            ? TagResult.Incomplete
            : TagResult.Complete($"https://www.twitch.tv/team/{Uri.EscapeDataString(slug)}");
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{team_members &lt;team&gt;}</c> tag using memoized <see cref="Team.GetTeams"/>.
/// </summary>
public sealed class TwitchTeamMembersTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_members";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (_, _, Team? team) = await TwitchTeamTagSupport
            .ResolveTeamAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (team?.Users is null || team.Users.Count == 0)
        {
            return TagResult.Incomplete;
        }

        string[] members =
        [
            .. team.Users
                .Select(TwitchTeamTagSupport.GetMemberDisplayName)
                .Where(static name => !string.IsNullOrWhiteSpace(name))!,
        ];

        return members.Length == 0
            ? TagResult.Incomplete
            : TagResult.Complete(string.Join(", ", members));
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{team_random_member &lt;team&gt;}</c> tag using memoized <see cref="Team.GetTeams"/>.
/// </summary>
public sealed class TwitchTeamRandomMemberTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_random_member";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (_, _, Team? team) = await TwitchTeamTagSupport
            .ResolveTeamAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (team?.Users is null || team.Users.Count == 0)
        {
            return TagResult.Incomplete;
        }

        string[] members =
        [
            .. team.Users
                .Select(TwitchTeamTagSupport.GetMemberDisplayName)
                .Where(static name => !string.IsNullOrWhiteSpace(name))!,
        ];

        if (members.Length == 0)
        {
            return TagResult.Incomplete;
        }

        string chosen = members[RandomNumberGenerator.GetInt32(members.Length)];
        return TagResult.Complete(chosen);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{team_member_game &lt;team&gt; &lt;user&gt;}</c> tag using memoized team, stream, and channel lookups.
/// </summary>
public sealed class TwitchTeamMemberGameTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_member_game";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (TwitchWorkspaceSessionLease? lease, TeamMember? member) = await TwitchTeamTagSupport
            .ResolveTeamMemberAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (lease is null || member is null)
        {
            return TagResult.Incomplete;
        }

        string memberLogin = TwitchHelixTagHelper.CleanHandle(member.UserLogin);
        if (!string.IsNullOrEmpty(memberLogin))
        {
            MemoizedLookup<TwitchStream> streamLookup = await TwitchHelixTagHelper
                .GetStreamAsync(context, lease, memberLogin, cancellationToken)
                .ConfigureAwait(false);
            if (streamLookup.Value is not null && !string.IsNullOrWhiteSpace(streamLookup.Value.GameName))
            {
                return TagResult.Complete(streamLookup.Value.GameName);
            }
        }

        if (string.IsNullOrWhiteSpace(member.UserId))
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<Channel> channelLookup = await TwitchHelixTagHelper
            .GetChannelInformationAsync(context, lease.TwitchSession, member.UserId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return TagResult.FromNonEmpty(channelLookup.Value?.GameName);
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{team_member_followers &lt;team&gt; &lt;user&gt;}</c> tag using memoized team and follower lookups.
/// </summary>
public sealed class TwitchTeamMemberFollowersTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_member_followers";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (TwitchWorkspaceSessionLease? lease, TeamMember? member) = await TwitchTeamTagSupport
            .ResolveTeamMemberAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (lease is null || member is null || string.IsNullOrWhiteSpace(member.UserId))
        {
            return TagResult.Incomplete;
        }

        MemoizedLookup<ResponseData<ChannelFollower>> followersLookup = await TwitchHelixTagHelper
            .GetChannelFollowersTotalAsync(context, lease.TwitchSession, member.UserId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return followersLookup.IsSuccess && followersLookup.Value?.Total is int total
            ? TagResult.Complete(total.ToString(CultureInfo.InvariantCulture))
            : TagResult.Incomplete;
    }

    #endregion Public Methods
}

/// <summary>
/// Handles the Twitch <c>{team_member_url &lt;team&gt; &lt;user&gt;}</c> tag using memoized <see cref="Team.GetTeams"/>.
/// </summary>
public sealed class TwitchTeamMemberUrlTagHandler : ICommandTagHandler
{
    #region Public Properties

    /// <inheritdoc />
    public string TagName => "team_member_url";

    /// <inheritdoc />
    public string? PlatformId => TwitchHelixTagHelper._twitchPlatformId;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        (_, TeamMember? member) = await TwitchTeamTagSupport
            .ResolveTeamMemberAsync(context, unevaluatedArgs, cancellationToken)
            .ConfigureAwait(false);

        if (member is null)
        {
            return TagResult.Incomplete;
        }

        string login = TwitchHelixTagHelper.CleanHandle(!string.IsNullOrWhiteSpace(member.UserLogin) ? member.UserLogin : member.UserName);
        return string.IsNullOrEmpty(login)
            ? TagResult.Incomplete
            : TagResult.Complete($"https://www.twitch.tv/{Uri.EscapeDataString(login)}");
    }

    #endregion Public Methods
}

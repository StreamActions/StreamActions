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

using StreamActions.Core.Actions;
using StreamActions.Core.Permissions;
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Chat;
using StreamActions.Twitch.Api.Clips;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.UI.Abstractions;
using System.Globalization;

namespace StreamActions.Twitch.UI.Actions;

/// <summary>
/// Factory producing Twitch-specific <see cref="IPlatformActionHandler"/> definitions for the Action Block catalog.
/// </summary>
public static class TwitchActionBlockCatalogFactory
{
    /// <summary>
    /// Creates all built-in Twitch <see cref="IPlatformActionHandler"/> instances.
    /// </summary>
    /// <returns>Twitch platform action handlers.</returns>
    public static IReadOnlyList<IPlatformActionHandler> CreateDefaultActions() =>
    [
        new CreateClipActionHandler(),
        new SendChatAnnouncementActionHandler(),
        new CreateStreamMarkerActionHandler(),
        new ModifyChannelInfoActionHandler(),
    ];

    /// <summary>
    /// Acquires the workspace's <see cref="TwitchWorkspaceSessionLease"/> from the scoped service provider.
    /// </summary>
    private static async ValueTask<TwitchWorkspaceSessionLease?> AcquireLeaseAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken) => context.ServiceProvider?.GetService(typeof(ITwitchWorkspaceSessionProvider)) is not ITwitchWorkspaceSessionProvider provider ? null : await provider.GetSessionAsync(context.WorkspaceId, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Twitch action creating a clip on the broadcaster's live stream (<c>Twitch &gt; Api &gt; Clips &gt; Create Clip</c>).
    /// </summary>
    private sealed class CreateClipActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "twitch.api.clips.create";

        /// <inheritdoc/>
        public string PlatformId => "twitch";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Twitch", "Api", "Clips"];

        /// <inheritdoc/>
        public string DisplayName => "Create Clip";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public string DefaultOutputVariableName => "clipUrl";

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("title", "Clip Title (optional)", ActionInputKind.TemplateString, IsRequired: false),
            new("duration", "Duration Seconds (5-60)", ActionInputKind.Decimal, IsRequired: false),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            TwitchWorkspaceSessionLease? lease = await AcquireLeaseAsync(context, cancellationToken).ConfigureAwait(false);
            if (lease is null || string.IsNullOrWhiteSpace(lease.BroadcasterId))
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }

                return null;
            }

            await using (lease.ConfigureAwait(false))
            {
                string? title = resolvedParameters.TryGetValue("title", out string? rawTitle) && !string.IsNullOrWhiteSpace(rawTitle)
                    ? rawTitle.Trim()
                    : null;
                float? duration = resolvedParameters.TryGetValue("duration", out string? rawDur)
                    && float.TryParse(rawDur, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedDuration)
                    ? parsedDuration
                    : null;

                ResponseData<CreatedClip>? response = await CreatedClip
                    .CreateClip(lease.TwitchSession, lease.BroadcasterId, title, duration)
                    .ConfigureAwait(false);

                CreatedClip? created = response?.Data is { Count: > 0 } data ? data[0] : null;
                return created?.EditUrl?.ToString() ?? created?.Id;
            }
        }
    }

    /// <summary>
    /// Twitch action sending a highlighted chat announcement (<c>Twitch &gt; Api &gt; Chat &gt; Send Announcement</c>).
    /// </summary>
    private sealed class SendChatAnnouncementActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "twitch.api.chat.announce";

        /// <inheritdoc/>
        public string PlatformId => "twitch";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Twitch", "Api", "Chat"];

        /// <inheritdoc/>
        public string DisplayName => "Send Chat Announcement";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("message", "Announcement Message", ActionInputKind.TemplateString, IsRequired: true),
            new("color", "Highlight Color", ActionInputKind.SelectList, IsRequired: false, DefaultValue: "Primary", AllowedValues: ["Primary", "Blue", "Green", "Orange", "Purple"]),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            TwitchWorkspaceSessionLease? lease = await AcquireLeaseAsync(context, cancellationToken).ConfigureAwait(false);
            if (lease is null || string.IsNullOrWhiteSpace(lease.BroadcasterId))
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }

                return null;
            }

            await using (lease.ConfigureAwait(false))
            {
                string message = resolvedParameters.GetValueOrDefault("message") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(message))
                {
                    return null;
                }

                Announcement.AnnouncementColor color = resolvedParameters.TryGetValue("color", out string? rawColor)
                    && Enum.TryParse<Announcement.AnnouncementColor>(rawColor, ignoreCase: true, out Announcement.AnnouncementColor parsedColor)
                    ? parsedColor
                    : Announcement.AnnouncementColor.Primary;

                _ = await Announcement.SendChatAnnouncement(
                    lease.TwitchSession,
                    lease.BroadcasterId,
                    lease.BroadcasterId,
                    new Announcement
                    {
                        Message = message,
                        Color = color,
                    }).ConfigureAwait(false);

                return message;
            }
        }
    }

    /// <summary>
    /// Twitch action creating a stream marker (<c>Twitch &gt; Api &gt; Streams &gt; Create Stream Marker</c>).
    /// </summary>
    private sealed class CreateStreamMarkerActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "twitch.api.streams.create_marker";

        /// <inheritdoc/>
        public string PlatformId => "twitch";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Twitch", "Api", "Streams"];

        /// <inheritdoc/>
        public string DisplayName => "Create Stream Marker";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public string DefaultOutputVariableName => "markerId";

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("description", "Marker Description (max 140 chars)", ActionInputKind.TemplateString, IsRequired: false),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            TwitchWorkspaceSessionLease? lease = await AcquireLeaseAsync(context, cancellationToken).ConfigureAwait(false);
            if (lease is null || string.IsNullOrWhiteSpace(lease.BroadcasterId))
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }

                return null;
            }

            await using (lease.ConfigureAwait(false))
            {
                string? description = resolvedParameters.TryGetValue("description", out string? rawDescription) && !string.IsNullOrWhiteSpace(rawDescription)
                    ? rawDescription.Trim()
                    : null;

                ResponseData<StreamMarker>? response = await StreamMarker.CreateStreamMarker(
                    lease.TwitchSession,
                    new CreateStreamMarkerParameters
                    {
                        UserId = lease.BroadcasterId,
                        Description = description,
                    }).ConfigureAwait(false);

                return response?.Data is { Count: > 0 } data ? data[0].Id : null;
            }
        }
    }

    /// <summary>
    /// Twitch action updating stream title or game category (<c>Twitch &gt; Api &gt; Channels &gt; Modify Channel Information</c>).
    /// </summary>
    private sealed class ModifyChannelInfoActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "twitch.api.channels.modify";

        /// <inheritdoc/>
        public string PlatformId => "twitch";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Twitch", "Api", "Channels"];

        /// <inheritdoc/>
        public string DisplayName => "Modify Channel Information";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("title", "Stream Title (optional)", ActionInputKind.TemplateString, IsRequired: false),
            new("gameId", "Game ID (optional)", ActionInputKind.TemplateString, IsRequired: false),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            TwitchWorkspaceSessionLease? lease = await AcquireLeaseAsync(context, cancellationToken).ConfigureAwait(false);
            if (lease is null || string.IsNullOrWhiteSpace(lease.BroadcasterId))
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }

                return null;
            }

            await using (lease.ConfigureAwait(false))
            {
                string? title = resolvedParameters.TryGetValue("title", out string? rawTitle) && !string.IsNullOrWhiteSpace(rawTitle)
                    ? rawTitle.Trim()
                    : null;
                string? gameId = resolvedParameters.TryGetValue("gameId", out string? rawGameId) && !string.IsNullOrWhiteSpace(rawGameId)
                    ? rawGameId.Trim()
                    : null;

                if (title is null && gameId is null)
                {
                    return null;
                }

                _ = await Channel.ModifyChannelInformation(
                    lease.TwitchSession,
                    lease.BroadcasterId,
                    new ModifyChannelParameters
                    {
                        Title = title,
                        GameId = gameId,
                    }).ConfigureAwait(false);

                return title ?? gameId;
            }
        }
    }
}

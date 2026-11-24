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

using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Permissions;
using System.Collections.Concurrent;
using System.Globalization;

namespace StreamActions.Core.Actions;

/// <summary>
/// Built-in Action Block trigger identifier constants.
/// </summary>
public static class BuiltInTriggerIds
{
    /// <summary>Fires on any chat message.</summary>
    public const string ChatMessage = "core.chat.message";

    /// <summary>Fires when a specific chat command trigger is invoked.</summary>
    public const string ChatCommand = "core.chat.command";

    /// <summary>Fires when a chat message matches a keyword or pattern.</summary>
    public const string ChatKeyword = "core.chat.keyword";

    /// <summary>Fires on a viewer's first chat message of a stream session.</summary>
    public const string FirstChatOfStream = "core.chat.first_of_stream";

    /// <summary>Fires when a viewer follows the channel.</summary>
    public const string Follow = "core.stream.follow";

    /// <summary>Fires when a viewer subscribes or renews a subscription.</summary>
    public const string Subscription = "core.stream.subscription";

    /// <summary>Fires when a viewer gifts subscriptions.</summary>
    public const string GiftSub = "core.stream.gift_sub";

    /// <summary>Fires when a viewer cheers bits or sends a paid chat cheer.</summary>
    public const string Cheer = "core.stream.cheer";

    /// <summary>Fires on external or native tip/donation events.</summary>
    public const string Tip = "core.stream.tip";

    /// <summary>Fires when another channel raids the workspace channel.</summary>
    public const string Raid = "core.stream.raid";

    /// <summary>Fires when a viewer redeems a channel point reward.</summary>
    public const string ChannelPointRedemption = "core.stream.redemption";

    /// <summary>Fires when the stream goes live.</summary>
    public const string StreamOnline = "core.stream.online";

    /// <summary>Fires when the stream goes offline.</summary>
    public const string StreamOffline = "core.stream.offline";

    /// <summary>Fires when a poll or prediction ends.</summary>
    public const string PollOrPredictionEnd = "core.stream.poll_prediction_end";

    /// <summary>Fires on a configured interval or 5-field UTC cron schedule.</summary>
    public const string TimerOrCron = "core.schedule.timer";

    /// <summary>Fires once at or after a configured UTC date and time.</summary>
    public const string OneShotSchedule = "core.schedule.one_shot";
}

/// <summary>
/// Thread-safe default implementation of <see cref="IActionBlockCatalog"/> supporting nested <c>CategoryPath</c> menus and live search.
/// </summary>
public sealed class ActionBlockCatalog : IActionBlockCatalog
{
    /// <summary>
    /// Registered action handlers keyed by <see cref="IPlatformActionHandler.ActionId"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, IPlatformActionHandler> _actions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registered trigger descriptors keyed by <see cref="ITriggerDescriptor.TriggerId"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, ITriggerDescriptor> _triggers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBlockCatalog"/> class from DI-registered handlers and triggers.
    /// </summary>
    /// <param name="actions">Registered <see cref="IPlatformActionHandler"/> instances.</param>
    /// <param name="triggers">Registered <see cref="ITriggerDescriptor"/> instances.</param>
    public ActionBlockCatalog(
        IEnumerable<IPlatformActionHandler> actions,
        IEnumerable<ITriggerDescriptor> triggers)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(triggers);

        foreach (IPlatformActionHandler action in actions)
        {
            if (action is not null && !string.IsNullOrWhiteSpace(action.ActionId))
            {
                this._actions[action.ActionId.Trim()] = action;
            }
        }

        foreach (ITriggerDescriptor trigger in triggers)
        {
            if (trigger is not null && !string.IsNullOrWhiteSpace(trigger.TriggerId))
            {
                this._triggers[trigger.TriggerId.Trim()] = trigger;
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IPlatformActionHandler> GetAllActions() =>
        [.. this._actions.Values.OrderBy(static a => a.DisplayName, StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc/>
    public IPlatformActionHandler? GetAction(string actionId) =>
        string.IsNullOrWhiteSpace(actionId)
            ? null
            : this._actions.TryGetValue(actionId.Trim(), out IPlatformActionHandler? handler)
                ? handler
                : null;

    /// <inheritdoc/>
    public IReadOnlyList<ActionCategoryNode> BuildActionCategoryTree()
    {
        MutableCategoryNode root = new("Root");
        foreach (IPlatformActionHandler action in this._actions.Values)
        {
            MutableCategoryNode cursor = root;
            IReadOnlyList<string> path = action.CategoryPath;
            if (path.Count == 0)
            {
                cursor = cursor.GetOrAddChild("General");
            }
            else
            {
                foreach (string segment in path)
                {
                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        cursor = cursor.GetOrAddChild(segment.Trim());
                    }
                }
            }

            cursor.Actions.Add(action);
        }

        return [.. root.Children.Values
            .OrderBy(static c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static c => c.ToImmutable())];
    }

    /// <inheritdoc/>
    public IReadOnlyList<IPlatformActionHandler> SearchActions(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return this.GetAllActions();
        }

        string trimmed = query.Trim();
        return [.. this._actions.Values
            .Where(a =>
                a.DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || a.ActionId.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || a.CategoryPath.Any(segment => segment.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.DisplayName.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ThenBy(static a => a.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<ITriggerDescriptor> GetAllTriggers() =>
        [.. this._triggers.Values.OrderBy(static t => t.DisplayName, StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc/>
    public ITriggerDescriptor? GetTrigger(string triggerId) =>
        string.IsNullOrWhiteSpace(triggerId)
            ? null
            : this._triggers.TryGetValue(triggerId.Trim(), out ITriggerDescriptor? descriptor)
                ? descriptor
                : null;

    /// <summary>
    /// Mutable tree builder node for <see cref="BuildActionCategoryTree"/>.
    /// </summary>
    /// <param name="name">Node display name.</param>
    private sealed class MutableCategoryNode(string name)
    {
        /// <summary>Gets the category name.</summary>
        public string Name { get; } = name;

        /// <summary>Gets child nodes.</summary>
        public Dictionary<string, MutableCategoryNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets actions directly under this node.</summary>
        public List<IPlatformActionHandler> Actions { get; } = [];

        /// <summary>Gets or creates a child category node.</summary>
        public MutableCategoryNode GetOrAddChild(string childName)
        {
            if (!this.Children.TryGetValue(childName, out MutableCategoryNode? child))
            {
                child = new MutableCategoryNode(childName);
                this.Children[childName] = child;
            }

            return child;
        }

        /// <summary>Freezes this node and all descendants into immutable <see cref="ActionCategoryNode"/> records.</summary>
        public ActionCategoryNode ToImmutable() => new(
            this.Name,
            [.. this.Children.Values.OrderBy(static c => c.Name, StringComparer.OrdinalIgnoreCase).Select(static c => c.ToImmutable())],
            [.. this.Actions.OrderBy(static a => a.DisplayName, StringComparer.OrdinalIgnoreCase)]);
    }
}

/// <summary>
/// Simple immutable descriptor implementation for built-in triggers.
/// </summary>
public sealed record BuiltInTriggerDescriptor(
    string TriggerId,
    string PlatformId,
    IReadOnlyList<string> CategoryPath,
    string DisplayName,
    string Description,
    IReadOnlyList<TaskParameterDefinition> OutputParameters,
    bool SupportsSchedule = false) : ITriggerDescriptor;

/// <summary>
/// Factory producing built-in platform-agnostic <see cref="ITriggerDescriptor"/> and <see cref="IPlatformActionHandler"/> definitions.
/// </summary>
public static class CoreActionBlockCatalogFactory
{
    /// <summary>
    /// Creates all built-in <see cref="ITriggerDescriptor"/> instances.
    /// </summary>
    /// <returns>Built-in trigger descriptors.</returns>
    public static IReadOnlyList<ITriggerDescriptor> CreateDefaultTriggers() =>
    [
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.ChatMessage,
            "core",
            ["Chat"],
            "On Chat Message",
            "Fires whenever a chat message is received in the workspace.",
            [
                new("senderLogin", "Sender Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderLogin"),
                new("senderDisplayName", "Sender Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderDisplayName"),
                new("message", "Message Text", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "message"),
                new("platformId", "Platform ID", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "platformId"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.ChatCommand,
            "core",
            ["Chat"],
            "On Command",
            "Fires when a chat command trigger is invoked.",
            [
                new("senderLogin", "Sender Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderLogin"),
                new("senderDisplayName", "Sender Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderDisplayName"),
                new("command", "Command Trigger", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "command"),
                new("rawArgs", "Raw Arguments", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "rawArgs"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.ChatKeyword,
            "core",
            ["Chat"],
            "On Keyword",
            "Fires when a chat message matches a configured keyword or regex pattern.",
            [
                new("senderLogin", "Sender Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderLogin"),
                new("matchedKeyword", "Matched Keyword", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "matchedKeyword"),
                new("message", "Message Text", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "message"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.FirstChatOfStream,
            "core",
            ["Chat"],
            "On First Chat of Stream",
            "Fires on a viewer's first chat message in the current stream session.",
            [
                new("senderLogin", "Sender Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderLogin"),
                new("senderDisplayName", "Sender Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "senderDisplayName"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.Follow,
            "core",
            ["Stream Events"],
            "On Follow",
            "Fires when a viewer follows the channel.",
            [
                new("followerLogin", "Follower Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "followerLogin"),
                new("followerDisplayName", "Follower Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "followerDisplayName"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.Subscription,
            "core",
            ["Stream Events", "Subscriptions"],
            "On Subscription",
            "Fires when a viewer subscribes or renews a channel subscription.",
            [
                new("subscriberLogin", "Subscriber Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "subscriberLogin"),
                new("subscriberDisplayName", "Subscriber Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "subscriberDisplayName"),
                new("tier", "Subscription Tier", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "tier"),
                new("cumulativeMonths", "Cumulative Months", ActionInputKind.Integer, PopulatedFromTriggerKey: "cumulativeMonths"),
                new("streakMonths", "Streak Months", ActionInputKind.Integer, PopulatedFromTriggerKey: "streakMonths"),
                new("isGift", "Is Gift", ActionInputKind.Boolean, PopulatedFromTriggerKey: "isGift"),
                new("message", "Resub Message", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "message"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.GiftSub,
            "core",
            ["Stream Events", "Subscriptions"],
            "On Gift Sub",
            "Fires when a viewer gifts one or more subscriptions.",
            [
                new("gifterLogin", "Gifter Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "gifterLogin"),
                new("gifterDisplayName", "Gifter Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "gifterDisplayName"),
                new("recipientLogin", "Recipient Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "recipientLogin"),
                new("tier", "Subscription Tier", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "tier"),
                new("giftCount", "Gift Count", ActionInputKind.Integer, PopulatedFromTriggerKey: "giftCount"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.Cheer,
            "core",
            ["Stream Events", "Monetization"],
            "On Cheer / Bits",
            "Fires when a viewer cheers bits.",
            [
                new("cheererLogin", "Cheerer Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "cheererLogin"),
                new("cheererDisplayName", "Cheerer Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "cheererDisplayName"),
                new("bits", "Bits Amount", ActionInputKind.Integer, PopulatedFromTriggerKey: "bits"),
                new("message", "Cheer Message", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "message"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.Tip,
            "core",
            ["Stream Events", "Monetization"],
            "On Tip",
            "Fires when an external tip or Super Chat contribution is received.",
            [
                new("donorName", "Donor Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "donorName"),
                new("amount", "Amount", ActionInputKind.Decimal, PopulatedFromTriggerKey: "amount"),
                new("currency", "Currency", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "currency"),
                new("message", "Message", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "message"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.Raid,
            "core",
            ["Stream Events"],
            "On Raid",
            "Fires when another broadcaster raids the channel.",
            [
                new("raiderLogin", "Raider Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "raiderLogin"),
                new("raiderDisplayName", "Raider Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "raiderDisplayName"),
                new("viewerCount", "Viewer Count", ActionInputKind.Integer, PopulatedFromTriggerKey: "viewerCount"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.ChannelPointRedemption,
            "core",
            ["Stream Events"],
            "On Channel Point Redemption",
            "Fires when a viewer redeems a channel point reward.",
            [
                new("redeemerLogin", "Redeemer Login", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "redeemerLogin"),
                new("redeemerDisplayName", "Redeemer Display Name", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "redeemerDisplayName"),
                new("rewardId", "Reward ID", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "rewardId"),
                new("rewardTitle", "Reward Title", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "rewardTitle"),
                new("userInput", "User Input", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "userInput"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.StreamOnline,
            "core",
            ["Stream Lifecycle"],
            "On Stream Online",
            "Fires when the primary stream transitions to online.",
            [
                new("platformId", "Platform ID", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "platformId"),
                new("gameCategory", "Game / Category", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "gameCategory"),
                new("title", "Stream Title", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "title"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.StreamOffline,
            "core",
            ["Stream Lifecycle"],
            "On Stream Offline",
            "Fires when the primary stream goes offline.",
            [
                new("platformId", "Platform ID", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "platformId"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.PollOrPredictionEnd,
            "core",
            ["Stream Events"],
            "On Poll / Prediction End",
            "Fires when a channel poll or prediction finishes.",
            [
                new("eventKind", "Event Kind", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "eventKind"),
                new("title", "Title", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "title"),
                new("winningOutcome", "Winning Outcome", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "winningOutcome"),
            ]),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.TimerOrCron,
            "core",
            ["Schedules"],
            "Timer / Cron",
            "Fires on an interval or 5-field UTC cron schedule (off-stream requires feature.action_blocks.offline_schedule).",
            [
                new("firedAtUtc", "Fired At (UTC)", ActionInputKind.TemplateString, PopulatedFromTriggerKey: "firedAtUtc"),
                new("isStreamLive", "Is Stream Live", ActionInputKind.Boolean, PopulatedFromTriggerKey: "isStreamLive"),
            ],
            SupportsSchedule: true),
        new BuiltInTriggerDescriptor(
            BuiltInTriggerIds.OneShotSchedule,
            "core",
            ["Schedules"],
            "One-Shot Date / Time",
            "Fires once at or after a scheduled UTC date and time (off-stream requires feature.action_blocks.offline_schedule).",
            [
                new("scheduledAtUtc", "Scheduled At (UTC)", ActionInputKind.DateTime, PopulatedFromTriggerKey: "scheduledAtUtc"),
                new("firedAtUtc", "Fired At (UTC)", ActionInputKind.DateTime, PopulatedFromTriggerKey: "firedAtUtc"),
                new("isStreamLive", "Is Stream Live", ActionInputKind.Boolean, PopulatedFromTriggerKey: "isStreamLive"),
            ],
            SupportsSchedule: true),
    ];

    /// <summary>
    /// Creates all built-in core <see cref="IPlatformActionHandler"/> instances.
    /// </summary>
    /// <returns>Core platform action handlers.</returns>
    public static IReadOnlyList<IPlatformActionHandler> CreateDefaultActions() =>
    [
        new SafeHttpActionHandler(),
        new KeyValueSetActionHandler(),
        new KeyValueIncrementActionHandler(),
        new CounterIncrementActionHandler(),
    ];

    /// <summary>
    /// Core action executing an SSRF-safe outbound HTTP request via <see cref="ISafeExternalHttpClient"/>.
    /// </summary>
    private sealed class SafeHttpActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "core.http.request";

        /// <inheritdoc/>
        public string PlatformId => "core";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Core", "HTTP"];

        /// <inheritdoc/>
        public string DisplayName => "Safe External HTTP Request";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public string DefaultOutputVariableName => "httpResponse";

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("url", "Target URL", ActionInputKind.TemplateString, IsRequired: true),
            new("method", "HTTP Method", ActionInputKind.SelectList, IsRequired: true, DefaultValue: "GET", AllowedValues: ["GET", "POST", "PUT", "PATCH", "DELETE"]),
            new("body", "Request Body", ActionInputKind.TemplateString, IsRequired: false),
            new("contentType", "Content-Type", ActionInputKind.TemplateString, IsRequired: false, DefaultValue: "application/json"),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            if (context.ServiceProvider?.GetService(typeof(ISafeExternalHttpClient)) is not ISafeExternalHttpClient httpClient)
            {
                return null;
            }

            string url = resolvedParameters.GetValueOrDefault("url") ?? string.Empty;
            string methodText = resolvedParameters.GetValueOrDefault("method") ?? "GET";
            string? body = resolvedParameters.TryGetValue("body", out string? rawBody) && !string.IsNullOrEmpty(rawBody) ? rawBody : null;
            string? contentType = resolvedParameters.TryGetValue("contentType", out string? rawCt) && !string.IsNullOrWhiteSpace(rawCt) ? rawCt : null;

            SafeExternalHttpResponse response = await httpClient.SendAsync(
                new SafeExternalHttpRequest(
                    Url: url,
                    Method: new HttpMethod(methodText.Trim().ToUpperInvariant()),
                    Body: body,
                    ContentType: contentType,
                    WorkspaceId: context.WorkspaceId),
                cancellationToken).ConfigureAwait(false);

            return response.Body;
        }
    }

    /// <summary>
    /// Core action writing a value into the workspace key-value store.
    /// </summary>
    private sealed class KeyValueSetActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "core.kv.set";

        /// <inheritdoc/>
        public string PlatformId => "core";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Core", "Storage", "Key-Value"];

        /// <inheritdoc/>
        public string DisplayName => "Set Key-Value Entry";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("table", "Table", ActionInputKind.TemplateString, IsRequired: true, DefaultValue: "action_blocks"),
            new("section", "Section", ActionInputKind.TemplateString, IsRequired: true, DefaultValue: "default"),
            new("key", "Key", ActionInputKind.TemplateString, IsRequired: true),
            new("value", "Value", ActionInputKind.TemplateString, IsRequired: true),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            if (context.ServiceProvider?.GetService(typeof(IWorkspaceKeyValueStore)) is not IWorkspaceKeyValueStore kvStore)
            {
                return null;
            }

            string table = resolvedParameters.GetValueOrDefault("table") ?? "action_blocks";
            string section = resolvedParameters.GetValueOrDefault("section") ?? "default";
            string key = resolvedParameters.GetValueOrDefault("key") ?? string.Empty;
            string value = resolvedParameters.GetValueOrDefault("value") ?? string.Empty;

            await kvStore.SetAsync(context.WorkspaceId, table, section, key, value, cancellationToken).ConfigureAwait(false);
            return value;
        }
    }

    /// <summary>
    /// Core action atomically incrementing a long integer in the workspace key-value store.
    /// </summary>
    private sealed class KeyValueIncrementActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "core.kv.increment";

        /// <inheritdoc/>
        public string PlatformId => "core";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Core", "Storage", "Key-Value"];

        /// <inheritdoc/>
        public string DisplayName => "Increment Key-Value Entry";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionRunCommand;

        /// <inheritdoc/>
        public string DefaultOutputVariableName => "kvValue";

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("table", "Table", ActionInputKind.TemplateString, IsRequired: true, DefaultValue: "action_blocks"),
            new("section", "Section", ActionInputKind.TemplateString, IsRequired: true, DefaultValue: "default"),
            new("key", "Key", ActionInputKind.TemplateString, IsRequired: true),
            new("delta", "Delta", ActionInputKind.Integer, IsRequired: true, DefaultValue: "1"),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            if (context.ServiceProvider?.GetService(typeof(IWorkspaceKeyValueStore)) is not IWorkspaceKeyValueStore kvStore)
            {
                return null;
            }

            string table = resolvedParameters.GetValueOrDefault("table") ?? "action_blocks";
            string section = resolvedParameters.GetValueOrDefault("section") ?? "default";
            string key = resolvedParameters.GetValueOrDefault("key") ?? string.Empty;
            long delta = long.TryParse(resolvedParameters.GetValueOrDefault("delta"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : 1L;

            long next = await kvStore.IncrementLongAsync(context.WorkspaceId, table, section, key, delta, cancellationToken).ConfigureAwait(false);
            return next.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Core action incrementing a named workspace counter.
    /// </summary>
    private sealed class CounterIncrementActionHandler : IPlatformActionHandler
    {
        /// <inheritdoc/>
        public string ActionId => "core.counter.increment";

        /// <inheritdoc/>
        public string PlatformId => "core";

        /// <inheritdoc/>
        public IReadOnlyList<string> CategoryPath => ["Core", "Counters"];

        /// <inheritdoc/>
        public string DisplayName => "Increment Workspace Counter";

        /// <inheritdoc/>
        public string RequiredPermissionKey => BuiltInPermissionKeys.ActionManageCounters;

        /// <inheritdoc/>
        public string DefaultOutputVariableName => "counterValue";

        /// <inheritdoc/>
        public IReadOnlyList<ActionParameterSchema> Parameters =>
        [
            new("name", "Counter Name", ActionInputKind.TemplateString, IsRequired: true),
            new("delta", "Delta", ActionInputKind.Integer, IsRequired: true, DefaultValue: "1"),
            new("gameCategory", "Game / Category (optional)", ActionInputKind.TemplateString, IsRequired: false),
        ];

        /// <inheritdoc/>
        public async ValueTask<string?> ExecuteAsync(
            IActionExecutionContext context,
            IReadOnlyDictionary<string, string> resolvedParameters,
            CancellationToken cancellationToken)
        {
            if (context.ServiceProvider?.GetService(typeof(ICounterStore)) is not ICounterStore counterStore)
            {
                return null;
            }

            string counterName = resolvedParameters.GetValueOrDefault("name") ?? string.Empty;
            long delta = long.TryParse(resolvedParameters.GetValueOrDefault("delta"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : 1L;
            string? gameCategory = resolvedParameters.TryGetValue("gameCategory", out string? rawGame) && !string.IsNullOrWhiteSpace(rawGame)
                ? rawGame
                : null;
            CounterScopeMode scopeMode = gameCategory is null ? CounterScopeMode.Global : CounterScopeMode.PerGame;

            Models.Entities.WorkspaceCounter updated = await counterStore
                .AdjustAsync(context.WorkspaceId, counterName, delta, scopeMode, gameCategory ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            return updated.CurrentValue.ToString(CultureInfo.InvariantCulture);
        }
    }
}

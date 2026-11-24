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
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Scripting;

/// <summary>
/// Defines the automatic unloading lifecycle mode of a loaded ClearScript V8 isolate.
/// </summary>
public enum ScriptLifecycleMode
{
    /// <summary>
    /// Loaded during an active stream session and automatically unloaded after the post-stream grace period expires.
    /// </summary>
    StreamSession = 0,

    /// <summary>
    /// Loaded on demand off-stream and automatically unloaded after the configured idle inactivity timeout.
    /// </summary>
    EventOnDemand = 1,

    /// <summary>
    /// Permanently resident across stream sessions (requires <c>feature.scripting.clearscript.persistent</c>).
    /// </summary>
    PermanentResident = 2,
}

/// <summary>
/// Structured chat command metadata declared by a VFS ClearScript script outside the JavaScript source code.
/// </summary>
/// <param name="Trigger">The chat command trigger (for example, <c>!heist</c> or <c>heist</c>).</param>
/// <param name="DefaultAllowedDesignations">Default platform user designations granted access on initial registration.</param>
/// <param name="GlobalCooldownSeconds">Optional global cooldown in seconds.</param>
/// <param name="UserCooldownSeconds">Optional per-user cooldown in seconds.</param>
public sealed record ScriptCommandRegistration(
    string Trigger,
    PlatformUserDesignations DefaultAllowedDesignations = PlatformUserDesignations.Everyone,
    int GlobalCooldownSeconds = 0,
    int UserCooldownSeconds = 0);

/// <summary>
/// Represents a ClearScript V8 JavaScript file stored in the workspace Virtual File System (<c>WorkspaceScripts</c>).
/// </summary>
public sealed class WorkspaceScript
{
    /// <summary>
    /// Gets or sets the immutable internal script identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the owning workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the normalized VFS path within the workspace (for example, <c>scripts/commands/heist.js</c> or <c>lib/helpers.js</c>).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JavaScript source code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the script is enabled for execution.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the script stays permanently resident in V8 memory 24/7 (requires <c>feature.scripting.clearscript.persistent</c>).
    /// </summary>
    public bool IsPermanentlyLoaded { get; set; }

    /// <summary>
    /// Gets or sets the structured chat commands registered by this script.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Mutable EF Core entity list storing script command registrations.")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Mutable EF Core entity property replaced during store updates.")]
    public List<ScriptCommandRegistration> RegisteredCommands { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when this script was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Request payload for executing a workspace VFS script or exported entry function from an Action Block.
/// </summary>
public sealed class ScriptExecutionRequest
{
    /// <summary>
    /// Gets or sets the owning workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the target VFS script identifier.
    /// </summary>
    public Guid ScriptId { get; set; }

    /// <summary>
    /// Gets or sets the optional exported function name on <c>$</c> or script scope to invoke.
    /// </summary>
    public string? EntryFunction { get; set; }

    /// <summary>
    /// Gets or sets resolved arguments passed to the entry function or event payload.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Arguments { get; set; }

    /// <summary>
    /// Gets or sets the originating platform identifier.
    /// </summary>
    public string PlatformId { get; set; } = "twitch";

    /// <summary>
    /// Gets or sets the originating channel identifier.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional actor identity triggering the script.
    /// </summary>
    public PlatformUserIdentity? ActorIdentity { get; set; }

    /// <summary>
    /// Gets or sets a callback to read a variable from the enclosing Action Block flow.
    /// </summary>
    public Func<string, string?>? GetFlowVariable { get; set; }

    /// <summary>
    /// Gets or sets a callback to write a variable back to the enclosing Action Block flow.
    /// </summary>
    public Action<string, string>? SetFlowVariable { get; set; }

    /// <summary>
    /// Gets or sets a callback to send or dry-run capture an outbound chat message.
    /// </summary>
    public Func<string, CancellationToken, ValueTask>? SendChatAsync { get; set; }
}

/// <summary>
/// Result returned after executing a ClearScript V8 script invocation.
/// </summary>
/// <param name="Succeeded">Whether the script executed without errors, timeouts, or quota failures.</param>
/// <param name="ReturnValue">Optional string representation of the entry function return value.</param>
/// <param name="ErrorMessage">Optional error message when <paramref name="Succeeded"/> is <see langword="false"/>.</param>
public sealed record ScriptExecutionResult(
    bool Succeeded,
    string? ReturnValue = null,
    string? ErrorMessage = null);

/// <summary>
/// Event payload dispatched to scripts subscribed via <c>$.bind(eventName, handler)</c> or <c>$.on(eventName, handler)</c>.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier.</param>
/// <param name="EventName">The normalized event name (for example, <c>command</c>, <c>chatMessage</c>, <c>subscription</c>, <c>follow</c>).</param>
/// <param name="Payload">Serializable key-value event properties exposed to the JS event object.</param>
/// <param name="PlatformId">Originating platform identifier.</param>
/// <param name="ChannelId">Originating channel identifier.</param>
/// <param name="TargetScriptId">Optional specific script identifier to dispatch to (for example, a command-owning script).</param>
/// <param name="ActorIdentity">Optional actor identity.</param>
public sealed record ScriptEventDispatchRequest(
    Guid WorkspaceId,
    string EventName,
    IReadOnlyDictionary<string, object?> Payload,
    string PlatformId = "twitch",
    string ChannelId = "",
    Guid? TargetScriptId = null,
    PlatformUserIdentity? ActorIdentity = null);

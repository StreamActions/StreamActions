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

using Microsoft.ClearScript;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StreamActions.Scripting.Proxies;

/// <summary>
/// Workspace-pinned key-value script API exposed to JavaScript as <c>context.kv</c> (<c>$.inidb</c>-style convenience with strict tenant isolation).
/// </summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript method contract matching ClearScript context.kv specification.")]
public sealed class ScopedKeyValueScriptApi
{
    /// <summary>
    /// Workspace identifier pinned inside C# and unreachable from JavaScript.
    /// </summary>
    private readonly Guid _workspaceId;

    /// <summary>
    /// Backing workspace key-value store.
    /// </summary>
    private readonly IWorkspaceKeyValueStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedKeyValueScriptApi"/> class.
    /// </summary>
    /// <param name="workspaceId">The pinned workspace identifier.</param>
    /// <param name="store">The workspace key-value store.</param>
    public ScopedKeyValueScriptApi(Guid workspaceId, IWorkspaceKeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this._workspaceId = workspaceId;
        this._store = store;
    }

    /// <summary>Reads a string value from the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<string?> getString(string table, string section, string key, string? defaultValue = null) =>
        this._store.GetStringAsync(this._workspaceId, table, section, key, defaultValue).AsTask();

    /// <summary>Reads a boolean value from the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<bool> getBool(string table, string section, string key, bool defaultValue = false) =>
        this._store.GetBoolAsync(this._workspaceId, table, section, key, defaultValue).AsTask();

    /// <summary>Reads a 32-bit integer value from the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<int> getInt(string table, string section, string key, int defaultValue = 0) =>
        this._store.GetIntAsync(this._workspaceId, table, section, key, defaultValue).AsTask();

    /// <summary>Reads a 64-bit integer value from the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<long> getLong(string table, string section, string key, long defaultValue = 0L) =>
        this._store.GetLongAsync(this._workspaceId, table, section, key, defaultValue).AsTask();

    /// <summary>Reads a single-precision floating-point value from the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<float> getFloat(string table, string section, string key, float defaultValue = 0f) =>
        this._store.GetFloatAsync(this._workspaceId, table, section, key, defaultValue).AsTask();

    /// <summary>Reads a double-precision floating-point value from the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<double> getDouble(string table, string section, string key, double defaultValue = 0d) =>
        this._store.GetDoubleAsync(this._workspaceId, table, section, key, defaultValue).AsTask();

    /// <summary>Writes a value into the workspace key-value store.</summary>
    public Task set(string table, string section, string key, object? value) =>
        this._store.SetAsync(this._workspaceId, table, section, key, value).AsTask();

    /// <summary>Atomically increments a 64-bit integer key in the workspace key-value store.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "JavaScript host API method names match the specification.")]
    public Task<long> incrementLong(string table, string section, string key, long delta = 1L) =>
        this._store.IncrementLongAsync(this._workspaceId, table, section, key, delta).AsTask();

    /// <summary>Deletes a key from the workspace key-value store.</summary>
    public Task<bool> delete(string table, string section, string key) =>
        this._store.DeleteAsync(this._workspaceId, table, section, key).AsTask();

    /// <summary>Lists all variable keys in the specified table and section.</summary>
    public Task<IReadOnlyList<string>> getKeys(string table, string section) =>
        this._store.GetKeysAsync(this._workspaceId, table, section).AsTask();
}

/// <summary>
/// JavaScript-facing HTTP response returned by <c>context.fetch(url, options)</c>.
/// </summary>
/// <param name="status">The HTTP status code.</param>
/// <param name="ok">Whether the status code is in the 2xx range.</param>
/// <param name="body">The response body (bounded to 64 KB).</param>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase camelCase property names form the JavaScript object contract.")]
public sealed record ScriptFetchResponse(int status, bool ok, string body);

/// <summary>
/// Host <c>context</c> object injected into every sandboxed ClearScript V8 isolate.
/// </summary>
public sealed partial class ScriptContextObject
{
    /// <summary>
    /// Workspace identifier pinned inside C#.
    /// </summary>
    private readonly Guid _workspaceId;

    /// <summary>
    /// Optional SSRF-safe HTTP client.
    /// </summary>
    private readonly ISafeExternalHttpClient? _safeExternalHttpClient;

    /// <summary>
    /// Optional outbound chat dispatcher.
    /// </summary>
    private readonly IOutboundChatDispatcher? _outboundChatDispatcher;

    /// <summary>
    /// Optional logger.
    /// </summary>
    private readonly ILogger? _logger;

    /// <summary>
    /// Active per-invocation variable read delegate.
    /// </summary>
    private readonly AsyncLocal<Func<string, string?>?> _activeGetVar = new();

    /// <summary>
    /// Active per-invocation variable write delegate.
    /// </summary>
    private readonly AsyncLocal<Action<string, string>?> _activeSetVar = new();

    /// <summary>
    /// Active per-invocation chat delivery delegate.
    /// </summary>
    private readonly AsyncLocal<Func<string, CancellationToken, ValueTask>?> _activeSendChat = new();

    /// <summary>
    /// Active per-invocation channel/platform routing metadata.
    /// </summary>
    private readonly AsyncLocal<(string PlatformId, string ChannelId)?> _activeRoute = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptContextObject"/> class.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="keyValueStore">Workspace key-value store.</param>
    /// <param name="safeExternalHttpClient">Optional SSRF-safe external HTTP client.</param>
    /// <param name="outboundChatDispatcher">Optional outbound chat dispatcher.</param>
    /// <param name="logger">Optional logger.</param>
    public ScriptContextObject(
        Guid workspaceId,
        IWorkspaceKeyValueStore keyValueStore,
        ISafeExternalHttpClient? safeExternalHttpClient = null,
        IOutboundChatDispatcher? outboundChatDispatcher = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(keyValueStore);

        this._workspaceId = workspaceId;
        this.kv = new ScopedKeyValueScriptApi(workspaceId, keyValueStore);
        this._safeExternalHttpClient = safeExternalHttpClient;
        this._outboundChatDispatcher = outboundChatDispatcher;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the workspace-scoped key-value store API (<c>context.kv</c>).
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript property contract.")]
    public ScopedKeyValueScriptApi kv { get; }

    /// <summary>
    /// Reads a variable from the enclosing Action Block flow.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript method contract.")]
    public string? getVar(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Func<string, string?>? getter = this._activeGetVar.Value;
        return getter?.Invoke(name.Trim());
    }

    /// <summary>
    /// Writes a variable into the enclosing Action Block flow.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript method contract.")]
    public void setVar(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        Action<string, string>? setter = this._activeSetVar.Value;
        setter?.Invoke(name.Trim(), Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    /// <summary>
    /// Enqueues an outbound chat message in the active workspace channel.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript method contract.")]
    public async Task sendChat(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Func<string, CancellationToken, ValueTask>? sendCallback = this._activeSendChat.Value;
        if (sendCallback is not null)
        {
            await sendCallback(message, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        if (this._outboundChatDispatcher is not null)
        {
            (string PlatformId, string ChannelId) = this._activeRoute.Value ?? ("twitch", string.Empty);
            await this._outboundChatDispatcher.EnqueueAsync(
                new OutboundChatMessage(
                    this._workspaceId,
                    PlatformId,
                    ChannelId,
                    message,
                    OutboundChatPriority.CommandReply)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs an SSRF-safe outbound HTTP request supporting <c>$secret.NAME</c> injection.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript method contract.")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Raw URL string supports $secret.NAME placeholders validated by SafeExternalHttpClient.")]
    public async Task<ScriptFetchResponse> fetch(string url, object? options = null)
    {
        if (this._safeExternalHttpClient is null)
        {
            throw new InvalidOperationException("SafeExternalHttpClient is not configured.");
        }

        HttpMethod method = HttpMethod.Get;
        string? body = null;
        string? contentType = null;
        Dictionary<string, string>? headers = null;

        if (options is ScriptObject opt)
        {
            if (opt.GetProperty("method") is string m && !string.IsNullOrWhiteSpace(m))
            {
                method = new HttpMethod(m.Trim().ToUpperInvariant());
            }

            if (opt.GetProperty("body") is string b)
            {
                body = b;
            }

            if (opt.GetProperty("contentType") is string ct && !string.IsNullOrWhiteSpace(ct))
            {
                contentType = ct;
            }

            if (opt.GetProperty("headers") is ScriptObject hdrObj)
            {
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string headerName in hdrObj.PropertyNames)
                {
                    if (hdrObj.GetProperty(headerName) is string headerVal)
                    {
                        headers[headerName] = headerVal;
                    }
                }
            }
        }

        SafeExternalHttpResponse response = await this._safeExternalHttpClient.SendAsync(
            new SafeExternalHttpRequest(
                Url: url,
                Method: method,
                Headers: headers,
                Body: body,
                ContentType: contentType,
                WorkspaceId: this._workspaceId)).ConfigureAwait(false);

        return new ScriptFetchResponse(response.StatusCode, response.IsSuccessStatusCode, response.Body);
    }

    /// <summary>
    /// Logs an informational diagnostic message emitted from a workspace ClearScript script.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Workspace {WorkspaceId} script log: {Message}")]
    private static partial void LogScriptMessage(ILogger logger, Guid workspaceId, string message);

    /// <summary>
    /// Logs an informational diagnostic message for this workspace script.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase JavaScript method contract.")]
    public void log(string message)
    {
        if (this._logger is not null)
        {
            LogScriptMessage(this._logger, this._workspaceId, message);
        }
    }

    /// <summary>
    /// Pushes per-invocation callbacks onto <see cref="AsyncLocal{T}"/> slots for the duration of a script call.
    /// </summary>
    internal IDisposable PushInvocationScope(
        string platformId,
        string channelId,
        Func<string, string?>? getVar,
        Action<string, string>? setVar,
        Func<string, CancellationToken, ValueTask>? sendChat)
    {
        Func<string, string?>? prevGet = this._activeGetVar.Value;
        Action<string, string>? prevSet = this._activeSetVar.Value;
        Func<string, CancellationToken, ValueTask>? prevChat = this._activeSendChat.Value;
        (string PlatformId, string ChannelId)? prevRoute = this._activeRoute.Value;

        this._activeGetVar.Value = getVar;
        this._activeSetVar.Value = setVar;
        this._activeSendChat.Value = sendChat;
        this._activeRoute.Value = (platformId, channelId);

        return new ScopeResetter(() =>
        {
            this._activeGetVar.Value = prevGet;
            this._activeSetVar.Value = prevSet;
            this._activeSendChat.Value = prevChat;
            this._activeRoute.Value = prevRoute;
        });
    }

    private sealed class ScopeResetter(Action onDispose) : IDisposable
    {
        private readonly Action _onDispose = onDispose;

        public void Dispose() => this._onDispose();
    }
}


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
using StreamActions.Scripting.Runtime;
using System.Dynamic;

namespace StreamActions.Scripting.Proxies;

/// <summary>
/// Descriptor returned to the JavaScript <c>$</c> proxy representing an exported property or cross-isolate callable.
/// </summary>
public sealed class DollarExportDescriptor
{
    /// <summary>
    /// Backing cross-isolate callable when <see cref="IsCallable"/> is <see langword="true"/>.
    /// </summary>
    private readonly DollarExportCallable? _callable;

    /// <summary>
    /// Initializes a value export descriptor.
    /// </summary>
    /// <param name="value">The exported value.</param>
    public DollarExportDescriptor(object? value) => this.Value = value;

    /// <summary>
    /// Initializes a callable export descriptor.
    /// </summary>
    /// <param name="callable">The cross-isolate callable wrapper.</param>
    internal DollarExportDescriptor(DollarExportCallable callable)
    {
        this._callable = callable;
        this.IsCallable = true;
    }

    /// <summary>Gets a value indicating whether the exported member is callable.</summary>
    public bool IsCallable { get; }

    /// <summary>Gets the non-callable exported value.</summary>
    public object? Value { get; }

    /// <summary>
    /// Invokes the cross-isolate callable with an argument array supplied by the JavaScript proxy.
    /// </summary>
    /// <param name="argsObject">A JavaScript array (<see cref="ScriptObject"/>) or .NET enumerable of arguments.</param>
    /// <returns>The marshaled return value or <see cref="Task{TResult}"/>.</returns>
    public object? InvokeArray(object? argsObject)
    {
        if (this._callable is null)
        {
            throw new InvalidOperationException("Exported '$' member is not callable.");
        }

        object?[] args = UnpackJsArgs(argsObject);
        return this._callable.Invoke(args);
    }

    private static object?[] UnpackJsArgs(object? argsObject)
    {
        if (argsObject is null or Undefined)
        {
            return [];
        }

        if (argsObject is ScriptObject scriptArray)
        {
            object? lenObj = scriptArray.GetProperty("length");
            if (lenObj is not null and not Undefined)
            {
                int length = Convert.ToInt32(lenObj, System.Globalization.CultureInfo.InvariantCulture);
                object?[] unpacked = new object?[length];
                for (int i = 0; i < length; i++)
                {
                    object? item = scriptArray.GetProperty(i);
                    unpacked[i] = item is Undefined ? null : item;
                }

                return unpacked;
            }
        }

        return [argsObject];
    }
}

/// <summary>
/// Per-script <c>$</c> root namespace bridge backed by the workspace's <see cref="WorkspaceScriptEnvironment"/>.
/// Tags every assigned export and <c>$.bind</c>/<c>$.on</c> event subscription with <c>ownerScriptId</c>
/// and marshals cross-isolate calls cleanly across separate <see cref="Microsoft.ClearScript.V8.V8ScriptEngine"/> instances.
/// </summary>
public sealed class ScopedDollarProxy : DynamicObject
{
    /// <summary>
    /// Parent workspace script environment holding the shared <c>$</c> registry and event bindings.
    /// </summary>
    private readonly WorkspaceScriptEnvironment _environment;

    /// <summary>
    /// Owning VFS script identifier for exports and event subscriptions created through this proxy.
    /// </summary>
    private readonly Guid _ownerScriptId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedDollarProxy"/> class.
    /// </summary>
    /// <param name="environment">The workspace script environment.</param>
    /// <param name="ownerScriptId">The owning VFS script identifier.</param>
    internal ScopedDollarProxy(WorkspaceScriptEnvironment environment, Guid ownerScriptId)
    {
        ArgumentNullException.ThrowIfNull(environment);

        this._environment = environment;
        this._ownerScriptId = ownerScriptId;
    }

    /// <summary>
    /// Registers an event handler bound via <c>$.bind(eventName, handler)</c> or <c>$.on(eventName, handler)</c>.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The JavaScript handler function.</param>
    public void BindEvent(string eventName, object? handler) =>
        this._environment.RegisterEventBinding(this._ownerScriptId, eventName, handler);

    /// <summary>
    /// Assigns an exported value or callable onto the workspace <c>$</c> namespace tagged with this script's identifier.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The exported value or JavaScript function.</param>
    public void SetExport(string name, object? value)
    {
        if (string.Equals(name, "bind", StringComparison.Ordinal)
            || string.Equals(name, "on", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot overwrite reserved '$' method '{name}'.");
        }

        if (string.Equals(name, nameof(GetType), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Reflection access is forbidden inside the ClearScript sandbox.");
        }

        this._environment.SetDollarExport(this._ownerScriptId, name, value);
    }

    /// <summary>
    /// Retrieves an exported descriptor for the specified <paramref name="name"/>, or <see langword="null"/> if undefined.
    /// </summary>
    /// <param name="name">The exported property name.</param>
    /// <returns>The <see cref="DollarExportDescriptor"/> or <see langword="null"/>.</returns>
    public DollarExportDescriptor? GetExport(string name) =>
        string.Equals(name, nameof(GetType), StringComparison.Ordinal)
            ? throw new UnauthorizedAccessException("Reflection access via GetType() is forbidden inside the ClearScript sandbox.")
            : !this._environment.TryGetDollarExport(name, out object? exported)
            ? null
            : exported is DollarExportCallable callable
            ? new DollarExportDescriptor(callable)
            : new DollarExportDescriptor(exported);
}

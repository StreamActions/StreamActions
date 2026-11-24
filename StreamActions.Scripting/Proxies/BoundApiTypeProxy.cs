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
using StreamActions.Twitch.Api.Common;
using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Reflection;

namespace StreamActions.Scripting.Proxies;

/// <summary>
/// Wraps any static API record in <c>StreamActions.Twitch.Api</c> (for example, <c>Game</c> or <c>CreatedClip</c>)
/// and automatically prepends the workspace's scoped <see cref="TwitchSession"/> as parameter 0.
/// The underlying <see cref="TwitchSession"/> and <see cref="TwitchToken"/> are never exposed to JavaScript.
/// </summary>
public sealed class BoundApiTypeProxy : DynamicObject
{
    /// <summary>
    /// Scoped Twitch session prepended to all static API calls.
    /// </summary>
    private readonly TwitchSession _session;

    /// <summary>
    /// Target <c>StreamActions.Twitch.Api.*</c> static API record type.
    /// </summary>
    private readonly Type _apiRecordType;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundApiTypeProxy"/> class.
    /// </summary>
    /// <param name="session">The workspace-scoped <see cref="TwitchSession"/>.</param>
    /// <param name="apiRecordType">The Twitch API record type to proxy.</param>
    public BoundApiTypeProxy(TwitchSession session, Type apiRecordType)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(apiRecordType);

        this._session = session;
        this._apiRecordType = apiRecordType;
    }

    /// <summary>
    /// Invokes a static method on the proxied Twitch API record from a ClearScript JavaScript <c>Proxy</c>,
    /// prepending the workspace-scoped <see cref="TwitchSession"/> automatically.
    /// </summary>
    /// <param name="memberName">The static method name.</param>
    /// <param name="argsObject">A JavaScript arguments array or single argument.</param>
    /// <returns>The underlying API method return value (such as a <see cref="Task{TResult}"/>).</returns>
    public object? InvokeMember(string memberName, object? argsObject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        if (string.Equals(memberName, nameof(GetType), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Reflection access via GetType() is forbidden inside the ClearScript sandbox.");
        }

        object?[] suppliedArgs = UnpackJsArgs(argsObject);
        return this.InvokeCore(memberName, suppliedArgs, out object? result)
            ? result
            : throw new MissingMethodException($"Method '{memberName}' was not found on '{this._apiRecordType.Name}'.");
    }

    /// <inheritdoc/>
    public override bool TryInvokeMember(
        InvokeMemberBinder binder,
        object?[]? args,
        out object? result)
    {
        ArgumentNullException.ThrowIfNull(binder);

        // Block reflection escapes via GetType even if invoked dynamically.
        return string.Equals(binder.Name, nameof(GetType), StringComparison.Ordinal)
            ? throw new UnauthorizedAccessException("Reflection access via GetType() is forbidden inside the ClearScript sandbox.")
            : this.InvokeCore(binder.Name, args ?? [], out result);
    }

    private bool InvokeCore(string methodName, object?[] suppliedArgs, out object? result)
    {
        MethodInfo[] candidates = [.. this._apiRecordType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))];

        if (candidates.Length == 0)
        {
            result = null;
            return false;
        }

        object?[] callArgs = [this._session, .. suppliedArgs];
        foreach (MethodInfo method in candidates)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(TwitchSession))
            {
                continue;
            }

            if (callArgs.Length > parameters.Length)
            {
                continue;
            }

            if (TryBindArguments(parameters, callArgs, out object?[]? boundArgs))
            {
                try
                {
                    result = method.Invoke(null, boundArgs);
                    return true;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
            }
        }

        result = null;
        return false;
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
                int length = Convert.ToInt32(lenObj, CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Attempts to convert ClearScript JS arguments and fill trailing optional parameters for a target <see cref="MethodInfo"/>.
    /// </summary>
    private static bool TryBindArguments(
        ParameterInfo[] parameters,
        object?[] callArgs,
        out object?[]? boundArgs)
    {
        boundArgs = new object?[parameters.Length];
        boundArgs[0] = callArgs[0];

        for (int i = 1; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            if (i < callArgs.Length)
            {
                if (!TryConvertArgument(callArgs[i], parameter.ParameterType, out object? converted))
                {
                    boundArgs = null;
                    return false;
                }

                boundArgs[i] = converted;
            }
            else if (parameter.HasDefaultValue)
            {
                boundArgs[i] = parameter.DefaultValue;
            }
            else
            {
                boundArgs = null;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts JavaScript values (including ClearScript <see cref="ScriptObject"/> arrays) into expected .NET parameter types.
    /// </summary>
    private static bool TryConvertArgument(object? value, Type targetType, out object? converted)
    {
        if (value is null or Undefined)
        {
            converted = targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
        }

        Type nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullableType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (nonNullableType == typeof(IEnumerable<string>) || nonNullableType == typeof(IReadOnlyList<string>))
        {
            if (TryConvertStringSequence(value, out string[]? sequence))
            {
                converted = sequence;
                return true;
            }
        }

        if (nonNullableType.IsEnum)
        {
            if (value is string enumText && Enum.TryParse(nonNullableType, enumText, ignoreCase: true, out object? parsedEnum))
            {
                converted = parsedEnum;
                return true;
            }
        }

        if (nonNullableType == typeof(string))
        {
            converted = Convert.ToString(value, CultureInfo.InvariantCulture);
            return true;
        }

        if (nonNullableType == typeof(int) || nonNullableType == typeof(long) || nonNullableType == typeof(float) || nonNullableType == typeof(double) || nonNullableType == typeof(bool))
        {
            try
            {
                converted = Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                converted = null;
                return false;
            }
        }

        converted = null;
        return false;
    }

    /// <summary>
    /// Converts a ClearScript JS array (<see cref="ScriptObject"/> or <see cref="IList"/>) into a <see cref="string"/> array.
    /// </summary>
    private static bool TryConvertStringSequence(object value, out string[]? strings)
    {
        if (value is string single)
        {
            strings = [single];
            return true;
        }

        if (value is ScriptObject scriptObject)
        {
            object? lengthObj = scriptObject.GetProperty("length");
            if (lengthObj is not Undefined and not null)
            {
                int length = Convert.ToInt32(lengthObj, CultureInfo.InvariantCulture);
                string[] items = new string[length];
                for (int i = 0; i < length; i++)
                {
                    object? elem = scriptObject.GetProperty(i);
                    items[i] = Convert.ToString(elem, CultureInfo.InvariantCulture) ?? string.Empty;
                }

                strings = items;
                return true;
            }
        }

        if (value is IEnumerable enumerable)
        {
            List<string> list = [];
            foreach (object? item in enumerable)
            {
                if (item is not null)
                {
                    list.Add(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);
                }
            }

            strings = [.. list];
            return true;
        }

        strings = null;
        return false;
    }
}


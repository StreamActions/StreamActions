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

using StreamActions.Twitch.Api.Common;
using System.Dynamic;
using System.Reflection;

namespace StreamActions.Scripting.Proxies;

/// <summary>
/// Discovers all public static API records in <c>StreamActions.Twitch.Api.*</c> once at startup
/// and exposes them as a hierarchical <c>twitch.&lt;Namespace&gt;.&lt;Type&gt;</c> host proxy bound to a workspace <see cref="TwitchSession"/>.
/// </summary>
public sealed class BoundNamespaceProxy : DynamicObject
{
    /// <summary>
    /// Root namespace prefix discovered from <c>StreamActions.Twitch</c>.
    /// </summary>
    private const string _apiNamespacePrefix = "StreamActions.Twitch.Api.";

    /// <summary>
    /// Statically cached namespace tree discovered from <c>StreamActions.Twitch</c>.
    /// </summary>
    private static readonly NamespaceNode _rootNode = BuildNamespaceTree();

    /// <summary>
    /// Workspace-scoped session provider callback (lazily resolved only when an API call chain reaches a type).
    /// </summary>
    private readonly Func<TwitchSession> _sessionProvider;

    /// <summary>
    /// Current tree cursor.
    /// </summary>
    private readonly NamespaceNode _node;

    /// <summary>
    /// Initializes a new root <see cref="BoundNamespaceProxy"/> bound to a fixed <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The workspace-scoped <see cref="TwitchSession"/>.</param>
    public BoundNamespaceProxy(TwitchSession session)
        : this(() => session ?? throw new ArgumentNullException(nameof(session)), _rootNode) => ArgumentNullException.ThrowIfNull(session);

    /// <summary>
    /// Initializes a new root <see cref="BoundNamespaceProxy"/> using a lazy session factory.
    /// </summary>
    /// <param name="sessionProvider">Callback returning the workspace-scoped <see cref="TwitchSession"/>.</param>
    public BoundNamespaceProxy(Func<TwitchSession> sessionProvider)
        : this(sessionProvider, _rootNode)
    {
    }

    /// <summary>
    /// Initializes a child <see cref="BoundNamespaceProxy"/> for a sub-namespace node.
    /// </summary>
    private BoundNamespaceProxy(Func<TwitchSession> sessionProvider, NamespaceNode node)
    {
        ArgumentNullException.ThrowIfNull(sessionProvider);
        ArgumentNullException.ThrowIfNull(node);

        this._sessionProvider = sessionProvider;
        this._node = node;
    }

    /// <summary>
    /// Resolves a child sub-namespace (<see cref="BoundNamespaceProxy"/>) or static API proxy (<see cref="BoundApiTypeProxy"/>) by member name.
    /// </summary>
    /// <param name="memberName">The namespace segment or API record name.</param>
    /// <returns>The resolved proxy, or <see langword="null"/> if not found.</returns>
    public object? ResolveMember(string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        if (string.Equals(memberName, nameof(GetType), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Reflection access via GetType() is forbidden inside the ClearScript sandbox.");
        }

        if (this._node.Children.TryGetValue(memberName, out NamespaceNode? childNode))
        {
            return new BoundNamespaceProxy(this._sessionProvider, childNode);
        }

        if (this._node.ApiTypes.TryGetValue(memberName, out Type? apiType))
        {
            TwitchSession session = this._sessionProvider();
            return new BoundApiTypeProxy(session, apiType);
        }

        return null;
    }

    /// <inheritdoc/>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        ArgumentNullException.ThrowIfNull(binder);

        result = this.ResolveMember(binder.Name);
        return result is not null;
    }

    /// <summary>
    /// Discovers public <c>StreamActions.Twitch.Api.*</c> types that expose static <see cref="TwitchSession"/> methods.
    /// </summary>
    private static NamespaceNode BuildNamespaceTree()
    {
        NamespaceNode root = new();
        Assembly twitchAssembly = typeof(TwitchSession).Assembly;

        foreach (Type type in twitchAssembly.GetExportedTypes())
        {
            if (string.IsNullOrWhiteSpace(type.Namespace)
                || !type.Namespace.StartsWith(_apiNamespacePrefix, StringComparison.Ordinal)
                || string.Equals(type.Namespace, "StreamActions.Twitch.Api.Common", StringComparison.Ordinal))
            {
                continue;
            }

            bool hasSessionMethod = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(static m =>
                {
                    ParameterInfo[] parameters = m.GetParameters();
                    return parameters.Length > 0 && parameters[0].ParameterType == typeof(TwitchSession);
                });

            if (!hasSessionMethod)
            {
                continue;
            }

            string relativeNamespace = type.Namespace[_apiNamespacePrefix.Length..];
            string[] segments = relativeNamespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
            NamespaceNode cursor = root;
            foreach (string segment in segments)
            {
                if (!cursor.Children.TryGetValue(segment, out NamespaceNode? next))
                {
                    next = new NamespaceNode();
                    cursor.Children[segment] = next;
                }

                cursor = next;
            }

            cursor.ApiTypes[type.Name] = type;
        }

        return root;
    }

    /// <summary>
    /// Internal namespace node mapping sub-namespace segments and API record types.
    /// </summary>
    private sealed class NamespaceNode
    {
        /// <summary>Gets child sub-namespaces.</summary>
        public Dictionary<string, NamespaceNode> Children { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets static API types under this namespace.</summary>
        public Dictionary<string, Type> ApiTypes { get; } = new(StringComparer.Ordinal);
    }
}


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

namespace StreamActions.Core.Commands;

/// <summary>
/// Built-in core plugin (<c>core.commands</c>) providing custom command management via <c>!bot command</c>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CommandsPlugin"/> class.
/// </remarks>
/// <param name="commandHandler">Optional <see cref="BotCommandSubCommandHandler"/> resolved from DI.</param>
public sealed class CommandsPlugin(BotCommandSubCommandHandler? commandHandler = null) : IPlugin
{
    /// <summary>
    /// Optional explicit command management sub-command handler instance.
    /// </summary>
    private readonly BotCommandSubCommandHandler? _commandHandler = commandHandler;

    /// <inheritdoc/>
    public string Id => "core.commands";

    /// <inheritdoc/>
    public string Name => "Commands";

    /// <inheritdoc/>
    public Version Version { get; } = new(1, 0, 0);

    /// <inheritdoc/>
    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Services.GetService(typeof(IBuiltInCommandRegistry)) is IBuiltInCommandRegistry registry)
        {
            BotCommandSubCommandHandler? handler = this._commandHandler
                ?? context.Services.GetService(typeof(BotCommandSubCommandHandler)) as BotCommandSubCommandHandler;

            if (handler is not null && registry.ResolveByCommandId(handler.CommandId) is null)
            {
                registry.Register(handler);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}

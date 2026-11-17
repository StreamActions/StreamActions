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
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Background service that periodically invokes <see cref="ITokenLifecycleService.RunMaintenancePassAsync"/>
/// every minute to proactively refresh expiring tokens and validate older active tokens.
/// </summary>
internal sealed partial class TokenLifecycleHostedService : BackgroundService
{
    /// <summary>
    /// Polling interval for token lifecycle maintenance passes.
    /// </summary>
    public static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The token lifecycle service.
    /// </summary>
    private readonly ITokenLifecycleService _tokenLifecycleService;

    /// <summary>
    /// The time provider used to construct the <see cref="PeriodicTimer"/>.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger instance.
    /// </summary>
    private readonly ILogger<TokenLifecycleHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenLifecycleHostedService"/> class.
    /// </summary>
    /// <param name="tokenLifecycleService">The token lifecycle service.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger instance.</param>
    public TokenLifecycleHostedService(
        ITokenLifecycleService tokenLifecycleService,
        TimeProvider timeProvider,
        ILogger<TokenLifecycleHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(tokenLifecycleService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._tokenLifecycleService = tokenLifecycleService;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background maintenance timer must survive transient errors and keep running.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(MaintenanceInterval, this._timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await this._tokenLifecycleService
                    .RunMaintenancePassAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMaintenancePassFailed(this._logger, ex);
            }
        }
    }

    /// <summary>
    /// Logs an unexpected failure during a scheduled token maintenance pass.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception thrown by <see cref="ITokenLifecycleService.RunMaintenancePassAsync"/>.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled token lifecycle maintenance pass failed.")]
    private static partial void LogMaintenancePassFailed(ILogger logger, Exception exception);
}

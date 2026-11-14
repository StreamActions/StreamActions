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

using Microsoft.EntityFrameworkCore;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;

namespace StreamActions.Database.Services;

/// <summary>
/// Manages database-persisted <see cref="InstanceSettings"/> with in-memory caching and hot-reload change notifications.
/// </summary>
public sealed class InstanceSettingsService : IInstanceSettingsService, IDisposable
{
    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// The time provider used to stamp settings updates.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Synchronization gate serializing cache initialization and settings updates.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The cached <see cref="InstanceSettings"/> instance, or <see langword="null"/> if not yet loaded.
    /// </summary>
    private InstanceSettings? _cachedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceSettingsService"/> class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider.</param>
    public InstanceSettingsService(
        IDbContextFactory<StreamActionsDbContext> contextFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._contextFactory = contextFactory;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged;

    /// <inheritdoc/>
    public async ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        InstanceSettings? cached = Volatile.Read(ref this._cachedSettings);
        if (cached is not null)
        {
            return cached;
        }

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = Volatile.Read(ref this._cachedSettings);
            if (cached is not null)
            {
                return cached;
            }

            StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                InstanceSettings? existing = await dbContext.InstanceSettings
                    .OrderBy(static x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    existing = new InstanceSettings
                    {
                        Id = Guid.CreateVersion7(),
                        AnonymousInstallId = Guid.NewGuid(),
                        BackupIntervalHours = 24,
                        MaxBackupFiles = 7,
                        InstanceOAuthAppPolicy = "instance_default",
                        InstanceBotAccountPolicy = "instance_default",
                        UpdatedAtUtc = this._timeProvider.GetUtcNow(),
                    };

                    _ = dbContext.InstanceSettings.Add(existing);
                    _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                this._cachedSettings = existing;
                return existing;
            }
        }
        finally
        {
            _ = this._gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            settings.UpdatedAtUtc = this._timeProvider.GetUtcNow();

            StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                bool exists = await dbContext.InstanceSettings
                    .AnyAsync(x => x.Id == settings.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    _ = dbContext.InstanceSettings.Update(settings);
                }
                else
                {
                    if (settings.Id == Guid.Empty)
                    {
                        settings.Id = Guid.CreateVersion7();
                    }

                    _ = dbContext.InstanceSettings.Add(settings);
                }

                _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                this._cachedSettings = settings;
            }
        }
        finally
        {
            _ = this._gate.Release();
        }

        this.SettingsChanged?.Invoke(this, new InstanceSettingsChangedEventArgs(settings));
    }

    /// <inheritdoc/>
    public void Dispose() => this._gate.Dispose();
}

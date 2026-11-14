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
using System.Collections.Concurrent;
using System.Globalization;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IWorkspaceKeyValueStore"/> with invariant-culture parsing and atomic increments.
/// </summary>
public sealed class WorkspaceKeyValueStore : IWorkspaceKeyValueStore
{
    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// Per-key semaphores serializing atomic read-modify-write increments.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceKeyValueStore"/> class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    public WorkspaceKeyValueStore(IDbContextFactory<StreamActionsDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        this._contextFactory = contextFactory;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetStringAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        string? defaultValue = null,
        CancellationToken ct = default)
    {
        (string normalizedTable, string normalizedSection, string normalizedVariable) = NormalizeKeys(table, section, variable);

        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            string? value = await dbContext.WorkspaceKeyValueEntries
                .Where(x => (x.WorkspaceId == workspaceId)
                    && (x.TableName == normalizedTable)
                    && (x.Section == normalizedSection)
                    && (x.Variable == normalizedVariable))
                .Select(static x => x.Value)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            return value ?? defaultValue;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> GetBoolAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        bool defaultValue = false,
        CancellationToken ct = default)
    {
        string? raw = await this.GetStringAsync(workspaceId, table, section, variable, null, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        string trimmed = raw.Trim();
        return string.Equals(trimmed, "1", StringComparison.Ordinal)
            || (!string.Equals(trimmed, "0", StringComparison.Ordinal) && (bool.TryParse(trimmed, out bool parsed) ? parsed : defaultValue));
    }

    /// <inheritdoc/>
    public async ValueTask<int> GetIntAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        int defaultValue = 0,
        CancellationToken ct = default)
    {
        string? raw = await this.GetStringAsync(workspaceId, table, section, variable, null, ct).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    /// <inheritdoc/>
    public async ValueTask<long> GetLongAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        long defaultValue = 0L,
        CancellationToken ct = default)
    {
        string? raw = await this.GetStringAsync(workspaceId, table, section, variable, null, ct).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(raw)
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : defaultValue;
    }

    /// <inheritdoc/>
    public async ValueTask<float> GetFloatAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        float defaultValue = 0f,
        CancellationToken ct = default)
    {
        string? raw = await this.GetStringAsync(workspaceId, table, section, variable, null, ct).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(raw)
            && float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : defaultValue;
    }

    /// <inheritdoc/>
    public async ValueTask<double> GetDoubleAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        double defaultValue = 0d,
        CancellationToken ct = default)
    {
        string? raw = await this.GetStringAsync(workspaceId, table, section, variable, null, ct).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : defaultValue;
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync<T>(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        T value,
        CancellationToken ct = default)
    {
        (string normalizedTable, string normalizedSection, string normalizedVariable) = NormalizeKeys(table, section, variable);
        string stringValue = FormatInvariant(value);

        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            int updated = await dbContext.WorkspaceKeyValueEntries
                .Where(x => (x.WorkspaceId == workspaceId)
                    && (x.TableName == normalizedTable)
                    && (x.Section == normalizedSection)
                    && (x.Variable == normalizedVariable))
                .ExecuteUpdateAsync(setters => setters.SetProperty(static x => x.Value, stringValue), ct)
                .ConfigureAwait(false);

            if (updated == 0)
            {
                WorkspaceKeyValueEntry entry = new()
                {
                    WorkspaceId = workspaceId,
                    TableName = normalizedTable,
                    Section = normalizedSection,
                    Variable = normalizedVariable,
                    Value = stringValue,
                };

                _ = dbContext.WorkspaceKeyValueEntries.Add(entry);
                _ = await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<long> IncrementLongAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        long delta = 1L,
        CancellationToken ct = default)
    {
        (string normalizedTable, string normalizedSection, string normalizedVariable) = NormalizeKeys(table, section, variable);
        string lockKey = $"{workspaceId:N}:{normalizedTable}:{normalizedSection}:{normalizedVariable}";
        SemaphoreSlim gate = this._keyLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                WorkspaceKeyValueEntry? existing = await dbContext.WorkspaceKeyValueEntries
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => (x.WorkspaceId == workspaceId)
                            && (x.TableName == normalizedTable)
                            && (x.Section == normalizedSection)
                            && (x.Variable == normalizedVariable),
                        ct)
                    .ConfigureAwait(false);

                long current = 0L;
                if ((existing is not null)
                    && long.TryParse(existing.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
                {
                    current = parsed;
                }

                long next = current + delta;
                string nextValue = next.ToString(CultureInfo.InvariantCulture);

                if (existing is null)
                {
                    _ = dbContext.WorkspaceKeyValueEntries.Add(new WorkspaceKeyValueEntry
                    {
                        WorkspaceId = workspaceId,
                        TableName = normalizedTable,
                        Section = normalizedSection,
                        Variable = normalizedVariable,
                        Value = nextValue,
                    });
                }
                else
                {
                    existing.Value = nextValue;
                }

                _ = await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                return next;
            }
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(
        Guid workspaceId,
        string table,
        string section,
        string variable,
        CancellationToken ct = default)
    {
        (string normalizedTable, string normalizedSection, string normalizedVariable) = NormalizeKeys(table, section, variable);

        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            int deleted = await dbContext.WorkspaceKeyValueEntries
                .Where(x => (x.WorkspaceId == workspaceId)
                    && (x.TableName == normalizedTable)
                    && (x.Section == normalizedSection)
                    && (x.Variable == normalizedVariable))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>> GetKeysAsync(
        Guid workspaceId,
        string table,
        string section,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        string normalizedTable = table.ToLowerInvariant();
        string normalizedSection = section.ToLowerInvariant();

        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            List<string> keys = await dbContext.WorkspaceKeyValueEntries
                .Where(x => (x.WorkspaceId == workspaceId)
                    && (x.TableName == normalizedTable)
                    && (x.Section == normalizedSection))
                .OrderBy(static x => x.Variable)
                .Select(static x => x.Variable)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return keys;
        }
    }

    /// <summary>
    /// Validates and normalizes the composite key segments to lowercase invariant strings.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The logical section name.</param>
    /// <param name="variable">The variable name.</param>
    /// <returns>A tuple containing the normalized table, section, and variable names.</returns>
    private static (string Table, string Section, string Variable) NormalizeKeys(string table, string section, string variable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);

        return (table.ToLowerInvariant(), section.ToLowerInvariant(), variable.ToLowerInvariant());
    }

    /// <summary>
    /// Formats a value using <see cref="CultureInfo.InvariantCulture"/> when <typeparamref name="T"/> implements <see cref="IFormattable"/>.
    /// </summary>
    /// <typeparam name="T">The value type to format.</typeparam>
    /// <param name="value">The value to format.</param>
    /// <returns>The invariant string representation of <paramref name="value"/>.</returns>
    private static string FormatInvariant<T>(T value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}

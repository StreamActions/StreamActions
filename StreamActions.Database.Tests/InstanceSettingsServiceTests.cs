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

using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class InstanceSettingsServiceTests
{
    #region UpdateAsync
    [Fact]
    [Trait("Member", "UpdateAsync")]
    public async Task GetAsync_And_UpdateAsync_CachesAndRaisesSettingsChanged()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        using InstanceSettingsService service = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        InstanceSettings? eventSettings = null;
        service.SettingsChanged += (_, args) => eventSettings = args.Settings;

        InstanceSettings initial = await service.GetAsync(ct);
        _ = initial.BackupIntervalHours.Should().Be(24);
        _ = initial.MaxBackupFiles.Should().Be(7);

        initial.MaxBackupFiles = 14;
        initial.SentryEnabled = true;
        await service.UpdateAsync(initial, ct);

        _ = eventSettings.Should().NotBeNull();
        _ = eventSettings!.MaxBackupFiles.Should().Be(14);
        _ = eventSettings.SentryEnabled.Should().BeTrue();

        InstanceSettings cached = await service.GetAsync(ct);
        _ = cached.MaxBackupFiles.Should().Be(14);
    }
    #endregion
}

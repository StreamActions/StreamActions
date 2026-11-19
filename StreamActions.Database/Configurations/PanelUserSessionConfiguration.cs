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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StreamActions.Core.Models.Entities;

namespace StreamActions.Database.Configurations;

/// <summary>
/// Entity Framework Core configuration for <see cref="PanelUserSession"/>.
/// </summary>
internal sealed class PanelUserSessionConfiguration : IEntityTypeConfiguration<PanelUserSession>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PanelUserSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.HasKey(static x => x.Id);
        _ = builder.Property(static x => x.ProviderId).HasMaxLength(128).IsRequired();
        _ = builder.Property(static x => x.RemoteIpAddress).HasMaxLength(128);
        _ = builder.Property(static x => x.UserAgent).HasMaxLength(512);

        _ = builder.HasOne<PanelUser>()
            .WithMany()
            .HasForeignKey(static x => x.PanelUserId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(static x => new { x.PanelUserId, x.RevokedAtUtc });
    }
}


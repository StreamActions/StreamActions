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
/// Entity Framework Core configuration for <see cref="ExternalOidcProvider"/>.
/// </summary>
internal sealed class ExternalOidcProviderConfiguration : IEntityTypeConfiguration<ExternalOidcProvider>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalOidcProvider> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.HasKey(static x => x.Id);
        _ = builder.Property(static x => x.Slug).HasMaxLength(128).IsRequired();
        _ = builder.Property(static x => x.DisplayName).HasMaxLength(200).IsRequired();
        _ = builder.Property(static x => x.AuthorityUrl).HasMaxLength(512).IsRequired();
        _ = builder.Property(static x => x.ClientId).HasMaxLength(256).IsRequired();
        _ = builder.Property(static x => x.EncryptedClientSecret).IsRequired();
        _ = builder.Property(static x => x.ScopesJson).IsRequired();
        _ = builder.Property(static x => x.GroupClaimType).HasMaxLength(128);

        _ = builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(static x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(static x => new { x.WorkspaceId, x.Slug }).IsUnique();
    }
}

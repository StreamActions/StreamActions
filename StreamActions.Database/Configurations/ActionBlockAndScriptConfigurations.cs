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
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StreamActions.Core.Actions;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Scripting;
using System.Text.Json;

namespace StreamActions.Database.Configurations;

/// <summary>
/// Shared EF Core JSON value converters and comparers for Action Block and ClearScript entities.
/// </summary>
internal static class ActionBlockJsonValueConverters
{
    /// <summary>
    /// Value converter for <see cref="List{T}"/> of <see cref="TaskParameterDefinition"/>.
    /// </summary>
    public static ValueConverter<List<TaskParameterDefinition>, string> ParameterListConverter { get; } =
        new(
            static v => JsonSerializer.Serialize(v, ActionBlockJson.Default),
            static v => string.IsNullOrWhiteSpace(v)
                ? new List<TaskParameterDefinition>()
                : JsonSerializer.Deserialize<List<TaskParameterDefinition>>(v, ActionBlockJson.Default) ?? new List<TaskParameterDefinition>());

    /// <summary>
    /// Value comparer for <see cref="List{T}"/> of <see cref="TaskParameterDefinition"/>.
    /// </summary>
    public static ValueComparer<List<TaskParameterDefinition>> ParameterListComparer { get; } =
        CreateJsonComparer<List<TaskParameterDefinition>>(ActionBlockJson.Default);

    /// <summary>
    /// Value converter for polymorphic <see cref="List{T}"/> of <see cref="ActionBlock"/>.
    /// </summary>
    public static ValueConverter<List<ActionBlock>, string> ActionBlockListConverter { get; } =
        new(
            static v => JsonSerializer.Serialize(v, ActionBlockJson.Default),
            static v => string.IsNullOrWhiteSpace(v)
                ? new List<ActionBlock>()
                : JsonSerializer.Deserialize<List<ActionBlock>>(v, ActionBlockJson.Default) ?? new List<ActionBlock>());

    /// <summary>
    /// Value comparer for polymorphic <see cref="List{T}"/> of <see cref="ActionBlock"/>.
    /// </summary>
    public static ValueComparer<List<ActionBlock>> ActionBlockListComparer { get; } =
        CreateJsonComparer<List<ActionBlock>>(ActionBlockJson.Default);

    /// <summary>
    /// Value converter for <see cref="ProfileTriggerDefinition"/>.
    /// </summary>
    public static ValueConverter<ProfileTriggerDefinition, string> TriggerConverter { get; } =
        new(
            static v => JsonSerializer.Serialize(v, ActionBlockJson.Default),
            static v => string.IsNullOrWhiteSpace(v)
                ? new ProfileTriggerDefinition()
                : JsonSerializer.Deserialize<ProfileTriggerDefinition>(v, ActionBlockJson.Default) ?? new ProfileTriggerDefinition());

    /// <summary>
    /// Value comparer for <see cref="ProfileTriggerDefinition"/>.
    /// </summary>
    public static ValueComparer<ProfileTriggerDefinition> TriggerComparer { get; } =
        CreateJsonComparer<ProfileTriggerDefinition>(ActionBlockJson.Default);

    /// <summary>
    /// Value converter for nullable <see cref="ActionBlockConditionGroup"/>.
    /// </summary>
    public static ValueConverter<ActionBlockConditionGroup?, string?> ConditionGroupConverter { get; } =
        new(
            static v => v == null ? null : JsonSerializer.Serialize(v, ActionBlockJson.Default),
            static v => string.IsNullOrWhiteSpace(v)
                ? null
                : JsonSerializer.Deserialize<ActionBlockConditionGroup>(v, ActionBlockJson.Default));

    /// <summary>
    /// Value comparer for nullable <see cref="ActionBlockConditionGroup"/>.
    /// </summary>
    public static ValueComparer<ActionBlockConditionGroup?> ConditionGroupComparer { get; } =
        new(
            static (a, b) => string.Equals(
                a == null ? null : JsonSerializer.Serialize(a, ActionBlockJson.Default),
                b == null ? null : JsonSerializer.Serialize(b, ActionBlockJson.Default),
                StringComparison.Ordinal),
            static v => v == null ? 0 : JsonSerializer.Serialize(v, ActionBlockJson.Default).GetHashCode(StringComparison.Ordinal),
            static v => v == null ? null : JsonSerializer.Deserialize<ActionBlockConditionGroup>(JsonSerializer.Serialize(v, ActionBlockJson.Default), ActionBlockJson.Default));

    /// <summary>
    /// Value converter for <see cref="List{T}"/> of <see cref="ScriptCommandRegistration"/>.
    /// </summary>
    public static ValueConverter<List<ScriptCommandRegistration>, string> ScriptCommandListConverter { get; } =
        new(
            static v => JsonSerializer.Serialize(v, ActionBlockJson.Default),
            static v => string.IsNullOrWhiteSpace(v)
                ? new List<ScriptCommandRegistration>()
                : JsonSerializer.Deserialize<List<ScriptCommandRegistration>>(v, ActionBlockJson.Default) ?? new List<ScriptCommandRegistration>());

    /// <summary>
    /// Value comparer for <see cref="List{T}"/> of <see cref="ScriptCommandRegistration"/>.
    /// </summary>
    public static ValueComparer<List<ScriptCommandRegistration>> ScriptCommandListComparer { get; } =
        CreateJsonComparer<List<ScriptCommandRegistration>>(ActionBlockJson.Default);

    /// <summary>
    /// Creates an EF Core <see cref="ValueComparer{T}"/> backed by deterministic JSON round-tripping.
    /// </summary>
    private static ValueComparer<T> CreateJsonComparer<T>(JsonSerializerOptions options)
        where T : class, new() =>
        new(
            (a, b) => string.Equals(
                JsonSerializer.Serialize(a, options),
                JsonSerializer.Serialize(b, options),
                StringComparison.Ordinal),
            v => JsonSerializer.Serialize(v, options).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, options), options) ?? new T());
}

/// <summary>
/// Entity Framework Core configuration for <see cref="WorkspaceActionBlockTask"/>.
/// </summary>
internal sealed class WorkspaceActionBlockTaskConfiguration : IEntityTypeConfiguration<WorkspaceActionBlockTask>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkspaceActionBlockTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.HasKey(static x => x.Id);
        _ = builder.Property(static x => x.Name).HasMaxLength(200).IsRequired();
        _ = builder.Property(static x => x.InputParameters)
            .HasConversion(ActionBlockJsonValueConverters.ParameterListConverter, ActionBlockJsonValueConverters.ParameterListComparer)
            .IsRequired();
        _ = builder.Property(static x => x.Blocks)
            .HasConversion(ActionBlockJsonValueConverters.ActionBlockListConverter, ActionBlockJsonValueConverters.ActionBlockListComparer)
            .IsRequired();

        _ = builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(static x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(static x => new { x.WorkspaceId, x.Name }).IsUnique();
    }
}

/// <summary>
/// Entity Framework Core configuration for <see cref="WorkspaceActionBlockProfile"/>.
/// </summary>
internal sealed class WorkspaceActionBlockProfileConfiguration : IEntityTypeConfiguration<WorkspaceActionBlockProfile>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkspaceActionBlockProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.HasKey(static x => x.Id);
        _ = builder.Property(static x => x.Name).HasMaxLength(200).IsRequired();
        _ = builder.Property(static x => x.Trigger)
            .HasConversion(ActionBlockJsonValueConverters.TriggerConverter, ActionBlockJsonValueConverters.TriggerComparer)
            .IsRequired();
        _ = builder.Property(static x => x.Predicates)
            .HasConversion(ActionBlockJsonValueConverters.ConditionGroupConverter, ActionBlockJsonValueConverters.ConditionGroupComparer);
        _ = builder.Property(static x => x.ParameterMappings)
            .HasConversion(JsonValueConverters.StringDictionaryConverter, JsonValueConverters.StringDictionaryComparer)
            .IsRequired();

        _ = builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(static x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(static x => new { x.WorkspaceId, x.Name }).IsUnique();
        _ = builder.HasIndex(static x => new { x.WorkspaceId, x.AttachedTaskId });
    }
}

/// <summary>
/// Entity Framework Core configuration for <see cref="WorkspaceScript"/>.
/// </summary>
internal sealed class WorkspaceScriptConfiguration : IEntityTypeConfiguration<WorkspaceScript>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkspaceScript> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.HasKey(static x => x.Id);
        _ = builder.Property(static x => x.Path).HasMaxLength(512).IsRequired();
        _ = builder.Property(static x => x.Code).IsRequired();
        _ = builder.Property(static x => x.RegisteredCommands)
            .HasConversion(ActionBlockJsonValueConverters.ScriptCommandListConverter, ActionBlockJsonValueConverters.ScriptCommandListComparer)
            .IsRequired();

        _ = builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(static x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(static x => new { x.WorkspaceId, x.Path }).IsUnique();
    }
}


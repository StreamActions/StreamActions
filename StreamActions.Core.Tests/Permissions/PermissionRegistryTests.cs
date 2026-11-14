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
using StreamActions.Core.Permissions;
using Xunit;

namespace StreamActions.Core.Tests.Permissions;

public sealed class PermissionRegistryTests
{
    #region BuiltInSeeding

    [Fact]
    [Trait("Member", "GetAll")]
    public void ConstructorSeedsAllBuiltInCategoriesAndFeatureFlags()
    {
        PermissionRegistry registry = new();

        IReadOnlyList<string> categories = registry.GetCategories();
        IReadOnlyList<PermissionDescriptor> all = registry.GetAll();
        IReadOnlyList<PermissionDescriptor> features = registry.GetByCategory(PermissionCategories.Features);

        _ = categories.Should().Contain([
            PermissionCategories.Features,
            PermissionCategories.Ui,
            PermissionCategories.Moderation,
            PermissionCategories.Actions,
        ]);
        _ = all.Should().NotBeEmpty();
        _ = features.Should().NotBeEmpty();
        _ = features.Should().OnlyContain(d => d.IsGloballyGated && d.Key.StartsWith("feature.", StringComparison.Ordinal));
        _ = registry.GetByCategory(PermissionCategories.Ui).Should().OnlyContain(d => !d.IsGloballyGated);
        _ = registry.GetByCategory(PermissionCategories.Moderation).Should().OnlyContain(d => !d.IsGloballyGated);
        _ = registry.GetByCategory(PermissionCategories.Actions).Should().OnlyContain(d => !d.IsGloballyGated);
    }

    [Theory]
    [InlineData(BuiltInPermissionKeys.FeatureCustomCommands, true)]
    [InlineData(BuiltInPermissionKeys.UiDashboardView, false)]
    [InlineData(BuiltInPermissionKeys.ModerationTimeout, false)]
    [InlineData(BuiltInPermissionKeys.ActionRunCommand, false)]
    [Trait("Member", "GetByKey")]
    public void GetByKeyForBuiltInKeyReturnsExpectedDescriptor(string key, bool expectedGloballyGated)
    {
        PermissionRegistry registry = new();

        PermissionDescriptor? descriptor = registry.GetByKey(key);

        _ = descriptor.Should().NotBeNull();
        _ = descriptor!.Key.Should().Be(key);
        _ = descriptor.IsGloballyGated.Should().Be(expectedGloballyGated);
    }

    #endregion

    #region Register

    [Fact]
    [Trait("Member", "Register")]
    public void RegisterWhenDescriptorIsValidAddsAndRetrievesCaseInsensitively()
    {
        PermissionRegistry registry = new();
        PermissionDescriptor custom = new(
            "plugin.obs.switch_scene",
            "OBS Plugin",
            "Switch OBS Scene",
            "Allows switching OBS scenes.",
            IsGloballyGated: true);

        registry.Register(custom);

        PermissionDescriptor? retrieved = registry.GetByKey("PLUGIN.OBS.SWITCH_SCENE");
        _ = retrieved.Should().Be(custom);
        _ = registry.GetCategories().Should().Contain("OBS Plugin");
        _ = registry.GetByCategory("OBS Plugin").Should().ContainSingle().Which.Should().Be(custom);
    }

    [Fact]
    [Trait("Member", "Register")]
    public void RegisterWhenDescriptorIsNullThrowsArgumentNullException()
    {
        PermissionRegistry registry = new();

        Action action = () => registry.Register(null!);

        _ = action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Unregister

    [Fact]
    [Trait("Member", "Unregister")]
    public void UnregisterWhenKeyExistsRemovesDescriptorAndReturnsTrue()
    {
        PermissionRegistry registry = new();
        PermissionDescriptor custom = new("plugin.test.action", "Plugins", "Test", "Test action.");
        registry.Register(custom);

        bool removed = registry.Unregister("plugin.test.action");

        _ = removed.Should().BeTrue();
        _ = registry.GetByKey("plugin.test.action").Should().BeNull();
    }

    [Fact]
    [Trait("Member", "Unregister")]
    public void UnregisterWhenKeyDoesNotExistReturnsFalse()
    {
        PermissionRegistry registry = new();

        bool removed = registry.Unregister("nonexistent.permission");

        _ = removed.Should().BeFalse();
    }

    #endregion
}

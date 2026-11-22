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
using StreamActions.Core.Models;
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
    [InlineData(BuiltInPermissionKeys.FeatureCustomCommands, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureTimers, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureQuotes, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureCounters, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureModeration, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureLoyalty, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureOverlays, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureSoundboard, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureAlerts, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeaturePublicAnonymousAccess, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeaturePublicViewerLogin, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureAddonDiscord, true, false, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureScriptingClearScript, true, true, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeaturePlugins, true, true, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureCustomOAuthApp, true, true, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureCustomBotAccount, true, true, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.FeatureCustomOidcProvider, true, true, PlatformUserDesignations.None)]
    [InlineData(BuiltInPermissionKeys.UiPanelAccess, false, false, PlatformUserDesignations.Broadcaster | PlatformUserDesignations.Moderator)]
    [InlineData(BuiltInPermissionKeys.UiDashboardView, false, false, PlatformUserDesignations.Broadcaster | PlatformUserDesignations.Moderator)]
    [InlineData(BuiltInPermissionKeys.UiSecretsView, false, false, PlatformUserDesignations.Broadcaster)]
    [InlineData(BuiltInPermissionKeys.UiSecretsManage, false, false, PlatformUserDesignations.Broadcaster)]
    [InlineData(BuiltInPermissionKeys.UiCommandsScriptEdit, false, false, PlatformUserDesignations.Broadcaster)]
    [InlineData(BuiltInPermissionKeys.UiPermissionsManage, false, false, PlatformUserDesignations.Broadcaster)]
    [InlineData(BuiltInPermissionKeys.UiSettingsManage, false, false, PlatformUserDesignations.Broadcaster)]
    [InlineData(BuiltInPermissionKeys.UiLoyaltyManage, false, false, PlatformUserDesignations.Broadcaster | PlatformUserDesignations.Moderator)]
    [InlineData(BuiltInPermissionKeys.UiCommandsActionsDiscordRoles, false, false, PlatformUserDesignations.Broadcaster)]
    [InlineData(BuiltInPermissionKeys.ModerationTimeout, false, false, PlatformUserDesignations.Broadcaster | PlatformUserDesignations.Moderator)]
    [InlineData(BuiltInPermissionKeys.ActionRunCommand, false, false, PlatformUserDesignations.Everyone)]
    [Trait("Member", "GetByKey")]
    public void GetByKeyForBuiltInKeyReturnsExpectedDescriptor(
        string key,
        bool expectedGloballyGated,
        bool expectedDangerous,
        PlatformUserDesignations expectedDefaultDesignations)
    {
        PermissionRegistry registry = new();

        PermissionDescriptor? descriptor = registry.GetByKey(key);

        _ = descriptor.Should().NotBeNull();
        _ = descriptor!.Key.Should().Be(key);
        _ = descriptor.IsGloballyGated.Should().Be(expectedGloballyGated);
        _ = descriptor.IsDangerousGlobalFeature.Should().Be(expectedDangerous);
        _ = descriptor.DefaultAllowedDesignations.Should().Be(expectedDefaultDesignations);
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

    [Fact]
    [Trait("Member", "Register")]
    public void RegisterWhenDefaultStateIsDenyThrowsArgumentException()
    {
        PermissionRegistry registry = new();
        PermissionDescriptor invalid = new(
            "plugin.bad.deny_default",
            "Bad Plugin",
            "Invalid Deny Default",
            "Plugin default must never be Deny.",
            DefaultState: PermissionState.Deny);

        Action action = () => registry.Register(invalid);

        _ = action.Should().Throw<ArgumentException>()
            .WithParameterName("descriptor");
    }

    [Theory]
    [InlineData(false, PermissionState.Inherit)]
    [InlineData(true, PermissionState.Allow)]
    [Trait("Member", "Register")]
    public void RegisterWhenDangerousGlobalFeatureIsNotGloballyGatedOrDefaultsToAllowThrowsArgumentException(
        bool isGloballyGated,
        PermissionState defaultState)
    {
        PermissionRegistry registry = new();
        PermissionDescriptor invalid = new(
            "feature.plugin.dangerous",
            PermissionCategories.Features,
            "Dangerous Feature",
            "High-risk feature.",
            IsGloballyGated: isGloballyGated,
            IsDangerousGlobalFeature: true,
            DefaultState: defaultState);

        Action action = () => registry.Register(invalid);

        _ = action.Should().Throw<ArgumentException>()
            .WithParameterName("descriptor");
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

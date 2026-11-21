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
using StreamActions.Core.Commands;
using StreamActions.Core.Permissions;
using System.Reflection;
using Xunit;

namespace StreamActions.Core.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="CommandAttribute"/>.
/// </summary>
public sealed class CommandAttributeTests
{
    #region ConstructorAndProperties

    [Fact]
    [Trait("Member", "CommandAttribute")]
    public void ConstructorNormalizesTriggerAndAppliesDefaults()
    {
        CommandAttribute attribute = new("  ShoutOut  ");

        _ = attribute.Trigger.Should().Be("shoutout");
        _ = attribute.Aliases.Should().BeEmpty();
        _ = attribute.Description.Should().BeNull();
        _ = attribute.DefaultPermissionKey.Should().BeNull();
        _ = attribute.GlobalCooldownSeconds.Should().Be(0);
        _ = attribute.UserCooldownSeconds.Should().Be(0);
        _ = attribute.AllowCustomOverride.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Member", "CommandAttribute")]
    public void ConstructorWhenTriggerIsBlankThrowsArgumentException(string trigger)
    {
        Action action = () => _ = new CommandAttribute(trigger);

        _ = action.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Member", "CommandAttribute")]
    public void AttributeReflectsAllConfiguredProperties()
    {
        SampleBuiltInCommand instance = new();
        CommandAttribute? attribute = instance.GetType().GetCustomAttribute<CommandAttribute>();
        CommandAttribute? methodAttribute = typeof(SampleBuiltInCommand)
            .GetMethod(nameof(SampleBuiltInCommand.Execute))!
            .GetCustomAttribute<CommandAttribute>();

        _ = attribute.Should().NotBeNull();
        _ = attribute!.Trigger.Should().Be("so");
        _ = attribute.Aliases.Should().Equal("shoutout", "promo");
        _ = attribute.Description.Should().Be("Shouts out a fellow creator.");
        _ = attribute.DefaultPermissionKey.Should().Be(BuiltInPermissionKeys.ActionRunCommand);
        _ = attribute.GlobalCooldownSeconds.Should().Be(10);
        _ = attribute.UserCooldownSeconds.Should().Be(30);
        _ = attribute.AllowCustomOverride.Should().BeTrue();

        _ = methodAttribute.Should().NotBeNull();
        _ = methodAttribute!.Trigger.Should().Be("ping");
        _ = methodAttribute.AllowCustomOverride.Should().BeFalse();
    }

    #endregion

    /// <summary>
    /// Sample class decorated with <see cref="CommandAttribute"/> for reflection verification.
    /// </summary>
    [Command(
        "SO",
        Aliases = ["shoutout", "promo"],
        Description = "Shouts out a fellow creator.",
        DefaultPermissionKey = BuiltInPermissionKeys.ActionRunCommand,
        GlobalCooldownSeconds = 10,
        UserCooldownSeconds = 30,
        AllowCustomOverride = true)]
    private sealed class SampleBuiltInCommand
    {
        /// <summary>
        /// Sample command handler method decorated with <see cref="CommandAttribute"/>.
        /// </summary>
        [Command("PING")]
        public static void Execute()
        {
        }
    }
}

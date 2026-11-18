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
using StreamActions.Core.Models.Entities;
using Xunit;

namespace StreamActions.Core.Tests.Models;

public sealed class AuthPolicyParserTests
{
    #region ParseOAuthAppPolicy

    [Theory]
    [Trait("Member", "ParseOAuthAppPolicy")]
    [InlineData(null, InstanceOAuthAppPolicy.InstanceOnly)]
    [InlineData("", InstanceOAuthAppPolicy.InstanceOnly)]
    [InlineData("instance_default", InstanceOAuthAppPolicy.InstanceOnly)]
    [InlineData("InstanceOnly", InstanceOAuthAppPolicy.InstanceOnly)]
    [InlineData("instance_with_optional_override", InstanceOAuthAppPolicy.InstanceWithOptionalOverride)]
    [InlineData("InstanceWithOptionalOverride", InstanceOAuthAppPolicy.InstanceWithOptionalOverride)]
    [InlineData("instance_login_with_streamer_api_oauth", InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth)]
    [InlineData("INSTANCE_LOGIN_WITH_STREAMER_API_OAUTH", InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth)]
    [InlineData("InstanceLoginWithStreamerApiOAuth", InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth)]
    [InlineData("2", InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth)]
    [InlineData("require_streamer_oauth_app", InstanceOAuthAppPolicy.RequireStreamerOAuthApp)]
    [InlineData("3", InstanceOAuthAppPolicy.RequireStreamerOAuthApp)]
    public void ParseOAuthAppPolicyWithValidValuesReturnsExpectedEnum(string? input, InstanceOAuthAppPolicy expected)
    {
        InstanceOAuthAppPolicy result = AuthPolicyParser.ParseOAuthAppPolicy(input);
        _ = result.Should().Be(expected);
    }

    [Fact]
    [Trait("Member", "ParseOAuthAppPolicy")]
    public void ParseOAuthAppPolicyWithUnknownValueThrowsArgumentException()
    {
        Action act = () => _ = AuthPolicyParser.ParseOAuthAppPolicy("invalid_policy");
        _ = act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region PerPlatformOAuthAppPolicy

    [Fact]
    [Trait("Member", "ResolveOAuthAppPolicy")]
    public void SetAndResolveOAuthAppPolicySupportsPerPlatformOverridesAndFallbackToDefault()
    {
        InstanceSettings settings = new()
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceOnly),
        };

        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settings, "twitch").Should().Be(InstanceOAuthAppPolicy.InstanceOnly);

        AuthPolicyParser.SetPlatformOAuthAppPolicy(settings, "Twitch", InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);
        AuthPolicyParser.SetPlatformOAuthAppPolicy(settings, "kick", InstanceOAuthAppPolicy.RequireStreamerOAuthApp);

        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settings, "twitch").Should().Be(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);
        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settings, "KICK").Should().Be(InstanceOAuthAppPolicy.RequireStreamerOAuthApp);
        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settings, "youtube").Should().Be(InstanceOAuthAppPolicy.InstanceOnly);
        _ = AuthPolicyParser.TryGetPlatformOAuthAppPolicyOverride(settings, "twitch", out InstanceOAuthAppPolicy twitchOverride).Should().BeTrue();
        _ = twitchOverride.Should().Be(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);

        AuthPolicyParser.SetPlatformOAuthAppPolicy(settings, "twitch", null);
        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settings, "twitch").Should().Be(InstanceOAuthAppPolicy.InstanceOnly);
        _ = AuthPolicyParser.TryGetPlatformOAuthAppPolicyOverride(settings, "twitch", out _).Should().BeFalse();

        AuthPolicyParser.SetPlatformOAuthAppPolicy(settings, "kick", null);
        _ = settings.PlatformOAuthAppPoliciesJson.Should().BeNull();
    }

    [Theory]
    [Trait("Member", "ResolveInstanceName")]
    [InlineData(null, "StreamActions")]
    [InlineData("", "StreamActions")]
    [InlineData("   ", "StreamActions")]
    [InlineData("  My Custom StreamHub  ", "My Custom StreamHub")]
    public void ResolveInstanceNameAndTotpIssuerNameFallbackToStreamActionsWhenBlank(string? configured, string expected)
    {
        InstanceSettings settings = new()
        {
            InstanceName = configured ?? string.Empty,
            TotpIssuerName = configured ?? string.Empty,
        };

        _ = AuthPolicyParser.ResolveInstanceName(settings).Should().Be(expected);
        _ = AuthPolicyParser.ResolveTotpIssuerName(settings).Should().Be(expected);
    }

    #endregion

    #region FormatPageTitle

    [Theory]
    [Trait("Member", "FormatPageTitle")]
    [InlineData("Sign In", null, "Sign In - StreamActions")]
    [InlineData("Sign In", "", "Sign In - StreamActions")]
    [InlineData("Sign In", "   ", "Sign In - StreamActions")]
    [InlineData("Sign In", "StreamActions", "Sign In - StreamActions")]
    [InlineData("Sign In", "streamactions", "Sign In - StreamActions")]
    [InlineData("Sign In", "  STREAMACTIONS  ", "Sign In - StreamActions")]
    [InlineData("Sign In", "CreatorHub", "Sign In - CreatorHub - StreamActions")]
    [InlineData("  Sign In  ", "  CreatorHub  ", "Sign In - CreatorHub - StreamActions")]
    [InlineData(null, null, "StreamActions")]
    [InlineData(null, "", "StreamActions")]
    [InlineData(null, "   ", "StreamActions")]
    [InlineData(null, "StreamActions", "StreamActions")]
    [InlineData(null, "streamactions", "StreamActions")]
    [InlineData("", "CreatorHub", "CreatorHub - StreamActions")]
    [InlineData("   ", "  CreatorHub  ", "CreatorHub - StreamActions")]
    [InlineData(null, "CreatorHub", "CreatorHub - StreamActions")]
    public void FormatPageTitleWithPageAndInstanceNameReturnsExpectedTitle(
        string? pageName,
        string? instanceName,
        string expected)
    {
        string result = AuthPolicyParser.FormatPageTitle(pageName, instanceName);
        _ = result.Should().Be(expected);
    }

    [Theory]
    [Trait("Member", "FormatPageTitle")]
    [InlineData("Sign In", null, "Sign In - StreamActions")]
    [InlineData("Sign In", "", "Sign In - StreamActions")]
    [InlineData("Sign In", "StreamActions", "Sign In - StreamActions")]
    [InlineData("Sign In", "streamactions", "Sign In - StreamActions")]
    [InlineData("Sign In", "CreatorHub", "Sign In - CreatorHub - StreamActions")]
    [InlineData(null, null, "StreamActions")]
    [InlineData(null, "StreamActions", "StreamActions")]
    [InlineData(null, "CreatorHub", "CreatorHub - StreamActions")]
    public void FormatPageTitleWithInstanceSettingsReturnsExpectedTitle(
        string? pageName,
        string? instanceName,
        string expected)
    {
        InstanceSettings? settings = instanceName is null
            ? null
            : new InstanceSettings { InstanceName = instanceName };

        string result = AuthPolicyParser.FormatPageTitle(pageName, settings);
        _ = result.Should().Be(expected);
    }

    #endregion

    #region ParseBotAccountPolicy

    [Theory]
    [Trait("Member", "ParseBotAccountPolicy")]
    [InlineData(null, InstanceBotAccountPolicy.DefaultOnly)]
    [InlineData("instance_default", InstanceBotAccountPolicy.DefaultOnly)]
    [InlineData("DefaultOnly", InstanceBotAccountPolicy.DefaultOnly)]
    [InlineData("default_with_optional_override", InstanceBotAccountPolicy.DefaultWithOptionalOverride)]
    [InlineData("require_streamer_bot_account", InstanceBotAccountPolicy.RequireStreamerBotAccount)]
    [InlineData("2", InstanceBotAccountPolicy.RequireStreamerBotAccount)]
    public void ParseBotAccountPolicyWithValidValuesReturnsExpectedEnum(string? input, InstanceBotAccountPolicy expected)
    {
        InstanceBotAccountPolicy result = AuthPolicyParser.ParseBotAccountPolicy(input);
        _ = result.Should().Be(expected);
    }

    #endregion
}

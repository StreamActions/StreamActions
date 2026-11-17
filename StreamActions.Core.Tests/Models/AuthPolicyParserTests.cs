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
    [InlineData("require_streamer_oauth_app", InstanceOAuthAppPolicy.RequireStreamerOAuthApp)]
    [InlineData("2", InstanceOAuthAppPolicy.RequireStreamerOAuthApp)]
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

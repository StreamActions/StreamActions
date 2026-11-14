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
using Microsoft.Extensions.Configuration;
using StreamActions.Core.Configuration;
using StreamActions.Daemon.Cli;
using Xunit;

namespace StreamActions.Daemon.Tests;

public sealed class DaemonCliParserTests
{
    #region Parse
    [Fact]
    [Trait("Member", "Parse")]
    public void Parse_WhenAllDaemonFlagsProvided_MapsBootstrapOverridesAndOptions()
    {
        string[] args =
        [
            "--no-web",
            "--mode=source",
            "--shard-id=3",
            "--config-dir=/tmp/cfg",
            "--data-dir=/tmp/data",
            "--log-dir=/tmp/logs",
            "--plugins-dir=/tmp/plugins",
            "--backup-dir=/tmp/backups",
            "--bootstrap-log-level=Debug",
            "--sentry-opt-in",
        ];

        DaemonCliOptions parsed = DaemonCliParser.Parse(args);

        _ = parsed.NoWeb.Should().BeTrue();
        _ = parsed.SentryOptIn.Should().BeTrue();
        _ = parsed.IsExecMode.Should().BeFalse();
        _ = parsed.RemainingArgs.Should().BeEmpty();
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Node:Mode", "Source"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Node:ShardId", "3"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Paths:ConfigDir", "/tmp/cfg"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Paths:DataDir", "/tmp/data"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Paths:LogDir", "/tmp/logs"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Paths:PluginsDir", "/tmp/plugins"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Paths:BackupDir", "/tmp/backups"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:BootstrapLogLevel", "Debug"));
        _ = parsed.ConfigurationOverrides.Should().Contain(new KeyValuePair<string, string?>("Bootstrap:Sentry:Enabled", "true"));
    }

    [Theory]
    [Trait("Member", "Parse")]
    [InlineData("all", "AllInOne")]
    [InlineData("source", "Source")]
    [InlineData("sink", "Sink")]
    public void Parse_WhenSpaceSeparatedModeProvided_MapsExpectedNodeMode(string inputMode, string expectedMode)
    {
        DaemonCliOptions parsed = DaemonCliParser.Parse(["--mode", inputMode]);

        _ = parsed.ConfigurationOverrides["Bootstrap:Node:Mode"].Should().Be(expectedMode);
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void Parse_WhenExecCommandProvided_CapturesExecModeAndArguments()
    {
        DaemonCliOptions parsed = DaemonCliParser.Parse(["--data-dir=/var/lib/sa", "exec", "unlock-user", "broadcaster1"]);

        _ = parsed.IsExecMode.Should().BeTrue();
        _ = parsed.ExecCommand.Should().Be("unlock-user broadcaster1");
        _ = parsed.ConfigurationOverrides["Bootstrap:Paths:DataDir"].Should().Be("/var/lib/sa");
    }

    [Fact]
    [Trait("Member", "Parse")]
    public void Parse_WhenUnknownHostFlagsProvided_PreservesThemWithoutError()
    {
        string[] args = ["--environment", "Testing", "--applicationName=StreamActions.Daemon", "--unknown-flag=123", "--no-web"];

        DaemonCliOptions parsed = DaemonCliParser.Parse(args);

        _ = parsed.NoWeb.Should().BeTrue();
        _ = parsed.RemainingArgs.Should().Equal(["--environment", "Testing", "--applicationName=StreamActions.Daemon", "--unknown-flag=123"]);
    }
    #endregion

    #region GetLinuxFhsDefaults
    [Fact]
    [Trait("Member", "GetLinuxFhsDefaults")]
    public void GetLinuxFhsDefaults_WhenCalled_ReturnsStandardFhsPaths()
    {
        PathBootstrapOptions fhs = DaemonBootstrapConfiguration.GetLinuxFhsDefaults();

        _ = fhs.ConfigDir.Should().Be("/etc/streamactions");
        _ = fhs.DataDir.Should().Be("/var/lib/streamactions");
        _ = fhs.LogDir.Should().Be("/var/log/streamactions");
        _ = fhs.PluginsDir.Should().Be("/var/lib/streamactions/plugins");
        _ = fhs.BackupDir.Should().Be("/var/lib/streamactions/backups");
    }

    [Fact]
    [Trait("Member", "GetDefaultPathConfiguration")]
    public void GetDefaultPathConfiguration_WhenFhsEnabled_AllowsCliOverridesToTakePrecedence()
    {
        IReadOnlyDictionary<string, string?> fhs = DaemonBootstrapConfiguration.GetDefaultPathConfiguration(useLinuxFhs: true);
        DaemonCliOptions cli = DaemonCliParser.Parse(["--data-dir=/custom/data"]);

        ConfigurationBuilder builder = new();
        _ = builder.AddInMemoryCollection(fhs);
        _ = builder.AddInMemoryCollection(cli.ConfigurationOverrides);
        IConfigurationRoot config = builder.Build();

        _ = config["Bootstrap:Paths:ConfigDir"].Should().Be("/etc/streamactions");
        _ = config["Bootstrap:Paths:DataDir"].Should().Be("/custom/data");
    }
    #endregion
}

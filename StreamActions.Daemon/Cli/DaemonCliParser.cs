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

using StreamActions.Core.Models;

namespace StreamActions.Daemon.Cli;

/// <summary>
/// Parses daemon command-line flags and <c>exec</c> IPC subcommands while tolerating unknown host arguments.
/// </summary>
internal static class DaemonCliParser
{
    /// <summary>
    /// Parses the provided command-line arguments into <see cref="DaemonCliOptions"/>.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The parsed <see cref="DaemonCliOptions"/> instance.</returns>
    public static DaemonCliOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        bool noWeb = false;
        bool sentryOptIn = false;
        bool isExecMode = false;
        string? execCommand = null;
        Dictionary<string, string?> overrides = new(StringComparer.OrdinalIgnoreCase);
        List<string> remainingArgs = [];

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (string.Equals(arg, "exec", StringComparison.OrdinalIgnoreCase))
            {
                isExecMode = true;
                execCommand = i + 1 < args.Length
                    ? string.Join(' ', args[(i + 1)..])
                    : string.Empty;
                break;
            }

            if (string.Equals(arg, "--no-web", StringComparison.OrdinalIgnoreCase))
            {
                noWeb = true;
                continue;
            }

            if (string.Equals(arg, "--sentry-opt-in", StringComparison.OrdinalIgnoreCase))
            {
                sentryOptIn = true;
                overrides["Bootstrap:Sentry:Enabled"] = "true";
                continue;
            }

            if (TryReadOption(args, ref i, "--mode", out string? modeValue))
            {
                if (TryParseNodeMode(modeValue, out NodeMode parsedMode))
                {
                    overrides["Bootstrap:Node:Mode"] = parsedMode.ToString();
                }

                continue;
            }

            if (TryReadOption(args, ref i, "--shard-id", out string? shardId))
            {
                overrides["Bootstrap:Node:ShardId"] = shardId;
                continue;
            }

            if (TryReadOption(args, ref i, "--config-dir", out string? configDir))
            {
                overrides["Bootstrap:Paths:ConfigDir"] = configDir;
                continue;
            }

            if (TryReadOption(args, ref i, "--data-dir", out string? dataDir))
            {
                overrides["Bootstrap:Paths:DataDir"] = dataDir;
                continue;
            }

            if (TryReadOption(args, ref i, "--log-dir", out string? logDir))
            {
                overrides["Bootstrap:Paths:LogDir"] = logDir;
                continue;
            }

            if (TryReadOption(args, ref i, "--plugins-dir", out string? pluginsDir))
            {
                overrides["Bootstrap:Paths:PluginsDir"] = pluginsDir;
                continue;
            }

            if (TryReadOption(args, ref i, "--backup-dir", out string? backupDir))
            {
                overrides["Bootstrap:Paths:BackupDir"] = backupDir;
                continue;
            }

            if (TryReadOption(args, ref i, "--bootstrap-log-level", out string? logLevel))
            {
                overrides["Bootstrap:BootstrapLogLevel"] = logLevel;
                continue;
            }

            remainingArgs.Add(arg);
        }

        return new DaemonCliOptions
        {
            NoWeb = noWeb,
            SentryOptIn = sentryOptIn,
            IsExecMode = isExecMode,
            ExecCommand = execCommand,
            ConfigurationOverrides = overrides,
            RemainingArgs = remainingArgs,
        };
    }

    /// <summary>
    /// Attempts to read an option value from <paramref name="args"/> at <paramref name="index"/> in either <c>--opt=val</c> or <c>--opt val</c> form.
    /// </summary>
    /// <param name="args">The command-line argument array.</param>
    /// <param name="index">The current argument index, advanced if the value is read from the next argument.</param>
    /// <param name="optionName">The option flag name to match.</param>
    /// <param name="value">When this method returns, contains the option value if matched; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the option was matched; otherwise <see langword="false"/>.</returns>
    private static bool TryReadOption(string[] args, ref int index, string optionName, out string? value)
    {
        string arg = args[index];
        string prefix = $"{optionName}=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];
            return true;
        }

        if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase) && (index + 1 < args.Length))
        {
            index++;
            value = args[index];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Attempts to parse a node mode string or alias into a <see cref="NodeMode"/> value.
    /// </summary>
    /// <param name="value">The raw mode string.</param>
    /// <param name="mode">When this method returns, contains the parsed <see cref="NodeMode"/> if successful.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was recognized; otherwise <see langword="false"/>.</returns>
    private static bool TryParseNodeMode(string? value, out NodeMode mode)
    {
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "allinone", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "all-in-one", StringComparison.OrdinalIgnoreCase))
        {
            mode = NodeMode.AllInOne;
            return true;
        }

        if (string.Equals(value, "source", StringComparison.OrdinalIgnoreCase))
        {
            mode = NodeMode.Source;
            return true;
        }

        if (string.Equals(value, "sink", StringComparison.OrdinalIgnoreCase))
        {
            mode = NodeMode.Sink;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out mode);
    }
}

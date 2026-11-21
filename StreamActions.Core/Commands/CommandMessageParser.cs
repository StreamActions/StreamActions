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

using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Messaging;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StreamActions.Core.Commands;

/// <summary>
/// Represents the result of parsing a raw inbound command message before binding to a channel or whisper envelope.
/// </summary>
/// <param name="Prefix">The matched command prefix.</param>
/// <param name="Trigger">The normalized lowercase command trigger without prefix.</param>
/// <param name="Arguments">The parsed arguments with quoted segments unquoted.</param>
/// <param name="RawArguments">The trimmed raw argument substring following the trigger.</param>
public sealed record ParsedCommandMessage(
    string Prefix,
    string Trigger,
    IReadOnlyList<string> Arguments,
    string RawArguments);

/// <summary>
/// Parses raw chat or whisper message text into normalized command triggers and quoted/unquoted argument lists.
/// </summary>
public static class CommandMessageParser
{
    /// <summary>
    /// Default command prefix used when no explicit prefix list is supplied.
    /// </summary>
    public const string DefaultPrefix = "!";

    /// <summary>
    /// Default single-element prefix list containing <see cref="DefaultPrefix"/>.
    /// </summary>
    private static readonly IReadOnlyList<string> _defaultPrefixes = [DefaultPrefix];

    /// <summary>
    /// Attempts to parse raw message text using <see cref="DefaultPrefix"/>.
    /// </summary>
    /// <param name="rawText">The raw inbound message text.</param>
    /// <param name="parsed">When this method returns <see langword="true"/>, contains the parsed command details.</param>
    /// <returns><see langword="true"/> if <paramref name="rawText"/> represents a valid command invocation; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(
        string? rawText,
        [NotNullWhen(true)] out ParsedCommandMessage? parsed) =>
        TryParse(rawText, _defaultPrefixes, out parsed);

    /// <summary>
    /// Attempts to parse raw message text using a single command prefix.
    /// </summary>
    /// <param name="rawText">The raw inbound message text.</param>
    /// <param name="prefix">The command prefix to match.</param>
    /// <param name="parsed">When this method returns <see langword="true"/>, contains the parsed command details.</param>
    /// <returns><see langword="true"/> if <paramref name="rawText"/> represents a valid command invocation; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(
        string? rawText,
        string prefix,
        [NotNullWhen(true)] out ParsedCommandMessage? parsed)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            parsed = null;
            return false;
        }

        return TryParse(rawText, [prefix], out parsed);
    }

    /// <summary>
    /// Attempts to parse raw message text using one of the specified command prefixes (longest prefix matched first).
    /// </summary>
    /// <param name="rawText">The raw inbound message text.</param>
    /// <param name="prefixes">The allowed command prefixes, or <see langword="null"/> to use <see cref="DefaultPrefix"/>.</param>
    /// <param name="parsed">When this method returns <see langword="true"/>, contains the parsed command details.</param>
    /// <returns><see langword="true"/> if <paramref name="rawText"/> represents a valid command invocation; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command triggers to lowercase invariant.")]
    public static bool TryParse(
        string? rawText,
        IReadOnlyList<string>? prefixes,
        [NotNullWhen(true)] out ParsedCommandMessage? parsed)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            parsed = null;
            return false;
        }

        string trimmed = rawText.Trim();
        IReadOnlyList<string> effectivePrefixes = prefixes ?? _defaultPrefixes;
        if (effectivePrefixes.Count == 0)
        {
            effectivePrefixes = _defaultPrefixes;
        }

        string? matchedPrefix = null;
        foreach (string candidate in effectivePrefixes)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (trimmed.StartsWith(candidate, StringComparison.Ordinal)
                && (matchedPrefix is null || candidate.Length > matchedPrefix.Length))
            {
                matchedPrefix = candidate;
            }
        }

        if (matchedPrefix is null)
        {
            parsed = null;
            return false;
        }

        ReadOnlySpan<char> afterPrefix = trimmed.AsSpan(matchedPrefix.Length);
        if (afterPrefix.IsEmpty || char.IsWhiteSpace(afterPrefix[0]))
        {
            parsed = null;
            return false;
        }

        int firstWhitespaceIndex = -1;
        for (int i = 0; i < afterPrefix.Length; i++)
        {
            if (char.IsWhiteSpace(afterPrefix[i]))
            {
                firstWhitespaceIndex = i;
                break;
            }
        }

        string rawTrigger;
        string rawArguments;
        if (firstWhitespaceIndex < 0)
        {
            rawTrigger = afterPrefix.ToString();
            rawArguments = string.Empty;
        }
        else
        {
            rawTrigger = afterPrefix[..firstWhitespaceIndex].ToString();
            rawArguments = afterPrefix[firstWhitespaceIndex..].Trim().ToString();
        }

        if (string.IsNullOrWhiteSpace(rawTrigger))
        {
            parsed = null;
            return false;
        }

        string normalizedTrigger = rawTrigger.ToLowerInvariant();
        IReadOnlyList<string> arguments = ParseArguments(rawArguments);

        parsed = new ParsedCommandMessage(matchedPrefix, normalizedTrigger, arguments, rawArguments);
        return true;
    }

    /// <summary>
    /// Attempts to parse an inbound <see cref="IChatMessage"/> into an <see cref="IChatCommand"/> using <see cref="DefaultPrefix"/>.
    /// </summary>
    /// <param name="message">The inbound chat message.</param>
    /// <param name="command">When this method returns <see langword="true"/>, contains the parsed <see cref="IChatCommand"/>.</param>
    /// <returns><see langword="true"/> if the message is a valid command invocation; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseChatCommand(
        IChatMessage message,
        [NotNullWhen(true)] out IChatCommand? command) =>
        TryParseChatCommand(message, _defaultPrefixes, out command);

    /// <summary>
    /// Attempts to parse an inbound <see cref="IChatMessage"/> into an <see cref="IChatCommand"/>.
    /// </summary>
    /// <param name="message">The inbound chat message.</param>
    /// <param name="prefixes">Optional command prefixes; defaults to <see cref="DefaultPrefix"/> when <see langword="null"/>.</param>
    /// <param name="command">When this method returns <see langword="true"/>, contains the parsed <see cref="IChatCommand"/>.</param>
    /// <returns><see langword="true"/> if the message is a valid command invocation; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseChatCommand(
        IChatMessage message,
        IReadOnlyList<string>? prefixes,
        [NotNullWhen(true)] out IChatCommand? command)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!TryParse(message.RawText, prefixes, out ParsedCommandMessage? parsed))
        {
            command = null;
            return false;
        }

        command = new ChatCommand(
            message.WorkspaceId,
            message.PlatformId,
            message.MessageId,
            message.Sender,
            message.Channel,
            message.RawText,
            message.ReceivedAtUtc,
            parsed.Prefix,
            parsed.Trigger,
            parsed.Arguments,
            parsed.RawArguments,
            message.ReplyToMessageId,
            message.IsAction,
            message.Metadata);

        return true;
    }

    /// <summary>
    /// Attempts to parse an inbound <see cref="IWhisper"/> into an <see cref="IWhisperCommand"/> using <see cref="DefaultPrefix"/>.
    /// </summary>
    /// <param name="whisper">The inbound whisper message.</param>
    /// <param name="command">When this method returns <see langword="true"/>, contains the parsed <see cref="IWhisperCommand"/>.</param>
    /// <returns><see langword="true"/> if the whisper is a valid command invocation; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseWhisperCommand(
        IWhisper whisper,
        [NotNullWhen(true)] out IWhisperCommand? command) =>
        TryParseWhisperCommand(whisper, _defaultPrefixes, out command);

    /// <summary>
    /// Attempts to parse an inbound <see cref="IWhisper"/> into an <see cref="IWhisperCommand"/>.
    /// </summary>
    /// <param name="whisper">The inbound whisper message.</param>
    /// <param name="prefixes">Optional command prefixes; defaults to <see cref="DefaultPrefix"/> when <see langword="null"/>.</param>
    /// <param name="command">When this method returns <see langword="true"/>, contains the parsed <see cref="IWhisperCommand"/>.</param>
    /// <returns><see langword="true"/> if the whisper is a valid command invocation; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseWhisperCommand(
        IWhisper whisper,
        IReadOnlyList<string>? prefixes,
        [NotNullWhen(true)] out IWhisperCommand? command)
    {
        ArgumentNullException.ThrowIfNull(whisper);

        if (!TryParse(whisper.RawText, prefixes, out ParsedCommandMessage? parsed))
        {
            command = null;
            return false;
        }

        command = new WhisperCommand(
            whisper.WorkspaceId,
            whisper.PlatformId,
            whisper.MessageId,
            whisper.Sender,
            whisper.RawText,
            whisper.ReceivedAtUtc,
            parsed.Prefix,
            parsed.Trigger,
            parsed.Arguments,
            parsed.RawArguments,
            whisper.TargetPlatformUserId);

        return true;
    }

    /// <summary>
    /// Splits a raw argument string into individual arguments, respecting double-quoted segments (with backslash escapes) and balanced single-quoted segments.
    /// </summary>
    /// <param name="rawArguments">The raw argument string to split.</param>
    /// <returns>An immutable read-only list of parsed arguments.</returns>
    public static IReadOnlyList<string> ParseArguments(string? rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments))
        {
            return [];
        }

        List<string> args = [];
        StringBuilder current = new();
        int index = 0;
        int length = rawArguments.Length;

        while (index < length)
        {
            while (index < length && char.IsWhiteSpace(rawArguments[index]))
            {
                index++;
            }

            if (index >= length)
            {
                break;
            }

            char startChar = rawArguments[index];
            if (startChar == '"')
            {
                index++;
                _ = current.Clear();
                while (index < length)
                {
                    char c = rawArguments[index];
                    if (c == '\\' && (index + 1) < length && (rawArguments[index + 1] == '"' || rawArguments[index + 1] == '\\'))
                    {
                        _ = current.Append(rawArguments[index + 1]);
                        index += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        index++;
                        break;
                    }

                    _ = current.Append(c);
                    index++;
                }

                args.Add(current.ToString());
            }
            else if (startChar == '\'' && TryFindClosingSingleQuote(rawArguments, index + 1, out int closingQuoteIndex))
            {
                index++;
                _ = current.Clear();
                while (index < closingQuoteIndex)
                {
                    char c = rawArguments[index];
                    if (c == '\\' && (index + 1) < closingQuoteIndex && (rawArguments[index + 1] == '\'' || rawArguments[index + 1] == '\\'))
                    {
                        _ = current.Append(rawArguments[index + 1]);
                        index += 2;
                        continue;
                    }

                    _ = current.Append(c);
                    index++;
                }

                index = closingQuoteIndex + 1;
                args.Add(current.ToString());
            }
            else
            {
                _ = current.Clear();
                while (index < length && !char.IsWhiteSpace(rawArguments[index]))
                {
                    _ = current.Append(rawArguments[index]);
                    index++;
                }

                args.Add(current.ToString());
            }
        }

        return args.AsReadOnly();
    }

    /// <summary>
    /// Searches forward from <paramref name="startIndex"/> for a matching unescaped single quote followed by whitespace or end-of-string.
    /// </summary>
    /// <param name="input">The raw argument text.</param>
    /// <param name="startIndex">The index immediately after the opening single quote.</param>
    /// <param name="closingQuoteIndex">When this method returns <see langword="true"/>, contains the index of the closing single quote.</param>
    /// <returns><see langword="true"/> if a matching closing single quote exists at a token boundary; otherwise, <see langword="false"/>.</returns>
    private static bool TryFindClosingSingleQuote(string input, int startIndex, out int closingQuoteIndex)
    {
        for (int i = startIndex; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\\' && (i + 1) < input.Length && (input[i + 1] == '\'' || input[i + 1] == '\\'))
            {
                i++;
                continue;
            }

            if (c == '\'' && ((i + 1) == input.Length || char.IsWhiteSpace(input[i + 1])))
            {
                closingQuoteIndex = i;
                return true;
            }
        }

        closingQuoteIndex = -1;
        return false;
    }
}

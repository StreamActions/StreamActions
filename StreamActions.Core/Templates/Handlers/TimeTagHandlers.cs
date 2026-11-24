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

using System.Globalization;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Shared cross-OS timezone and duration formatting helpers for time-related template tag handlers.
/// </summary>
internal static class TimeTagFormatting
{
    /// <summary>
    /// Resolves an IANA or Windows timezone identifier safely across operating systems, defaulting to UTC when omitted.
    /// </summary>
    /// <param name="timeZoneId">The optional timezone identifier.</param>
    /// <param name="timeZone">When this method returns <see langword="true"/>, contains the resolved <see cref="TimeZoneInfo"/>.</param>
    /// <returns><see langword="true"/> if the timezone was resolved; otherwise, <see langword="false"/>.</returns>
    internal static bool TryResolveTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZone = TimeZoneInfo.Utc;
            return true;
        }

        string trimmed = timeZoneId.Trim();
        if (string.Equals(trimmed, "UTC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Etc/UTC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Z", StringComparison.OrdinalIgnoreCase))
        {
            timeZone = TimeZoneInfo.Utc;
            return true;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(trimmed);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback to IANA/Windows conversion when running on environments with incomplete ID mappings.
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmed, out string? windowsId))
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmed, out string? ianaId))
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        timeZone = TimeZoneInfo.Utc;
        return false;
    }

    /// <summary>
    /// Parses a target ISO-8601 or invariant date/time string into UTC <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="rawDate">The raw date/time string.</param>
    /// <param name="targetUtc">When this method returns <see langword="true"/>, contains the parsed UTC timestamp.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    internal static bool TryParseTargetDate(string? rawDate, out DateTimeOffset targetUtc)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            targetUtc = default;
            return false;
        }

        return DateTimeOffset.TryParse(
            rawDate.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out targetUtc);
    }

    /// <summary>
    /// Formats a non-negative <see cref="TimeSpan"/> into a compact human-readable duration (for example, <c>2d 3h 4m 5s</c>).
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>The formatted duration string using <see cref="CultureInfo.InvariantCulture"/>.</returns>
    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0s";
        }

        int totalDays = (int)duration.TotalDays;
        if (totalDays > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{totalDays}d {duration.Hours}h {duration.Minutes}m {duration.Seconds}s");
        }

        return duration.Hours > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s")
            : duration.Minutes > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{duration.Minutes}m {duration.Seconds}s")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.Seconds}s");
    }
}

/// <summary>
/// Handles the <c>{time [tz] [format]}</c> tag using <see cref="ITagEvaluationContext.TimeProvider"/> and <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class TimeTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Default timestamp format used when no explicit format argument is provided.
    /// </summary>
    public const string DefaultFormat = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc />
    public string TagName => "time";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        string? tzArg = null;
        string format = DefaultFormat;

        if (unevaluatedArgs.Count >= 1)
        {
            TagResult tzResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!tzResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            tzArg = tzResult.Value;
        }

        if (unevaluatedArgs.Count >= 2)
        {
            TagResult formatResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!formatResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            if (!string.IsNullOrWhiteSpace(formatResult.Value))
            {
                format = formatResult.Value;
            }
        }

        if (!TimeTagFormatting.TryResolveTimeZone(tzArg, out TimeZoneInfo tz))
        {
            return TagResult.Incomplete;
        }

        DateTimeOffset nowUtc = context.TimeProvider.GetUtcNow();
        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(nowUtc, tz);

        try
        {
            return TagResult.Complete(localTime.ToString(format, CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            return TagResult.Incomplete;
        }
    }
}

/// <summary>
/// Handles the <c>{countdown &lt;isoOrDate&gt;}</c> tag, formatting the remaining duration until a target timestamp.
/// </summary>
public sealed class CountdownTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "countdown";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        TagResult targetResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!targetResult.IsComplete || !TimeTagFormatting.TryParseTargetDate(targetResult.Value, out DateTimeOffset targetUtc))
        {
            return TagResult.Incomplete;
        }

        TimeSpan remaining = targetUtc - context.TimeProvider.GetUtcNow();
        return TagResult.Complete(TimeTagFormatting.FormatDuration(remaining));
    }
}

/// <summary>
/// Handles the <c>{countup &lt;isoOrDate&gt;}</c> tag, formatting the elapsed duration since a past timestamp.
/// </summary>
public sealed class CountupTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "countup";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        TagResult targetResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!targetResult.IsComplete || !TimeTagFormatting.TryParseTargetDate(targetResult.Value, out DateTimeOffset targetUtc))
        {
            return TagResult.Incomplete;
        }

        TimeSpan elapsed = context.TimeProvider.GetUtcNow() - targetUtc;
        return TagResult.Complete(TimeTagFormatting.FormatDuration(elapsed));
    }
}

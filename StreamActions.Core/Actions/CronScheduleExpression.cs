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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StreamActions.Core.Actions;

/// <summary>
/// Parses and evaluates standard 5-field UTC cron expressions (<c>minute hour day-of-month month day-of-week</c>).
/// </summary>
public sealed class CronScheduleExpression
{
    /// <summary>
    /// Allowed minute values (0-59).
    /// </summary>
    private readonly bool[] _minutes;

    /// <summary>
    /// Allowed hour values (0-23).
    /// </summary>
    private readonly bool[] _hours;

    /// <summary>
    /// Allowed day-of-month values (1-31).
    /// </summary>
    private readonly bool[] _daysOfMonth;

    /// <summary>
    /// Allowed month values (1-12).
    /// </summary>
    private readonly bool[] _months;

    /// <summary>
    /// Allowed day-of-week values (0-6 where Sunday is 0).
    /// </summary>
    private readonly bool[] _daysOfWeek;

    /// <summary>
    /// Whether the day-of-month field was left wildcarded (<c>*</c>).
    /// </summary>
    private readonly bool _dayOfMonthWildcard;

    /// <summary>
    /// Whether the day-of-week field was left wildcarded (<c>*</c>).
    /// </summary>
    private readonly bool _dayOfWeekWildcard;

    /// <summary>
    /// Initializes a new instance of the <see cref="CronScheduleExpression"/> class.
    /// </summary>
    /// <param name="expression">The normalized 5-field expression string.</param>
    /// <param name="minutes">Allowed minute flags.</param>
    /// <param name="hours">Allowed hour flags.</param>
    /// <param name="daysOfMonth">Allowed day-of-month flags.</param>
    /// <param name="months">Allowed month flags.</param>
    /// <param name="daysOfWeek">Allowed day-of-week flags.</param>
    /// <param name="dayOfMonthWildcard">Whether day-of-month is unrestricted.</param>
    /// <param name="dayOfWeekWildcard">Whether day-of-week is unrestricted.</param>
    private CronScheduleExpression(
        string expression,
        bool[] minutes,
        bool[] hours,
        bool[] daysOfMonth,
        bool[] months,
        bool[] daysOfWeek,
        bool dayOfMonthWildcard,
        bool dayOfWeekWildcard)
    {
        this.Expression = expression;
        this._minutes = minutes;
        this._hours = hours;
        this._daysOfMonth = daysOfMonth;
        this._months = months;
        this._daysOfWeek = daysOfWeek;
        this._dayOfMonthWildcard = dayOfMonthWildcard;
        this._dayOfWeekWildcard = dayOfWeekWildcard;
    }

    /// <summary>
    /// Gets the raw normalized 5-field cron expression.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Attempts to parse a 5-field cron expression.
    /// </summary>
    /// <param name="expression">The cron expression to parse.</param>
    /// <param name="parsed">When successful, receives the parsed <see cref="CronScheduleExpression"/>.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(
        string? expression,
        [NotNullWhen(true)] out CronScheduleExpression? parsed) =>
        TryParseInternal(expression, out parsed, out _);

    /// <summary>
    /// Validates a 5-field cron expression and returns an error message when invalid.
    /// </summary>
    /// <param name="expression">The cron expression to validate.</param>
    /// <returns>An error description when invalid, or <see langword="null"/> when valid.</returns>
    public static string? Validate(string? expression) =>
        TryParseInternal(expression, out _, out string? error) ? null : error;

    /// <summary>
    /// Internal parser returning both the parsed expression and an error diagnostic.
    /// </summary>
    private static bool TryParseInternal(
        string? expression,
        [NotNullWhen(true)] out CronScheduleExpression? parsed,
        out string? error)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Cron expression cannot be empty.";
            return false;
        }

        string trimmed = expression.Trim();
        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            error = "Cron expression must contain exactly 5 fields: minute hour day-of-month month day-of-week.";
            return false;
        }

        if (!TryParseField(parts[0], 0, 59, allowSundaySeven: false, out bool[]? minutes, out _, out error)
            || !TryParseField(parts[1], 0, 23, allowSundaySeven: false, out bool[]? hours, out _, out error)
            || !TryParseField(parts[2], 1, 31, allowSundaySeven: false, out bool[]? daysOfMonth, out bool domWildcard, out error)
            || !TryParseField(parts[3], 1, 12, allowSundaySeven: false, out bool[]? months, out _, out error)
            || !TryParseField(parts[4], 0, 6, allowSundaySeven: true, out bool[]? daysOfWeek, out bool dowWildcard, out error))
        {
            return false;
        }

        parsed = new CronScheduleExpression(
            string.Join(' ', parts),
            minutes,
            hours,
            daysOfMonth,
            months,
            daysOfWeek,
            domWildcard,
            dowWildcard);
        error = null;
        return true;
    }

    /// <summary>
    /// Determines whether the specified UTC timestamp matches this cron schedule at minute precision.
    /// </summary>
    /// <param name="utcTimestamp">The UTC timestamp to evaluate.</param>
    /// <returns><see langword="true"/> if the minute, hour, month, and day match; otherwise, <see langword="false"/>.</returns>
    public bool Matches(DateTimeOffset utcTimestamp)
    {
        DateTimeOffset utc = utcTimestamp.ToUniversalTime();
        if (!this._minutes[utc.Minute] || !this._hours[utc.Hour] || !this._months[utc.Month])
        {
            return false;
        }

        bool domMatch = this._daysOfMonth[utc.Day];
        bool dowMatch = this._daysOfWeek[(int)utc.DayOfWeek];

        if (this._dayOfMonthWildcard && this._dayOfWeekWildcard)
        {
            return true;
        }

        if (this._dayOfMonthWildcard)
        {
            return dowMatch;
        }

        if (this._dayOfWeekWildcard)
        {
            return domMatch;
        }

        // Standard Vixie cron semantics: when both DOM and DOW are restricted, match either.
        return domMatch || dowMatch;
    }

    /// <summary>
    /// Computes the next UTC occurrence strictly after <paramref name="fromUtc"/>, searching up to 4 years ahead.
    /// </summary>
    /// <param name="fromUtc">The exclusive lower-bound UTC timestamp.</param>
    /// <returns>The next matching UTC timestamp truncated to minute precision, or <see langword="null"/> if none exists within 4 years.</returns>
    public DateTimeOffset? GetNextOccurrenceUtc(DateTimeOffset fromUtc)
    {
        DateTimeOffset utc = fromUtc.ToUniversalTime();
        DateTimeOffset candidate = new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
        candidate = candidate.AddMinutes(1);

        DateTimeOffset limit = candidate.AddYears(4);
        while (candidate <= limit)
        {
            if (this.Matches(candidate))
            {
                return candidate;
            }

            candidate = candidate.AddMinutes(1);
        }

        return null;
    }

    /// <summary>
    /// Parses a single cron field token supporting <c>*</c>, <c>*/step</c>, <c>start-end/step</c>, and comma-separated lists.
    /// </summary>
    /// <param name="field">The raw field token.</param>
    /// <param name="min">Minimum inclusive field value.</param>
    /// <param name="max">Maximum inclusive field value.</param>
    /// <param name="allowSundaySeven">Whether <c>7</c> maps to Sunday (<c>0</c>).</param>
    /// <param name="flags">The populated boolean lookup array.</param>
    /// <param name="isWildcard">Outputs whether the field is an unrestricted <c>*</c>.</param>
    /// <param name="error">Outputs a diagnostic error message on failure.</param>
    /// <returns><see langword="true"/> if parsed successfully; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseField(
        string field,
        int min,
        int max,
        bool allowSundaySeven,
        [NotNullWhen(true)] out bool[]? flags,
        out bool isWildcard,
        out string? error)
    {
        flags = new bool[max + 1];
        isWildcard = string.Equals(field, "*", StringComparison.Ordinal);

        string[] segments = field.Split(',', StringSplitOptions.TrimEntries);
        foreach (string segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
            {
                error = $"Invalid empty segment in cron field '{field}'.";
                flags = null;
                return false;
            }

            string rangePart = segment;
            int step = 1;
            int slashIndex = segment.IndexOf('/', StringComparison.Ordinal);
            if (slashIndex >= 0)
            {
                rangePart = segment[..slashIndex];
                string stepPart = segment[(slashIndex + 1)..];
                if (!int.TryParse(stepPart, NumberStyles.None, CultureInfo.InvariantCulture, out step) || step <= 0)
                {
                    error = $"Invalid step value '{stepPart}' in cron field '{field}'.";
                    flags = null;
                    return false;
                }
            }

            int start;
            int end;
            if (string.Equals(rangePart, "*", StringComparison.Ordinal))
            {
                start = min;
                end = max;
            }
            else if (rangePart.Contains('-', StringComparison.Ordinal))
            {
                string[] bounds = rangePart.Split('-', StringSplitOptions.TrimEntries);
                int effectiveMax = allowSundaySeven ? 7 : max;
                if (bounds.Length != 2
                    || !TryParseBound(bounds[0], min, effectiveMax, out start)
                    || !TryParseBound(bounds[1], min, effectiveMax, out end)
                    || start > end)
                {
                    error = $"Invalid range '{rangePart}' in cron field '{field}'.";
                    flags = null;
                    return false;
                }
            }
            else
            {
                int effectiveMax = allowSundaySeven ? 7 : max;
                if (!TryParseBound(rangePart, min, effectiveMax, out start))
                {
                    error = $"Invalid value '{rangePart}' in cron field '{field}'.";
                    flags = null;
                    return false;
                }

                end = slashIndex >= 0 ? max : start;
            }

            for (int value = start; value <= end; value += step)
            {
                int normalizedValue = allowSundaySeven && value == 7 ? 0 : value;
                if (normalizedValue >= min && normalizedValue <= max)
                {
                    flags[normalizedValue] = true;
                }
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Parses a single integer cron bound within [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    /// <param name="token">The raw integer token.</param>
    /// <param name="min">Minimum inclusive value.</param>
    /// <param name="max">Maximum inclusive value.</param>
    /// <param name="value">Outputs the parsed integer value.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseBound(string token, int min, int max, out int value) =>
        int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value)
        && value >= min
        && value <= max;
}

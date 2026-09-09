using System.Text.RegularExpressions;

namespace Scheduling.Cron;

/// <summary>
/// Default implementation of <see cref="ICronParser"/> for standard 5-field cron expressions.
/// Supports standard macros, ranges (-), steps (/), lists (,), weekday names (SUN-SAT), and month names (JAN-DEC).
/// </summary>
public class CronParser : ICronParser
{
    private static readonly Dictionary<string, string> Macros = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@yearly"] = "0 0 1 1 *",
        ["@annually"] = "0 0 1 1 *",
        ["@monthly"] = "0 0 1 * *",
        ["@weekly"] = "0 0 * * 0",
        ["@daily"] = "0 0 * * *",
        ["@midnight"] = "0 0 * * *",
        ["@hourly"] = "0 * * * *"
    };

    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JAN"] = 1, ["FEB"] = 2, ["MAR"] = 3, ["APR"] = 4, ["MAY"] = 5, ["JUN"] = 6,
        ["JUL"] = 7, ["AUG"] = 8, ["SEP"] = 9, ["OCT"] = 10, ["NOV"] = 11, ["DEC"] = 12
    };

    private static readonly Dictionary<string, int> DayOfWeekNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUN"] = 0, ["MON"] = 1, ["TUE"] = 2, ["WED"] = 3, ["THU"] = 4, ["FRI"] = 5, ["SAT"] = 6
    };

    public bool IsValid(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            var normalized = NormalizeExpression(expression);
            var parts = SplitParts(normalized);
            if (parts.Length != 5)
            {
                return false;
            }

            ParseField(parts[0], 0, 59);
            ParseField(parts[1], 0, 23);
            ParseField(parts[2], 1, 31);
            ParseMonthField(parts[3]);
            ParseDayOfWeekField(parts[4]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public DateTime? GetNextOccurrence(string expression, DateTime afterUtc)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var normalized = NormalizeExpression(expression);
        var parts = SplitParts(normalized);
        if (parts.Length != 5)
        {
            throw new ArgumentException($"Invalid cron expression: '{expression}'. Expected 5 fields.", nameof(expression));
        }

        var minutes = ParseField(parts[0], 0, 59);
        var hours = ParseField(parts[1], 0, 23);
        var daysOfMonth = ParseField(parts[2], 1, 31);
        var months = ParseMonthField(parts[3]);
        var daysOfWeek = ParseDayOfWeekField(parts[4]);

        var domIsWildcard = parts[2] == "*";
        var dowIsWildcard = parts[4] == "*";

        // Truncate to current minute, add 1 minute
        var current = new DateTime(afterUtc.Year, afterUtc.Month, afterUtc.Day, afterUtc.Hour, afterUtc.Minute, 0, DateTimeKind.Utc)
            .AddMinutes(1);

        var maxDate = afterUtc.AddYears(5);

        while (current <= maxDate)
        {
            // 1. Month check
            if (!months.Contains(current.Month))
            {
                // Advance to start of next month
                current = new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                continue;
            }

            // 2. Day check
            var dayMatches = CheckDayMatch(current, daysOfMonth, daysOfWeek, domIsWildcard, dowIsWildcard);
            if (!dayMatches)
            {
                // Advance to start of next day
                current = new DateTime(current.Year, current.Month, current.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
                continue;
            }

            // 3. Hour check
            if (!hours.Contains(current.Hour))
            {
                // Advance to start of next hour
                current = new DateTime(current.Year, current.Month, current.Day, current.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                continue;
            }

            // 4. Minute check
            if (!minutes.Contains(current.Minute))
            {
                current = current.AddMinutes(1);
                continue;
            }

            return current;
        }

        return null;
    }

    public IReadOnlyList<DateTime> GetNextOccurrences(string expression, DateTime afterUtc, int count)
    {
        if (count <= 0)
        {
            return Array.Empty<DateTime>();
        }

        var results = new List<DateTime>(count);
        var cursor = afterUtc;

        for (var i = 0; i < count; i++)
        {
            var next = GetNextOccurrence(expression, cursor);
            if (!next.HasValue)
            {
                break;
            }

            results.Add(next.Value);
            cursor = next.Value;
        }

        return results.AsReadOnly();
    }

    public string Describe(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "No expression specified";
        }

        var normalized = NormalizeExpression(expression);
        var parts = SplitParts(normalized);
        if (parts.Length != 5)
        {
            return expression;
        }

        var minPart = parts[0];
        var hourPart = parts[1];
        var domPart = parts[2];
        var monthPart = parts[3];
        var dowPart = parts[4];

        if (minPart == "*" && hourPart == "*" && domPart == "*" && monthPart == "*" && dowPart == "*")
        {
            return "Every minute";
        }

        if (minPart.StartsWith("*/") && hourPart == "*" && domPart == "*" && monthPart == "*" && dowPart == "*")
        {
            return $"Every {minPart[2..]} minutes";
        }

        if (minPart == "0" && hourPart.StartsWith("*/") && domPart == "*" && monthPart == "*" && dowPart == "*")
        {
            return $"Every {hourPart[2..]} hours";
        }

        if (minPart == "0" && hourPart == "*" && domPart == "*" && monthPart == "*" && dowPart == "*")
        {
            return "Every hour, at minute 0";
        }

        if (int.TryParse(minPart, out var min) && int.TryParse(hourPart, out var hr) && domPart == "*" && monthPart == "*" && dowPart == "*")
        {
            return $"At {hr:D2}:{min:D2}, every day";
        }

        if (int.TryParse(minPart, out var m2) && int.TryParse(hourPart, out var h2) && domPart != "*" && monthPart == "*" && dowPart == "*")
        {
            return $"At {h2:D2}:{m2:D2}, on day {domPart} of the month";
        }

        if (int.TryParse(minPart, out var m3) && int.TryParse(hourPart, out var h3) && domPart == "*" && monthPart == "*" && dowPart != "*")
        {
            var dowNames = FormatDowPart(dowPart);
            return $"At {h3:D2}:{m3:D2}, on {dowNames}";
        }

        return $"Cron ({normalized})";
    }

    private static string NormalizeExpression(string expression)
    {
        var trimmed = expression.Trim();
        if (Macros.TryGetValue(trimmed, out var expanded))
        {
            return expanded;
        }
        return trimmed;
    }

    private static string[] SplitParts(string expression)
    {
        return Regex.Split(expression.Trim(), @"\s+");
    }

    private static bool CheckDayMatch(
        DateTime dt,
        HashSet<int> daysOfMonth,
        HashSet<int> daysOfWeek,
        bool domIsWildcard,
        bool dowIsWildcard)
    {
        var domMatches = daysOfMonth.Contains(dt.Day);
        var dowValue = (int)dt.DayOfWeek; // 0=Sunday
        var dowMatches = daysOfWeek.Contains(dowValue);

        if (domIsWildcard && dowIsWildcard)
        {
            return true;
        }
        if (!domIsWildcard && dowIsWildcard)
        {
            return domMatches;
        }
        if (domIsWildcard && !dowIsWildcard)
        {
            return dowMatches;
        }

        // Both specified -> standard cron OR logic
        return domMatches || dowMatches;
    }

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        var result = new HashSet<int>();
        var segments = field.Split(',');

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new FormatException("Empty segment in cron field.");
            }

            if (segment == "*")
            {
                for (var i = min; i <= max; i++)
                {
                    result.Add(i);
                }
            }
            else if (segment.StartsWith("*/"))
            {
                var step = int.Parse(segment[2..]);
                if (step <= 0) throw new FormatException("Step must be positive.");
                for (var i = min; i <= max; i += step)
                {
                    result.Add(i);
                }
            }
            else if (segment.Contains('/'))
            {
                var parts = segment.Split('/');
                var step = int.Parse(parts[1]);
                if (step <= 0) throw new FormatException("Step must be positive.");

                var range = ParseRange(parts[0], min, max);
                for (var i = range.Start; i <= range.End; i += step)
                {
                    result.Add(i);
                }
            }
            else if (segment.Contains('-'))
            {
                var range = ParseRange(segment, min, max);
                for (var i = range.Start; i <= range.End; i++)
                {
                    result.Add(i);
                }
            }
            else
            {
                var val = int.Parse(segment);
                if (val < min || val > max)
                {
                    throw new ArgumentOutOfRangeException(nameof(field), $"Value {val} is outside allowed range {min}-{max}.");
                }
                result.Add(val);
            }
        }

        return result;
    }

    private static (int Start, int End) ParseRange(string rangeStr, int min, int max)
    {
        if (rangeStr == "*")
        {
            return (min, max);
        }

        var parts = rangeStr.Split('-');
        var start = int.Parse(parts[0]);
        var end = int.Parse(parts[1]);

        if (start < min || start > max || end < min || end > max || start > end)
        {
            throw new FormatException($"Invalid range: '{rangeStr}'.");
        }

        return (start, end);
    }

    private static HashSet<int> ParseMonthField(string field)
    {
        var replaced = field;
        foreach (var (name, num) in MonthNames)
        {
            replaced = Regex.Replace(replaced, $@"\b{name}\b", num.ToString(), RegexOptions.IgnoreCase);
        }
        return ParseField(replaced, 1, 12);
    }

    private static HashSet<int> ParseDayOfWeekField(string field)
    {
        var replaced = field;
        foreach (var (name, num) in DayOfWeekNames)
        {
            replaced = Regex.Replace(replaced, $@"\b{name}\b", num.ToString(), RegexOptions.IgnoreCase);
        }

        var parsed = ParseField(replaced, 0, 7);
        // 7 is Sunday in standard cron -> map to 0
        if (parsed.Contains(7))
        {
            parsed.Remove(7);
            parsed.Add(0);
        }
        return parsed;
    }

    private static string FormatDowPart(string dowPart)
    {
        return dowPart switch
        {
            "0" or "7" => "Sunday",
            "1" => "Monday",
            "2" => "Tuesday",
            "3" => "Wednesday",
            "4" => "Thursday",
            "5" => "Friday",
            "6" => "Saturday",
            "1-5" => "weekdays (Monday to Friday)",
            "0,6" or "6,0" => "weekends (Saturday and Sunday)",
            _ => $"day of week {dowPart}"
        };
    }
}

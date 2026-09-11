using Scheduling.Cron;
using Xunit;

namespace Scheduling.Tests;

public class CronParserTests
{
    private readonly CronParser _parser = new();

    [Theory]
    [InlineData("* * * * *", true)]
    [InlineData("0 6 * * *", true)]
    [InlineData("*/15 * * * *", true)]
    [InlineData("0 0 1 1 *", true)]
    [InlineData("0 0 * * 0", true)]
    [InlineData("0 0 * * SUN", true)]
    [InlineData("0 0 1 JAN *", true)]
    [InlineData("10-20/2 * * * *", true)]
    [InlineData("1,2,3 4,5,6 * * *", true)]
    [InlineData("@yearly", true)]
    [InlineData("@monthly", true)]
    [InlineData("@weekly", true)]
    [InlineData("@daily", true)]
    [InlineData("@hourly", true)]
    [InlineData("@midnight", true)]
    public void IsValid_ValidExpressions_ReturnsTrue(string expression, bool expected)
    {
        Assert.Equal(expected, _parser.IsValid(expression));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("* * * *", false)] // 4 fields
    [InlineData("* * * * * *", false)] // 6 fields
    [InlineData("60 * * * *", false)] // minute 60
    [InlineData("* 24 * * *", false)] // hour 24
    [InlineData("* * 32 * *", false)] // day 32
    [InlineData("* * * 13 *", false)] // month 13
    [InlineData("* * * * 8", false)] // weekday 8
    [InlineData("*/0 * * * *", false)] // step 0
    [InlineData("@invalid", false)]
    public void IsValid_InvalidExpressions_ReturnsFalse(string expression, bool expected)
    {
        Assert.Equal(expected, _parser.IsValid(expression));
    }

    [Fact]
    public void GetNextOccurrence_DailyAtSixAm_CalculatesCorrectDate()
    {
        // Reference: 2026-09-09 05:00:00 UTC
        var from = new DateTime(2026, 9, 9, 5, 0, 0, DateTimeKind.Utc);
        var next = _parser.GetNextOccurrence("0 6 * * *", from);

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 9, 9, 6, 0, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_DailyAtSixAm_WhenPastTime_CalculatesNextDay()
    {
        // Reference: 2026-09-09 07:00:00 UTC
        var from = new DateTime(2026, 9, 9, 7, 0, 0, DateTimeKind.Utc);
        var next = _parser.GetNextOccurrence("0 6 * * *", from);

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 9, 10, 6, 0, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_EveryFifteenMinutes_AdvancesCorrectly()
    {
        var from = new DateTime(2026, 9, 9, 10, 14, 0, DateTimeKind.Utc);
        var next = _parser.GetNextOccurrence("*/15 * * * *", from);

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 9, 9, 10, 15, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void GetNextOccurrences_ReturnsRequestedCount()
    {
        var from = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);
        var occurrences = _parser.GetNextOccurrences("0 * * * *", from, 3);

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateTime(2026, 9, 9, 11, 0, 0, DateTimeKind.Utc), occurrences[0]);
        Assert.Equal(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc), occurrences[1]);
        Assert.Equal(new DateTime(2026, 9, 9, 13, 0, 0, DateTimeKind.Utc), occurrences[2]);
    }

    [Fact]
    public void Describe_ReturnsReadableStrings()
    {
        Assert.Contains("minute", _parser.Describe("* * * * *"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15 minutes", _parser.Describe("*/15 * * * *"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("06:00", _parser.Describe("0 6 * * *"), StringComparison.OrdinalIgnoreCase);
    }
}

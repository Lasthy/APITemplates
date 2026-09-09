namespace Scheduling.Cron;

/// <summary>
/// Abstraction for parsing and evaluating standard 5-field cron expressions.
/// </summary>
public interface ICronParser
{
    /// <summary>
    /// Validates whether the given string is a valid cron expression or macro.
    /// </summary>
    bool IsValid(string expression);

    /// <summary>
    /// Computes the next occurrence after the given UTC datetime.
    /// </summary>
    DateTime? GetNextOccurrence(string expression, DateTime afterUtc);

    /// <summary>
    /// Computes the next N occurrences after the given UTC datetime.
    /// </summary>
    IReadOnlyList<DateTime> GetNextOccurrences(string expression, DateTime afterUtc, int count);

    /// <summary>
    /// Returns a human-readable English description of the cron expression.
    /// </summary>
    string Describe(string expression);
}

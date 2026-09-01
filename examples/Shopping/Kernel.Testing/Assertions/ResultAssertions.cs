using Kernel.Common;
using Kernel.Validation;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="Result"/>, <see cref="Result{T}"/>, and <see cref="PaginatedResult{T}"/>.
/// </summary>
public static class ResultAssertions
{
    /// <summary>
    /// Asserts that the non-generic result succeeded.
    /// </summary>
    public static Result ShouldBeSuccess(this Result result, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(result.IsSuccess, userMessage ?? $"Expected Result to be successful, but it failed with error: {result.Error?.Message}");
        return result;
    }

    /// <summary>
    /// Asserts that the non-generic result failed.
    /// </summary>
    public static Result ShouldBeFailure(this Result result, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(result.IsFailure, userMessage ?? "Expected Result to be failure, but it succeeded.");
        return result;
    }

    /// <summary>
    /// Asserts that the non-generic result failed with a specific exception type.
    /// </summary>
    public static TException ShouldBeFailure<TException>(this Result result, string? userMessage = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(result.IsFailure, userMessage ?? "Expected Result to be failure, but it succeeded.");
        Assert.NotNull(result.Error);
        Assert.IsAssignableFrom<TException>(result.Error);
        return (TException)result.Error;
    }

    /// <summary>
    /// Asserts that the generic result succeeded and unwraps its value.
    /// </summary>
    public static T ShouldBeSuccess<T>(this Result<T> result, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(result.IsSuccess, userMessage ?? $"Expected Result<{typeof(T).Name}> to be successful, but it failed with error: {result.Error?.Message}");
        return result.Value!;
    }

    /// <summary>
    /// Asserts that the generic result failed.
    /// </summary>
    public static Result<T> ShouldBeFailure<T>(this Result<T> result, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(result.IsFailure, userMessage ?? $"Expected Result<{typeof(T).Name}> to be failure, but it succeeded.");
        return result;
    }

    /// <summary>
    /// Asserts that the generic result failed with a specific exception type.
    /// </summary>
    public static TException ShouldBeFailure<T, TException>(this Result<T> result, string? userMessage = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(result.IsFailure, userMessage ?? $"Expected Result<{typeof(T).Name}> to be failure, but it succeeded.");
        Assert.NotNull(result.Error);
        Assert.IsAssignableFrom<TException>(result.Error);
        return (TException)result.Error;
    }

    /// <summary>
    /// Asserts that the result failed with a <see cref="ValidationException"/> and optionally checks for a specific field violation.
    /// </summary>
    public static ValidationException ShouldHaveValidationError(this Result result, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var ex = result.ShouldBeFailure<ValidationException>();
        if (!string.IsNullOrEmpty(field))
        {
            var hasField = ex.Errors.Any(e => string.Equals(e.Field, field, StringComparison.OrdinalIgnoreCase));
            Assert.True(hasField, $"Expected validation error for field '{field}', but errors were: {string.Join(", ", ex.Errors.Select(e => $"{e.Field}: {e.Message}"))}");
        }
        return ex;
    }

    /// <summary>
    /// Asserts that the generic result failed with a <see cref="ValidationException"/> and optionally checks for a specific field violation.
    /// </summary>
    public static ValidationException ShouldHaveValidationError<T>(this Result<T> result, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var ex = result.ShouldBeFailure<T, ValidationException>();
        if (!string.IsNullOrEmpty(field))
        {
            var hasField = ex.Errors.Any(e => string.Equals(e.Field, field, StringComparison.OrdinalIgnoreCase));
            Assert.True(hasField, $"Expected validation error for field '{field}', but errors were: {string.Join(", ", ex.Errors.Select(e => $"{e.Field}: {e.Message}"))}");
        }
        return ex;
    }

    /// <summary>
    /// Asserts pagination properties on a <see cref="PaginatedResult{T}"/>.
    /// </summary>
    public static PaginatedResult<T> ShouldHavePage<T>(
        this PaginatedResult<T> result,
        int expectedPage,
        int expectedPageSize,
        int expectedTotalCount)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ShouldBeSuccess();
        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
        Assert.Equal(expectedTotalCount, result.TotalCount);
        return result;
    }
}

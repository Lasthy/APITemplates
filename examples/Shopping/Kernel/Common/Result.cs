using System.Text.Json.Serialization;

namespace Kernel.Common;

/// <summary>
/// Represents the result of an application operation.
/// Encapsulates success/failure state, optional value, and related messages.
/// </summary>
public record Result
{
    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Optional informational or warning messages.
    /// </summary>
    public string[] Messages { get; } = [];

    /// <summary>
    /// Optional technical error message when the operation failed.
    /// </summary>
    [JsonIgnore]
    public Exception? Error { get; }

    protected Result(bool isSuccess, Exception? error = null, params string[] messages)
    {
        if (isSuccess && error is not null)
            throw new ArgumentException("A successful result cannot have an error message.", nameof(error));
        if (!isSuccess && error is null)
            throw new ArgumentException("A failure result must have an error.", nameof(error));

        IsSuccess = isSuccess;
        Error = error;
        Messages = messages;
    }

    /// <summary>
    /// Creates a successful result without a value.
    /// </summary>
    public static Result Success() => new(true);

    /// <summary>
    /// Creates a successful result with informational messages.
    /// </summary>
    public static Result Success(params string[] messages) => new(true, null, messages);

    /// <summary>
    /// Creates a failure result with an error message.
    /// </summary>
    public static Result Failure(Exception error) => new(false, error);

    /// <summary>
    /// Creates a failure result with an error message and additional messages.
    /// </summary>
    public static Result Failure(Exception error, params string[] messages) => new(false, error, messages);

    /// <summary>
    /// Creates a successful result with a typed value.
    /// </summary>
    public static Result<T> Success<T>(T value, params string[] messages) => new(value, true, null, messages);

    /// <summary>
    /// Creates a failure result for a typed operation.
    /// </summary>
    public static Result<T> Failure<T>(Exception error, params string[] messages) => new(default, false, error, messages);

	// Non-generic Result
    public TOut Match<TOut>(
        Func<TOut> onSuccess,
        Func<Exception, TOut> onFailure)
    {
        return IsSuccess
            ? onSuccess()
            : onFailure(Error!);
    }
}

/// <summary>
/// Represents a result containing a typed value.
/// </summary>
/// <remarks>
/// The constructor is intentionally internal to enforce creation through <see cref="Result"/>
/// factory methods, keeping success/failure invariants centralized.
/// </remarks>
/// <typeparam name="T">Type of the value.</typeparam>
public record Result<T> : Result
{
    /// <summary>
    /// Value returned on success. May be null for reference types.
    /// </summary>
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, Exception? error = null, params string[] messages)
        : base(isSuccess, error, messages)
    {
        Value = value;
    }

    /// <summary>
    /// Transform the value inside a successful Result<T>.
    /// </summary>
    public Result<TOut> Map<TOut>(
        Func<T, TOut> map)
    {
        if (IsFailure)
            return Result.Failure<TOut>(Error!);

        return Result.Success(map(Value!));
    }

    /// <summary>
    /// Chain an operation that itself returns a Result.
    /// </summary>
    public Result<TOut> Bind<TOut>(
        Func<T, Result<TOut>> bind)
    {
        if (IsFailure)
            return Result.Failure<TOut>(Error!);

        return bind(Value!);
    }

    // Synchronous
    public TOut Match<TOut>(
        Func<T, TOut> onSuccess,
        Func<Exception, TOut> onFailure)
    {
        return IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error!);
    }
}
namespace Kernel.Common;

public static class ResultExtensions
{
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> map)
    {
        var result = await resultTask;
        return result.Map(map);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> bind)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result.Failure<TOut>(result.Error!);

        return await bind(result.Value!);
    }

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Result<T> result,
        Func<T, Task<TOut>> onSuccess,
        Func<Exception, Task<TOut>> onFailure)
    {
        return result.IsSuccess
            ? await onSuccess(result.Value!)
            : await onFailure(result.Error!);
    }

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Task<Result<T>> resultTask,
        Func<T, TOut> onSuccess,
        Func<Exception, TOut> onFailure)
    {
        var result = await resultTask;
        return result.IsSuccess
            ? onSuccess(result.Value!)
            : onFailure(result.Error!);
    }
}
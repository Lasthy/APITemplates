using Kernel.Common;
using Kernel.Testing.Assertions;
using Kernel.Validation;
using Xunit;

namespace Kernel.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        result.ShouldBeSuccess();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Success_WithMessage_ShouldIncludeMessage()
    {
        var result = Result.Success("Item created successfully");

        result.ShouldBeSuccess();
        Assert.Single(result.Messages);
        Assert.Equal("Item created successfully", result.Messages[0]);
    }

    [Fact]
    public void Failure_WithException_ShouldCreateFailureResult()
    {
        var ex = new InvalidOperationException("Something went wrong");
        var result = Result.Failure(ex);

        result.ShouldBeFailure<InvalidOperationException>();
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public void Failure_WithMessage_ShouldCreateFailureResultWithException()
    {
        var result = Result.Failure(new InvalidOperationException("Invalid request"));

        result.ShouldBeFailure();
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("Invalid request", result.Error.Message);
    }

    [Fact]
    public void GenericSuccess_ShouldContainValue()
    {
        var result = Result.Success(42);

        var value = result.ShouldBeSuccess();
        Assert.Equal(42, value);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailure_ShouldContainErrorAndNoValue()
    {
        var ex = new ArgumentException("Bad argument");
        var result = Result.Failure<string>(ex);

        result.ShouldBeFailure<ArgumentException>();
        Assert.Null(result.Value);
    }

    [Fact]
    public void Map_OnSuccess_ShouldTransformValue()
    {
        var result = Result.Success(10);
        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(20, mapped.Value);
    }

    [Fact]
    public void Map_OnFailure_ShouldPropagateError()
    {
        var ex = new InvalidOperationException("Fail");
        var result = Result.Failure<int>(ex);
        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsFailure);
        Assert.Same(ex, mapped.Error);
    }

    [Fact]
    public void Bind_OnSuccess_ShouldChainResult()
    {
        var result = Result.Success<string>("hello");
        var bound = result.Bind(s => Result.Success(s.Length));

        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public void Bind_OnFailure_ShouldNotInvokeBinder()
    {
        var ex = new InvalidOperationException("Fail");
        var result = Result.Failure<string>(ex);
        var invoked = false;

        var bound = result.Bind(s =>
        {
            invoked = true;
            return Result.Success(s.Length);
        });

        Assert.False(invoked);
        Assert.True(bound.IsFailure);
    }

    [Fact]
    public void Match_ShouldInvokeAppropriateDelegate()
    {
        var successResult = Result.Success<int>(100);
        var successMatched = successResult.Match(
            onSuccess: val => $"Value is {val}",
            onFailure: err => $"Error: {err.Message}");

        Assert.Equal("Value is 100", successMatched);

        var failureResult = Result.Failure<int>(new Exception("Network error"));
        var failureMatched = failureResult.Match(
            onSuccess: val => $"Value is {val}",
            onFailure: err => $"Error: {err.Message}");

        Assert.Equal("Error: Network error", failureMatched);
    }

    [Fact]
    public async Task MapAsync_ShouldTransformValueAsynchronously()
    {
        var resultTask = Task.FromResult(Result.Success<int>(5));
        var mapped = await resultTask.MapAsync(val => val * 10);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(50, mapped.Value);
    }

    [Fact]
    public async Task BindAsync_ShouldChainResultAsynchronously()
    {
        var resultTask = Task.FromResult(Result.Success<string>("test"));
        var bound = await resultTask.BindAsync(str => Task.FromResult(Result.Success<string>(str.ToUpperInvariant())));

        Assert.True(bound.IsSuccess);
        Assert.Equal("TEST", bound.Value);
    }

    [Fact]
    public void ShouldHaveValidationError_ShouldAssertSpecificField()
    {
        var validationEx = new ValidationException(new[]
        {
            new ValidationError("Email", "Email is required"),
            new ValidationError("Password", "Password is too short")
        });

        var result = Result.Failure(validationEx);
        result.ShouldHaveValidationError("Email");
        result.ShouldHaveValidationError("Password");
    }
}

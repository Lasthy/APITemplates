using System.Reflection;
using Identity.Features.Auth;
using Identity.Features.Users;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Tests;

public class TransactionAttributeTests
{
    [Fact]
    public void TransactionAttribute_DefaultConstructor_SetsOrderToTwo()
    {
        var attr = new TransactionAttribute();
        Assert.Equal(2, attr.Order);
    }

    [Theory]
    [InlineData(typeof(CreateUserHandler))]
    [InlineData(typeof(BanUserHandler))]
    [InlineData(typeof(CreateDummyUserHandler))]
    [InlineData(typeof(PurgeExpiredDummyUsersHandler))]
    [InlineData(typeof(UpdateUserHandler))]
    [InlineData(typeof(ChangePasswordHandler))]
    [InlineData(typeof(ResetPasswordHandler))]
    public void MultiOperationHandlers_HaveTransactionAttribute_WithOrderTwo(Type handlerType)
    {
        var attr = handlerType.GetCustomAttribute<TransactionAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(2, attr.Order);

        var loggingAttr = handlerType.GetCustomAttribute<LoggingAttribute>();
        Assert.NotNull(loggingAttr);
        Assert.Equal(1, loggingAttr.Order);

        // Verify Logging runs before Transaction (lower Order runs first)
        Assert.True(loggingAttr.Order < attr.Order);
    }

    [Fact]
    public async Task TransactionInterceptor_WhenNextSucceeds_ReturnsSuccess()
    {
        var interceptor = new TransactionInterceptor(NullLogger<TransactionInterceptor>.Instance);
        var dummyRequest = new DummyRequest();

        var result = await interceptor.InterceptAsync(
            dummyRequest,
            () => Task.FromResult(Result.Success<string>("ok")));

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task TransactionInterceptor_WhenNextFails_ReturnsFailure()
    {
        var interceptor = new TransactionInterceptor(NullLogger<TransactionInterceptor>.Instance);
        var dummyRequest = new DummyRequest();

        var result = await interceptor.InterceptAsync(
            dummyRequest,
            () => Task.FromResult(Result.Failure<string>(new InvalidOperationException("Failed"))));

        Assert.True(result.IsFailure);
        Assert.Equal("Failed", result.Error?.Message);
    }

    [Fact]
    public async Task TransactionInterceptor_WhenNextThrows_CatchesAndReturnsFailure()
    {
        var interceptor = new TransactionInterceptor(NullLogger<TransactionInterceptor>.Instance);
        var dummyRequest = new DummyRequest();

        var result = await interceptor.InterceptAsync<string>(
            dummyRequest,
            () => throw new InvalidOperationException("Unhandled exception"));

        Assert.True(result.IsFailure);
        Assert.Equal("Unhandled exception", result.Error?.Message);
    }

    private record DummyRequest : IRequest<string>;
}

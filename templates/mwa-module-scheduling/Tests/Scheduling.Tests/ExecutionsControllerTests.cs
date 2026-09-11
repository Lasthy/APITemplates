using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Contracts.Dtos;
using Scheduling.Controllers;
using Scheduling.Features.Executions;
using Xunit;

namespace Scheduling.Tests;

public class ExecutionsControllerTests
{
    private class FakeDispatcher : IDispatcher
    {
        public Task<Result<TResponse>> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetExecutions)
            {
                var paged = PaginatedResult<JobExecutionDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            if (request is GetExecution ge)
            {
                if (ge.Id == DefaultId.Empty)
                {
                    return Task.FromResult(Result.Failure<TResponse>(new KeyNotFoundException("Execution not found.")));
                }

                var dto = new JobExecutionDetailDto(
                    ge.Id, DefaultId.NewUlid(), "Succeeded", "SampleHandler",
                    null, "Done", null, null, false, "inst-1", DateTime.UtcNow, DateTime.UtcNow, 150);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetJobExecutions)
            {
                var paged = PaginatedResult<JobExecutionDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            throw new NotImplementedException();
        }
    }

    private readonly FakeDispatcher _dispatcher = new();
    private readonly ExecutionsController _controller;

    public ExecutionsControllerTests()
    {
        _controller = new ExecutionsController(_dispatcher);
    }

    [Fact]
    public async Task GetList_ReturnsOkWithPaginatedResult()
    {
        var result = await _controller.GetList(null, null, null, null, 1, 20, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        var executionId = DefaultId.NewUlid();
        var result = await _controller.GetById(executionId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var execution = Assert.IsType<JobExecutionDetailDto>(okResult.Value);
        Assert.Equal(executionId, execution.Id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.GetById(DefaultId.Empty, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByJobId_ReturnsOkWithJobExecutions()
    {
        var jobId = DefaultId.NewUlid();
        var result = await _controller.GetByJobId(jobId, 1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}

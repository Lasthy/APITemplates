using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Contracts.Dtos;
using Scheduling.Controllers;
using Scheduling.Features.Jobs;
using Xunit;
using static Scheduling.Controllers.JobsController;

namespace Scheduling.Tests;

public class JobsControllerTests
{
    private class FakeDispatcher : IDispatcher
    {
        public DefaultId LastRequestedJobId { get; set; }

        public Task<Result<TResponse>> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CreateJob cj)
            {
                var dto = new ScheduledJobDetailDto(
                    DefaultId.NewUlid(), cj.Name, cj.Description, cj.HandlerType, cj.CronExpression,
                    cj.ScheduledAt, cj.PayloadJson, "Enabled", DateTime.UtcNow.AddHours(1),
                    null, null, 0, 0, 0, cj.TimeoutSeconds, cj.Priority, false, DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetJob gj)
            {
                LastRequestedJobId = gj.Id;
                if (gj.Id == DefaultId.Empty)
                {
                    return Task.FromResult(Result.Failure<TResponse>(new KeyNotFoundException("Job not found.")));
                }

                var dto = new ScheduledJobDetailDto(
                    gj.Id, "Sample Job", null, "SampleHandler", "0 0 * * *",
                    null, null, "Enabled", DateTime.UtcNow.AddDays(1),
                    null, null, 0, 0, 0, null, 100, false, DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetJobs)
            {
                var paged = PaginatedResult<ScheduledJobDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            if (request is UpdateJob uj)
            {
                var dto = new ScheduledJobDetailDto(
                    uj.Id, uj.Name ?? "Updated Job", uj.Description, "SampleHandler", uj.CronExpression ?? "0 0 * * *",
                    null, uj.PayloadJson, "Enabled", DateTime.UtcNow.AddDays(1),
                    null, null, 0, 0, 0, uj.TimeoutSeconds, uj.Priority ?? 100, false, DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is DeleteJob)
            {
                return Task.FromResult(Result.Success((TResponse)(object)true));
            }

            if (request is EnableJob)
            {
                return Task.FromResult(Result.Success((TResponse)(object)true));
            }

            if (request is DisableJob)
            {
                return Task.FromResult(Result.Success((TResponse)(object)true));
            }

            if (request is TriggerJob tj)
            {
                var executionDto = new JobExecutionDetailDto(
                    DefaultId.NewUlid(), tj.Id, "Succeeded", "SampleHandler",
                    null, "Done", null, null, true, "test-inst", DateTime.UtcNow, DateTime.UtcNow, 42);
                return Task.FromResult(Result.Success((TResponse)(object)executionDto));
            }

            if (request is GetJobNextOccurrences)
            {
                var list = (IReadOnlyList<DateTime>)new List<DateTime> { DateTime.UtcNow.AddDays(1) };
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            throw new NotImplementedException();
        }
    }

    private readonly FakeDispatcher _dispatcher = new();
    private readonly JobsController _controller;

    public JobsControllerTests()
    {
        _controller = new JobsController(_dispatcher);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var request = new CreateJobRequest("Test Job", "TestHandler", "0 0 * * *");
        var result = await _controller.Create(request, CancellationToken.None);

        var actionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var job = Assert.IsType<ScheduledJobDetailDto>(actionResult.Value);
        Assert.Equal("Test Job", job.Name);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        var jobId = DefaultId.NewUlid();
        var result = await _controller.GetById(jobId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var job = Assert.IsType<ScheduledJobDetailDto>(okResult.Value);
        Assert.Equal(jobId, job.Id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.GetById(DefaultId.Empty, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsOkWithUpdatedDto()
    {
        var jobId = DefaultId.NewUlid();
        var request = new UpdateJobRequest(Name: "Renamed Job");
        var result = await _controller.Update(jobId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var job = Assert.IsType<ScheduledJobDetailDto>(okResult.Value);
        Assert.Equal("Renamed Job", job.Name);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var jobId = DefaultId.NewUlid();
        var result = await _controller.Delete(jobId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task EnableAndDisable_ReturnOk()
    {
        var jobId = DefaultId.NewUlid();
        var enableResult = await _controller.Enable(jobId, CancellationToken.None);
        Assert.IsType<OkObjectResult>(enableResult);

        var disableResult = await _controller.Disable(jobId, CancellationToken.None);
        Assert.IsType<OkObjectResult>(disableResult);
    }

    [Fact]
    public async Task Trigger_ReturnsOkWithExecutionDetail()
    {
        var jobId = DefaultId.NewUlid();
        var result = await _controller.Trigger(jobId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var execution = Assert.IsType<JobExecutionDetailDto>(okResult.Value);
        Assert.Equal("Succeeded", execution.Status);
        Assert.True(execution.IsManualTrigger);
    }

    [Fact]
    public async Task GetNext_ReturnsOkWithOccurrences()
    {
        var jobId = DefaultId.NewUlid();
        var result = await _controller.GetNext(jobId, 5, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<DateTime>>(okResult.Value);
        Assert.NotEmpty(list);
    }
}

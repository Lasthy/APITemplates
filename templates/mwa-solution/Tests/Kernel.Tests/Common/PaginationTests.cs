using Kernel.Common;
using Kernel.Testing.Assertions;
using Xunit;

namespace Kernel.Tests.Common;

public class PaginationTests
{
    private record SampleQuery : PaginatedQuery
    {
        public override int MaxPageSize => 50;
    }

    [Fact]
    public void PaginatedResult_Create_ShouldCalculateMetadataCorrectly()
    {
        var items = new[] { "A", "B", "C" };
        var result = PaginatedResult<string>.Create(items, totalCount: 25, page: 2, pageSize: 10);

        result.ShouldBeSuccess();
        result.ShouldHavePage(expectedPage: 2, expectedPageSize: 10, expectedTotalCount: 25);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
        Assert.Equal(3, result.Items.Count());
    }

    [Fact]
    public void PaginatedResult_FirstPage_ShouldNotHavePreviousPage()
    {
        var items = new[] { "A" };
        var result = PaginatedResult<string>.Create(items, totalCount: 10, page: 1, pageSize: 5);

        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_LastPage_ShouldNotHaveNextPage()
    {
        var items = new[] { "A" };
        var result = PaginatedResult<string>.Create(items, totalCount: 10, page: 2, pageSize: 5);

        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_Empty_ShouldHaveZeroTotalCountAndPages()
    {
        var result = PaginatedResult<int>.Empty(page: 1, pageSize: 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_Failure_ShouldSetErrorAndEmptyItems()
    {
        var ex = new InvalidOperationException("Db query failed");
        var result = PaginatedResult<string>.Failure(ex);

        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void PaginatedQuery_Normalization_ShouldClampToValidRanges()
    {
        var query = new SampleQuery
        {
            Page = -5,
            PageSize = 200
        };

        Assert.Equal(1, query.GetNormalizedPage());
        Assert.Equal(50, query.GetNormalizedPageSize()); // MaxPageSize is 50

        var queryZero = new SampleQuery
        {
            Page = 0,
            PageSize = 0
        };

        Assert.Equal(1, queryZero.GetNormalizedPage());
        Assert.Equal(20, queryZero.GetNormalizedPageSize());
    }
}

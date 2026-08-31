using Catalog.Entities;
using Catalog.Persistence;
using Kernel.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Shopping.Tests;

public class PaginationExtensionsTests
{
    #region Test Models

    public class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public TestCategory? Category { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TestCategory
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public record CustomPagedQuery : PaginatedQuery
    {
        public override int MaxPageSize => 50;
    }

    private static List<TestItem> CreateSampleItems(int count = 100)
    {
        var items = new List<TestItem>();
        for (int i = 1; i <= count; i++)
        {
            items.Add(new TestItem
            {
                Id = i,
                Name = $"Item {i:D3}",
                Price = count - i + 1,
                Category = new TestCategory
                {
                    Id = (i % 5) + 1,
                    Title = $"Category {(i % 5) + 1:D2}"
                },
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)
            });
        }
        return items;
    }

    #endregion

    #region PaginatedQuery Tests

    [Fact]
    public void PaginatedQuery_DefaultValues_ShouldMatchSpecification()
    {
        var query = new PaginatedQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Null(query.SortBy);
        Assert.Equal(SortDirection.Ascending, query.SortDirection);
        Assert.Equal(100, query.MaxPageSize);
        Assert.Equal(1, query.GetNormalizedPage());
        Assert.Equal(20, query.GetNormalizedPageSize());
    }

    [Fact]
    public void PaginatedQuery_Normalization_ShouldClampNegativeValues()
    {
        var query = new PaginatedQuery
        {
            Page = -5,
            PageSize = -10
        };

        Assert.Equal(1, query.GetNormalizedPage());
        Assert.Equal(20, query.GetNormalizedPageSize());
    }

    [Fact]
    public void PaginatedQuery_Normalization_ShouldClampExceedingPageSize()
    {
        var query = new PaginatedQuery
        {
            Page = 2,
            PageSize = 250
        };

        Assert.Equal(2, query.GetNormalizedPage());
        Assert.Equal(100, query.GetNormalizedPageSize());
    }

    [Fact]
    public void PaginatedQuery_CustomMaxPageSize_ShouldBeRespected()
    {
        var query = new CustomPagedQuery
        {
            PageSize = 75
        };

        Assert.Equal(50, query.MaxPageSize);
        Assert.Equal(50, query.GetNormalizedPageSize());
    }

    #endregion

    #region PaginatedResult Tests

    [Fact]
    public void PaginatedResult_Create_ShouldCalculatePaginationMetadataCorrectly()
    {
        var items = new[] { "A", "B", "C" };
        var pagedResult = PaginatedResult<string>.Create(items, page: 2, pageSize: 10, totalCount: 25);

        Assert.True(pagedResult.IsSuccess);
        Assert.False(pagedResult.IsFailure);
        Assert.Equal(2, pagedResult.Page);
        Assert.Equal(10, pagedResult.PageSize);
        Assert.Equal(25, pagedResult.TotalCount);
        Assert.Equal(3, pagedResult.TotalPages);
        Assert.True(pagedResult.HasNextPage);
        Assert.True(pagedResult.HasPreviousPage);
        Assert.Equal(3, pagedResult.Items.Count());
    }

    [Fact]
    public void PaginatedResult_FirstPage_ShouldHaveNextPageAndNoPreviousPage()
    {
        var items = new[] { 1, 2, 3 };
        var result = PaginatedResult<int>.Create(items, page: 1, pageSize: 10, totalCount: 30);

        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_LastPage_ShouldHaveNoNextPageAndHavePreviousPage()
    {
        var items = new[] { 1, 2, 3 };
        var result = PaginatedResult<int>.Create(items, page: 3, pageSize: 10, totalCount: 30);

        Assert.Equal(3, result.Page);
        Assert.Equal(3, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_SinglePage_ShouldHaveNoNextOrPreviousPage()
    {
        var items = new[] { 1, 2 };
        var result = PaginatedResult<int>.Create(items, page: 1, pageSize: 10, totalCount: 2);

        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_Empty_ShouldProduceZeroTotalCountAndPages()
    {
        var empty = PaginatedResult<string>.Empty(page: 1, pageSize: 20);

        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasNextPage);
        Assert.False(empty.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResult_Failure_ShouldReturnFailedResult()
    {
        var error = new InvalidOperationException("Failed to query database.");
        var failure = PaginatedResult<string>.Failure(error, "DB error occurred.");

        Assert.False(failure.IsSuccess);
        Assert.True(failure.IsFailure);
        Assert.Same(error, failure.Error);
        Assert.Contains("DB error occurred.", failure.Messages);
        Assert.Empty(failure.Items);
    }

    [Fact]
    public void PaginatedResult_SuccessAlias_ShouldMatchCreate()
    {
        var items = new[] { "X", "Y" };
        var result = PaginatedResult<string>.Success(items, page: 1, pageSize: 20, totalCount: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(items, result.Items);
    }

    #endregion

    #region QueryableExtensions - ApplySort Tests

    [Fact]
    public void ApplySort_Ascending_ShouldSortByProperty()
    {
        var items = CreateSampleItems(10).AsQueryable();

        var sorted = items.ApplySort("Price", SortDirection.Ascending).ToList();

        Assert.Equal(1, sorted.First().Price);
        Assert.Equal(10, sorted.Last().Price);
    }

    [Fact]
    public void ApplySort_Descending_ShouldSortByPropertyDescending()
    {
        var items = CreateSampleItems(10).AsQueryable();

        var sorted = items.ApplySort("Price", SortDirection.Descending).ToList();

        Assert.Equal(10, sorted.First().Price);
        Assert.Equal(1, sorted.Last().Price);
    }

    [Fact]
    public void ApplySort_CaseInsensitivePropertyName_ShouldMatch()
    {
        var items = CreateSampleItems(10).AsQueryable();

        var sorted = items.ApplySort("name", SortDirection.Descending).ToList();

        Assert.Equal("Item 010", sorted.First().Name);
        Assert.Equal("Item 001", sorted.Last().Name);
    }

    [Fact]
    public void ApplySort_NestedProperty_ShouldSortByNestedProperty()
    {
        var items = CreateSampleItems(10).AsQueryable();

        var sorted = items.ApplySort("Category.Title", SortDirection.Descending).ToList();

        Assert.Equal("Category 05", sorted.First().Category!.Title);
    }

    [Fact]
    public void ApplySort_NullOrEmptyOrWhitespace_ShouldReturnQueryUnchanged()
    {
        var items = CreateSampleItems(5).AsQueryable();

        var sortedNull = items.ApplySort((string?)null).ToList();
        var sortedEmpty = items.ApplySort(string.Empty).ToList();
        var sortedWhitespace = items.ApplySort("   ").ToList();

        Assert.Equal(1, sortedNull.First().Id);
        Assert.Equal(1, sortedEmpty.First().Id);
        Assert.Equal(1, sortedWhitespace.First().Id);
    }

    [Fact]
    public void ApplySort_NonExistentProperty_ShouldReturnQueryUnchanged()
    {
        var items = CreateSampleItems(5).AsQueryable();

        var sorted = items.ApplySort("NonExistentProperty").ToList();

        Assert.Equal(1, sorted.First().Id);
        Assert.Equal(5, sorted.Last().Id);
    }

    [Fact]
    public void ApplySort_ViaPaginatedQuery_ShouldApplySorting()
    {
        var items = CreateSampleItems(10).AsQueryable();
        var query = new PaginatedQuery
        {
            SortBy = "Price",
            SortDirection = SortDirection.Descending
        };

        var sorted = items.ApplySort(query).ToList();

        Assert.Equal(10, sorted.First().Price);
    }

    #endregion

    #region QueryableExtensions - ApplyPagination Tests

    [Fact]
    public void ApplyPagination_ValidPageAndPageSize_ShouldSkipAndTakeCorrectly()
    {
        var items = CreateSampleItems(50).AsQueryable();
        var query = new PaginatedQuery
        {
            Page = 2,
            PageSize = 10
        };

        var pageItems = items.ApplyPagination(query).ToList();

        Assert.Equal(10, pageItems.Count);
        Assert.Equal(11, pageItems.First().Id);
        Assert.Equal(20, pageItems.Last().Id);
    }

    [Fact]
    public void ApplyPagination_WithSortBy_ShouldSortAndPaginate()
    {
        var items = CreateSampleItems(50).AsQueryable();
        var query = new PaginatedQuery
        {
            Page = 1,
            PageSize = 5,
            SortBy = "Price",
            SortDirection = SortDirection.Ascending
        };

        var pageItems = items.ApplyPagination(query).ToList();

        Assert.Equal(5, pageItems.Count);
        Assert.Equal(1, pageItems.First().Price);
        Assert.Equal(5, pageItems.Last().Price);
    }

    [Fact]
    public void ApplyPagination_NegativePageAndPageSize_ShouldUseDefaults()
    {
        var items = CreateSampleItems(50).AsQueryable();
        var query = new PaginatedQuery
        {
            Page = -1,
            PageSize = -1
        };

        var pageItems = items.ApplyPagination(query).ToList();

        Assert.Equal(20, pageItems.Count);
        Assert.Equal(1, pageItems.First().Id);
    }

    [Fact]
    public void ApplyPagination_ExceedingMaxPageSize_ShouldClampToMaxPageSize()
    {
        var items = CreateSampleItems(150).AsQueryable();
        var query = new PaginatedQuery
        {
            Page = 1,
            PageSize = 120
        };

        var pageItems = items.ApplyPagination(query).ToList();

        Assert.Equal(100, pageItems.Count);
    }

    [Fact]
    public void ApplyPagination_ExplicitParameters_ShouldPaginate()
    {
        var items = CreateSampleItems(30).AsQueryable();

        var pageItems = items.ApplyPagination(page: 3, pageSize: 5, maxPageSize: 100).ToList();

        Assert.Equal(5, pageItems.Count);
        Assert.Equal(11, pageItems.First().Id);
        Assert.Equal(15, pageItems.Last().Id);
    }

    #endregion

    #region QueryableExtensions - ToPaginatedResult / ToPaginatedResultAsync Tests

    [Fact]
    public void ToPaginatedResult_InMemoryQueryable_ShouldReturnCompletePaginatedResult()
    {
        var items = CreateSampleItems(45).AsQueryable();
        var query = new PaginatedQuery
        {
            Page = 2,
            PageSize = 10,
            SortBy = "Id",
            SortDirection = SortDirection.Ascending
        };

        var result = items.ToPaginatedResult(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(45, result.TotalCount);
        Assert.Equal(5, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
        Assert.Equal(10, result.Items.Count());
        Assert.Equal(11, result.Items.First().Id);
        Assert.Equal(20, result.Items.Last().Id);
    }

    [Fact]
    public async Task ToPaginatedResultAsync_InMemoryQueryable_ShouldExecuteWithoutEFAsyncException()
    {
        var items = CreateSampleItems(25).AsQueryable();
        var query = new PaginatedQuery
        {
            Page = 1,
            PageSize = 10
        };

        var result = await items.ToPaginatedResultAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(10, result.Items.Count());
    }

    [Fact]
    public async Task ToPaginatedResultAsync_WithEFCoreDbContext_ShouldExecuteAsyncQueries()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaginationTest_{Guid.NewGuid()}")
            .Options;

        await using var db = new CatalogDbContext(options);

        for (int i = 1; i <= 35; i++)
        {
            db.Products.Add(new Product
            {
                Name = $"Product {i:D2}",
                Description = "Description",
                Sku = $"SKU-{i:D2}",
                Price = i * 10,
                StockQuantity = 50,
                LowStockThreshold = 10
            });
        }
        await db.SaveChangesAsync();

        var query = new PaginatedQuery
        {
            Page = 2,
            PageSize = 10,
            SortBy = "Price",
            SortDirection = SortDirection.Descending
        };

        var result = await db.Products
            .AsNoTracking()
            .ToPaginatedResultAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(35, result.TotalCount);
        Assert.Equal(4, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
        Assert.Equal(10, result.Items.Count());
        Assert.Equal("Product 25", result.Items.First().Name);
    }

    #endregion

    #region IEnumerable Extensions Tests

    [Fact]
    public void IEnumerable_ApplySortAndPagination_ShouldWorkCorrectly()
    {
        IEnumerable<TestItem> items = CreateSampleItems(30);

        var sorted = items.ApplySort("Price", SortDirection.Ascending).ToList();
        Assert.Equal(1, sorted.First().Price);

        var paginated = items.ApplyPagination(page: 2, pageSize: 5).ToList();
        Assert.Equal(5, paginated.Count);
        Assert.Equal(6, paginated.First().Id);

        var query = new PaginatedQuery
        {
            Page = 3,
            PageSize = 5,
            SortBy = "Id",
            SortDirection = SortDirection.Descending
        };

        var pagedResult = items.ToPaginatedResult(query);
        Assert.Equal(3, pagedResult.Page);
        Assert.Equal(5, pagedResult.PageSize);
        Assert.Equal(30, pagedResult.TotalCount);
        Assert.Equal(6, pagedResult.TotalPages);
        Assert.Equal(5, pagedResult.Items.Count());
        Assert.Equal(20, pagedResult.Items.First().Id);
    }

    #endregion
}

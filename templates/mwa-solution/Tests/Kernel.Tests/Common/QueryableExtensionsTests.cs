using Kernel.Common;
using Xunit;

namespace Kernel.Tests.Common;

public class QueryableExtensionsTests
{
    private class Category
    {
        public string Title { get; set; } = string.Empty;
    }

    private class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public Category Category { get; set; } = new();
    }

    private readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = "Banana", Price = 1.50m, Category = new Category { Title = "Fruit" } },
        new Product { Id = 2, Name = "Apple", Price = 2.00m, Category = new Category { Title = "Fruit" } },
        new Product { Id = 3, Name = "Carrot", Price = 0.90m, Category = new Category { Title = "Vegetable" } },
        new Product { Id = 4, Name = "Steak", Price = 15.00m, Category = new Category { Title = "Meat" } },
    };

    [Fact]
    public void ApplySort_Ascending_ShouldSortByProperty()
    {
        var sorted = _products.AsQueryable().ApplySort("Name", SortDirection.Ascending).ToList();

        Assert.Equal("Apple", sorted[0].Name);
        Assert.Equal("Banana", sorted[1].Name);
        Assert.Equal("Carrot", sorted[2].Name);
        Assert.Equal("Steak", sorted[3].Name);
    }

    [Fact]
    public void ApplySort_Descending_ShouldSortByProperty()
    {
        var sorted = _products.AsQueryable().ApplySort("Price", SortDirection.Descending).ToList();

        Assert.Equal(15.00m, sorted[0].Price);
        Assert.Equal(2.00m, sorted[1].Price);
        Assert.Equal(1.50m, sorted[2].Price);
        Assert.Equal(0.90m, sorted[3].Price);
    }

    [Fact]
    public void ApplySort_NestedProperty_ShouldSortByNestedProperty()
    {
        var sorted = _products.AsQueryable().ApplySort("Category.Title", SortDirection.Ascending).ToList();

        // "Fruit", "Fruit", "Meat", "Vegetable"
        Assert.Equal("Fruit", sorted[0].Category.Title);
        Assert.Equal("Vegetable", sorted.Last().Category.Title);
    }

    [Fact]
    public void ApplySort_NullOrEmptySortBy_ShouldReturnUnmodifiedQueryable()
    {
        var sorted = _products.AsQueryable().ApplySort(null, SortDirection.Ascending).ToList();
        Assert.Equal(1, sorted[0].Id);
    }

    [Fact]
    public void ApplyPagination_ShouldSkipAndTakeCorrectly()
    {
        var page = _products.AsQueryable().ApplyPagination(page: 2, pageSize: 2, maxPageSize: 10).ToList();

        Assert.Equal(2, page.Count);
        Assert.Equal(3, page[0].Id);
        Assert.Equal(4, page[1].Id);
    }

    private record ProductQuery : PaginatedQuery;

    [Fact]
    public async Task ToPaginatedResultAsync_ShouldReturnPaginatedResult()
    {
        var query = new ProductQuery
        {
            Page = 1,
            PageSize = 2,
            SortBy = "Name",
            SortDirection = SortDirection.Ascending
        };

        var result = await _products.AsQueryable().ToPaginatedResultAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count());
        Assert.Equal("Apple", result.Items.First().Name);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }
}

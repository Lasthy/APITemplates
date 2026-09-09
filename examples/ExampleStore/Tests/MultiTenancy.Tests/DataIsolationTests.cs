using Kernel.Entities;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Entities;
using MultiTenancy.Persistence;
using MultiTenancy.Services;
using Xunit;

namespace MultiTenancy.Tests;

public class DataIsolationTests
{
    public class TestProduct : ITenantScoped, IAuditable
    {
        public DefaultId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DefaultId TenantId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DefaultId? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DefaultId? UpdatedBy { get; set; }
    }

    public class TestModuleDbContext : DbContext
    {
        public DbSet<TestProduct> Products { get; set; } = null!;

        private readonly ITenantContext _tenantContext;
        private readonly TenantSaveChangesInterceptor _interceptor;

        public TestModuleDbContext(
            DbContextOptions<TestModuleDbContext> options,
            ITenantContext tenantContext,
            TenantSaveChangesInterceptor interceptor)
            : base(options)
        {
            _tenantContext = tenantContext;
            _interceptor = interceptor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.AddInterceptors(_interceptor);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<TestProduct>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.ApplyTenantFilters(this, _tenantContext);
        }
    }

    private readonly DefaultId _tenantA = DefaultId.NewUlid();
    private readonly DefaultId _tenantB = DefaultId.NewUlid();
    private readonly TenantContext _tenantContext;
    private readonly FakeScopeContext _scopeContext;
    private readonly TenantSaveChangesInterceptor _interceptor;
    private readonly DbContextOptions<TestModuleDbContext> _dbOptions;

    public DataIsolationTests()
    {
        _dbOptions = new DbContextOptionsBuilder<TestModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _scopeContext = new FakeScopeContext().AsAuthenticated();
        _tenantContext = new TenantContext(new EmptyServiceProvider(), _scopeContext);
        _interceptor = new TenantSaveChangesInterceptor(_tenantContext, _scopeContext);
    }

    [Fact]
    public async Task QueryFilter_IsolatesDataBetweenTenants()
    {
        // 1. Seed data as Tenant A
        _tenantContext.SetTenantId(_tenantA);
        using (var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor))
        {
            db.Products.Add(new TestProduct { Id = DefaultId.NewUlid(), Name = "Product A1" });
            db.Products.Add(new TestProduct { Id = DefaultId.NewUlid(), Name = "Product A2" });
            await db.SaveChangesAsync();
        }

        // 2. Seed data as Tenant B
        _tenantContext.SetTenantId(_tenantB);
        using (var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor))
        {
            db.Products.Add(new TestProduct { Id = DefaultId.NewUlid(), Name = "Product B1" });
            await db.SaveChangesAsync();
        }

        // 3. Query as Tenant A -> only Product A1 and A2 visible
        _tenantContext.SetTenantId(_tenantA);
        using (var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor))
        {
            var productsA = await db.Products.ToListAsync();
            Assert.Equal(2, productsA.Count);
            Assert.All(productsA, p => Assert.Equal(_tenantA, p.TenantId));
        }

        // 4. Query as Tenant B -> only Product B1 visible
        _tenantContext.SetTenantId(_tenantB);
        using (var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor))
        {
            var productsB = await db.Products.ToListAsync();
            Assert.Single(productsB);
            Assert.Equal("Product B1", productsB[0].Name);
        }

        // 5. Query as System user (bypass active) -> all 3 products visible
        TenantContext.SetBypassFilter(true);
        try
        {
            using var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor);
            var allProducts = await db.Products.ToListAsync();
            Assert.Equal(3, allProducts.Count);
        }
        finally
        {
            TenantContext.SetBypassFilter(false);
        }
    }

    [Fact]
    public async Task SaveChangesInterceptor_AutomaticallyStampsCurrentTenantId()
    {
        _tenantContext.SetTenantId(_tenantA);
        using var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor);

        var product = new TestProduct { Id = DefaultId.NewUlid(), Name = "Auto Stamped" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        Assert.Equal(_tenantA, product.TenantId);
    }

    [Fact]
    public async Task SaveChangesInterceptor_ThrowsWhenNoTenantActive()
    {
        _tenantContext.SetTenantId(null);
        using var db = new TestModuleDbContext(_dbOptions, _tenantContext, _interceptor);

        var product = new TestProduct { Id = DefaultId.NewUlid(), Name = "No Tenant" };
        db.Products.Add(product);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("without a valid TenantId when no tenant is active", ex.Message);
    }

    private class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}

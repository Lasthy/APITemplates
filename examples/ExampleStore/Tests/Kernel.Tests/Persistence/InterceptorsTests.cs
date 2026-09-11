using Kernel.Entities;
using Kernel.Events;
using Kernel.Persistence;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kernel.Tests.Persistence;

public class InterceptorsTests
{
    private record EntityCreatedDomainEvent(DefaultId EntityId) : Event;

    private class TestAuditableEntity : IAuditable, ISoftDeletable, IHasDomainEvents
    {
        public DefaultId Id { get; set; } = DefaultId.NewUlid();
        public string Name { get; set; } = string.Empty;

        // IAuditable
        public DateTime CreatedAt { get; set; }
        public DefaultId? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DefaultId? UpdatedBy { get; set; }

        // ISoftDeletable
        public DateTime? DeletedAt { get; set; }
        public DefaultId? DeletedBy { get; set; }

        // IHasDomainEvents
        private readonly List<Event> _domainEvents = new();
        public IReadOnlyCollection<Event> DomainEvents => _domainEvents.AsReadOnly();
        public void AddDomainEvent(Event domainEvent) => _domainEvents.Add(domainEvent);
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestAuditableEntity> Entities => Set<TestAuditableEntity>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestAuditableEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<TestAuditableEntity>().Ignore(e => e.DomainEvents);
        }
    }

    [Fact]
    public async Task AuditSaveChangesInterceptor_OnAdd_ShouldSetCreatedAtAndCreatedBy()
    {
        var userId = DefaultId.NewUlid();
        var fakeScope = new FakeScopeContext().AsAuthenticated(userId);
        var auditInterceptor = new AuditSaveChangesInterceptor(fakeScope);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(auditInterceptor)
            .Options;

        using var db = new TestDbContext(options);
        var entity = new TestAuditableEntity { Name = "New Item" };

        db.Entities.Add(entity);
        await db.SaveChangesAsync();

        Assert.NotEqual(default, entity.CreatedAt);
        Assert.Equal(userId, entity.CreatedBy);
        Assert.Null(entity.UpdatedAt);
    }

    [Fact]
    public async Task AuditSaveChangesInterceptor_OnUpdate_ShouldSetUpdatedAtAndUpdatedBy()
    {
        var initialUser = DefaultId.NewUlid();
        var updateUser = DefaultId.NewUlid();
        var fakeScope = new FakeScopeContext().AsAuthenticated(initialUser);
        var auditInterceptor = new AuditSaveChangesInterceptor(fakeScope);

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(auditInterceptor)
            .Options;

        DefaultId entityId;
        using (var db = new TestDbContext(options))
        {
            var entity = new TestAuditableEntity { Name = "Original" };
            db.Entities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
        }

        // Switch user
        fakeScope.AsAuthenticated(updateUser);

        using (var db = new TestDbContext(options))
        {
            var entity = await db.Entities.FindAsync(entityId);
            Assert.NotNull(entity);
            entity.Name = "Updated Name";
            await db.SaveChangesAsync();

            Assert.NotNull(entity.UpdatedAt);
            Assert.Equal(updateUser, entity.UpdatedBy);
        }
    }

    [Fact]
    public async Task SoftDeleteSaveChangesInterceptor_ShouldConvertDeleteToModifiedWithDeletedAt()
    {
        var userId = DefaultId.NewUlid();
        var fakeScope = new FakeScopeContext().AsAuthenticated(userId);
        var softDeleteInterceptor = new SoftDeleteSaveChangesInterceptor(fakeScope);

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(softDeleteInterceptor)
            .Options;

        DefaultId entityId;
        using (var db = new TestDbContext(options))
        {
            var entity = new TestAuditableEntity { Name = "To Delete" };
            db.Entities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
        }

        using (var db = new TestDbContext(options))
        {
            var entity = await db.Entities.FindAsync(entityId);
            Assert.NotNull(entity);

            db.Entities.Remove(entity);
            await db.SaveChangesAsync();

            Assert.NotNull(entity.DeletedAt);
            Assert.Equal(userId, entity.DeletedBy);
        }

        // Entity still exists in database, but marked deleted
        using (var db = new TestDbContext(options))
        {
            var entity = await db.Entities.FindAsync(entityId);
            Assert.NotNull(entity);
            Assert.NotNull(entity.DeletedAt);
        }
    }

    [Fact]
    public async Task DomainEventsSaveChangesInterceptor_ShouldDispatchAndClearDomainEvents()
    {
        var fakeEventBus = new FakeEventBus();
        var interceptor = new DispatchDomainEventsSaveChangesInterceptor(fakeEventBus);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        using var db = new TestDbContext(options);
        var entity = new TestAuditableEntity { Name = "Domain Event Test" };
        var domainEvent = new EntityCreatedDomainEvent(entity.Id);
        entity.AddDomainEvent(domainEvent);

        db.Entities.Add(entity);
        await db.SaveChangesAsync();

        // Event dispatched to event bus
        var published = fakeEventBus.GetPublishedEvents<EntityCreatedDomainEvent>();
        Assert.Single(published);
        Assert.Equal(entity.Id, published[0].EntityId);

        // Domain events collection cleared on entity
        Assert.Empty(entity.DomainEvents);
    }
}

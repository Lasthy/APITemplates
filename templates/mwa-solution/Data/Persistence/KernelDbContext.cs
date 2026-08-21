using Kernel.Boxing;
using Kernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Data.Persistence;

public class KernelDbContext : DbContext, IKernelDbContext
{
    public KernelDbContext(DbContextOptions<KernelDbContext> options) : base(options)
    {
    }

    public DbSet<Message> Messages { get; set; } = null!;
}

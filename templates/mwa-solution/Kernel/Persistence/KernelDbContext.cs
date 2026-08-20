using Kernel.Boxing;
using Microsoft.EntityFrameworkCore;

namespace Kernel.Persistence;

public class KernelDbContext : DbContext
{
	public KernelDbContext(DbContextOptions<KernelDbContext> options) : base(options)
	{
	}

	public DbSet<Message> Messages { get; set; } = null!;
}

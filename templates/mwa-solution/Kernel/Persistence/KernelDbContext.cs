using Kernel.Boxing;
using Microsoft.EntityFrameworkCore;

namespace Kernel.Persistence;

public class KernelDbContext : DbContext
{
	public DbSet<Message> Messages { get; set; } = null!;
	
	
}

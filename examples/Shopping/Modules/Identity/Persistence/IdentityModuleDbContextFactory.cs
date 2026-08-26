using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Persistence;

/// <summary>
/// Design-time factory for <see cref="IdentityModuleDbContext"/> to enable EF Core migration commands.
/// </summary>
public class IdentityModuleDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityModuleDbContext>();
        optionsBuilder.UseSqlite("Data Source=identity.db", x =>
        {
            x.MigrationsHistoryTable("__EFMigrationsHistory_identity");
        });

        return new IdentityModuleDbContext(optionsBuilder.Options);
    }
}

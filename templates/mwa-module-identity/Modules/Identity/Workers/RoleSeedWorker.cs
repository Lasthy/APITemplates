using Identity.Configuration;
using Identity.Contracts.Constants;
using Identity.Entities;
using Identity.Profiles;
using Identity.Services;
using Kernel.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Workers;

/// <summary>
/// One-shot startup worker that ensures all profile-declared roles exist in the
/// Identity database and optionally seeds a default admin user.
/// </summary>
public class RoleSeedWorker : BaseWorker
{
    private readonly ILogger<RoleSeedWorker> _logger;

    public RoleSeedWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RoleSeedWorker> logger)
        : base(scopeFactory, logger, isOneShot: true)
    {
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken ct)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var profileRegistry = services.GetRequiredService<ProfileRegistry>();
        var identitySettings = services.GetRequiredService<IdentitySettings>();

        // Ensure all profile-declared roles exist
        var declaredRoles = profileRegistry.GetAllDeclaredRoles();
        foreach (var roleName in declaredRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Created role '{RoleName}'", roleName);
                }
                else
                {
                    _logger.LogWarning("Failed to create role '{RoleName}': {Errors}",
                        roleName, string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // Seed default admin user if configured and no admin exists
        if (!string.IsNullOrWhiteSpace(identitySettings.SeedAdminEmail))
        {
            var envName = identitySettings.EnvironmentName ??
                          Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                          Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                          "Production";

            var isDevelopment = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(envName, "Test", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(envName, "Testing", StringComparison.OrdinalIgnoreCase);

            if (!isDevelopment && identitySettings.IsDefaultSeedAdminPassword)
            {
                throw new InvalidOperationException(
                    "In production environments, default seed admin credentials ('Admin123!') cannot be used. Specify a strong SeedAdminPassword via appsettings.Identity.json or IDENTITY__SEEDADMINPASSWORD, or set SeedAdminEmail to empty to disable automatic admin seeding.");
            }

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var existingAdmin = await userManager.FindByEmailAsync(identitySettings.SeedAdminEmail);

            if (existingAdmin == null)
            {
                var userService = services.GetRequiredService<IUserService>();
                var result = await userService.CreateUserAsync(
                    identitySettings.SeedAdminEmail,
                    "admin",
                    identitySettings.SeedAdminPassword,
                    IdentityProfiles.System,
                    ct);

                if (result.IsSuccess)
                {
                    if (identitySettings.IsDefaultSeedAdminPassword)
                    {
                        _logger.LogWarning("Seeded admin user '{Email}' with default development credentials. Change this password before deploying to production.", identitySettings.SeedAdminEmail);
                    }
                    else
                    {
                        _logger.LogInformation("Seeded admin user '{Email}'", identitySettings.SeedAdminEmail);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to seed admin user: {Error}", result.Error?.Message);
                }
            }
        }
    }
}

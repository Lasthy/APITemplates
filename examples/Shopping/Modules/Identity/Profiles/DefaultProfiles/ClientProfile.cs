using Identity.Contracts.Constants;
using Identity.Entities;
using Identity.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Public client/customer profile.
/// Assigned to users who self-register via the public registration endpoint.
/// </summary>
public class ClientProfile : IProfile
{
    public static string Name => IdentityProfiles.Client;
    public static string Description => "Customer profile for browsing products, cart management, and purchasing.";
    public static bool IsPublic => true;
    public static IReadOnlyList<string> Roles => [IdentityRoles.Client];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;

    public static async Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
    {
        var dbContext = context.Services.GetRequiredService<IdentityModuleDbContext>();

        var client = new Client
        {
            Id = DefaultId.NewUlid(),
            UserId = context.User.Id,
            Name = context.User.UserName ?? string.Empty,
            Email = context.User.Email ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(ct);

        await context.AddClaimAsync(IdentityClaimTypes.ClientId, client.Id.ToString());
    }
}

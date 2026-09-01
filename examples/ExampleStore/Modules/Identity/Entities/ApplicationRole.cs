using Microsoft.AspNetCore.Identity;

namespace Identity.Entities;

/// <summary>
/// Application role entity using <see cref="DefaultId"/> (Ulid) as the primary key type.
/// </summary>
public class ApplicationRole : IdentityRole<DefaultId>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName)
    {
        Id = DefaultId.NewUlid();
    }
}

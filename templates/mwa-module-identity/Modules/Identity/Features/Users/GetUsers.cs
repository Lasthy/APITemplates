using Identity.Contracts.Dtos;
using Identity.Entities;
using Identity.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Identity.Features.Users;

/// <summary>
/// Query to get a paginated list of all users (admin only).
/// </summary>
public record GetUsers(int Page = 1, int PageSize = 20, string? ProfileFilter = null, bool? IsDummy = null) : IRequest<IEnumerable<UserDto>>;

/// <summary>
/// Handles <see cref="GetUsers"/> by querying the Identity database.
/// </summary>
[Logging(Order = 1)]
public class GetUsersHandler : IRequestHandler<GetUsers, IEnumerable<UserDto>>
{
    private readonly IdentityModuleDbContext _dbContext;

    public GetUsersHandler(IdentityModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IEnumerable<UserDto>>> HandleAsync(GetUsers request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ProfileFilter))
        {
            query = query.Where(u => u.ProfileName == request.ProfileFilter);
        }

        if (request.IsDummy.HasValue)
        {
            query = query.Where(u => u.IsDummy == request.IsDummy.Value);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Email ?? string.Empty,
                u.UserName ?? string.Empty,
                u.ProfileName,
                u.IsBanned,
                u.IsDummy,
                u.CreatedAt,
                u.DummyExpiresAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<UserDto>>(users);
    }
}

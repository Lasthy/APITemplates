using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Members;

/// <summary>
/// Command to add a user directly to a tenant with a specified role.
/// </summary>
public record AddMember(DefaultId TenantId, DefaultId UserId, string Role) : IRequest<TenantMemberDto>;

/// <summary>
/// Handles <see cref="AddMember"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class AddMemberHandler : IRequestHandler<AddMember, TenantMemberDto>
{
    private readonly ITenantService _tenantService;

    public AddMemberHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<TenantMemberDto>> HandleAsync(
        AddMember request,
        CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.AddMemberAsync(
            request.TenantId,
            request.UserId,
            request.Role,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TenantMemberDto>(result.Error!);
        }

        var member = result.Value!;
        return Result.Success(new TenantMemberDto(member.UserId, member.Role, member.JoinedAt));
    }
}

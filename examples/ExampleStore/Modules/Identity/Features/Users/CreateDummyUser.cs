using Identity.Contracts.Dtos;
using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to create a dummy user cloning the roles/claims of a source user (System role only).
/// Returns an <see cref="AuthTokenDto"/> for the dummy user.
/// </summary>
public record CreateDummyUser(DefaultId SourceUserId, DefaultId CreatedBy) : IRequest<AuthTokenDto>;

/// <summary>
/// Handles <see cref="CreateDummyUser"/> by creating a dummy clone of the source user.
/// </summary>
[Logging(Order = 1)]
public class CreateDummyUserHandler : IRequestHandler<CreateDummyUser, AuthTokenDto>
{
    private readonly IUserService _userService;

    public CreateDummyUserHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Result<AuthTokenDto>> HandleAsync(CreateDummyUser request, CancellationToken cancellationToken = default)
    {
        return await _userService.CreateDummyUserAsync(request.SourceUserId, request.CreatedBy, cancellationToken);
    }
}

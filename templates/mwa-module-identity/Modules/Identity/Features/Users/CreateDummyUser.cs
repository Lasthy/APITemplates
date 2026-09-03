using Identity.Contracts.Dtos;
using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to create or recycle a dummy user cloning the roles/claims of a source user (System role only).
/// Returns credentials and initial authentication tokens (<see cref="DummyUserDto"/>).
/// </summary>
public record CreateDummyUser(DefaultId SourceUserId, DefaultId CreatedBy, int? LifetimeMinutes = null) : IRequest<DummyUserDto>;

/// <summary>
/// Handles <see cref="CreateDummyUser"/> by creating or recycling a dummy clone of the source user.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class CreateDummyUserHandler : IRequestHandler<CreateDummyUser, DummyUserDto>
{
    private readonly IUserService _userService;

    public CreateDummyUserHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Result<DummyUserDto>> HandleAsync(CreateDummyUser request, CancellationToken cancellationToken = default)
    {
        return await _userService.CreateDummyUserAsync(request.SourceUserId, request.CreatedBy, request.LifetimeMinutes, cancellationToken);
    }
}

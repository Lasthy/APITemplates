using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to create a dummy user cloning the roles/claims of a source user (System role only).
/// Returns an <see cref="AuthTokenDto"/> for the dummy user.
/// </summary>
public record CreateDummyUser(DefaultId SourceUserId, DefaultId CreatedBy) : IRequest<AuthTokenDto>;

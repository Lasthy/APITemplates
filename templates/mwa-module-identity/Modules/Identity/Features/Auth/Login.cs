using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Authentication request: email + password login.
/// </summary>
public record Login(string Email, string Password) : IRequest<AuthTokenDto>;

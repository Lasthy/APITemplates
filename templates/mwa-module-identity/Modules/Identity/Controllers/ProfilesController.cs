using Asp.Versioning;
using Identity.Contracts.Constants;
using Identity.Features.Profiles;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

/// <summary>
/// Profile listing endpoints.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/profiles")]
[ApiExplorerSettings(GroupName = "Identity")]
[Tags("Profiles")]
public class ProfilesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IScopeContext _scopeContext;

    public ProfilesController(IDispatcher dispatcher, IScopeContext scopeContext)
    {
        _dispatcher = dispatcher;
        _scopeContext = scopeContext;
    }

    #region GetProfiles

    /// <summary>
    /// List all registered profiles. Anonymous users see only public profiles.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfiles(CancellationToken ct)
    {
        var includeNonPublic = _scopeContext.IsAuthenticated && _scopeContext.IsInRole(IdentityRoles.Admin);
        var result = await _dispatcher.DispatchAsync(
            new GetProfiles(includeNonPublic), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region GetProfile

    /// <summary>
    /// Get detailed information about a specific profile.
    /// </summary>
    [HttpGet("{name}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfile([FromRoute] string name, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetProfile(name), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => NotFound(new { message = failure.Message }));
    }

    #endregion
}

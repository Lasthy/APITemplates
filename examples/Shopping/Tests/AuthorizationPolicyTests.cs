using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Kernel.Authorization;
using Kernel.Authorization.Extensions;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Shopping.Tests;

public class AuthorizationPolicyTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public AuthorizationPolicyTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    #region Mock Scope Context

    private class TestScopeContext : IScopeContext
    {
        public DefaultId? UserId { get; set; }
        public bool IsAuthenticated { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public bool IsSystem => IsInRole(Kernel.Constants.Roles.System);

        public string? GetClaim(string claimType) => Claims.TryGetValue(claimType, out var v) ? v : null;
        public IReadOnlyList<string> GetClaimValues(string claimType) => Claims.TryGetValue(claimType, out var v) ? [v] : [];
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Test Requests & Handlers & Policies

    public record SecureDataRequest(string Message) : IRequest<string>;

    [Authorize("Authenticated")]
    public class SecureDataHandler : IRequestHandler<SecureDataRequest, string>
    {
        public Task<Result<string>> HandleAsync(SecureDataRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"Processed: {request.Message}"));
        }
    }

    public record AdminOnlyRequest(string Data) : IRequest<string>;

    [Authorize("AdminOnly")]
    public class AdminOnlyHandler : IRequestHandler<AdminOnlyRequest, string>
    {
        public Task<Result<string>> HandleAsync(AdminOnlyRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"Admin accepted: {request.Data}"));
        }
    }

    public record MultiPolicyRequest(string Item) : IRequest<string>;

    [Authorize("Authenticated")]
    [Authorize("VipOnly")]
    public class MultiPolicyHandler : IRequestHandler<MultiPolicyRequest, string>
    {
        public Task<Result<string>> HandleAsync(MultiPolicyRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"VIP: {request.Item}"));
        }
    }

    public class VipPolicy : IAuthorizationPolicy
    {
        public static string PolicyName => "VipOnly";
        public string Name => PolicyName;

        public Task<Result> AuthorizeAsync(IScopeContext context, object? resource, CancellationToken cancellationToken = default)
        {
            if (context.GetClaim("Tier") == "VIP")
            {
                return Task.FromResult(Result.Success());
            }

            return Task.FromResult(Result.Failure(new UnauthorizedAccessException("VIP tier required.")));
        }
    }

    public record MissingPolicyRequest : IRequest<string>;

    [Authorize("NonExistentPolicy")]
    public class MissingPolicyHandler : IRequestHandler<MissingPolicyRequest, string>
    {
        public Task<Result<string>> HandleAsync(MissingPolicyRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>("Should not run"));
        }
    }

    #endregion

    #region Unit Tests: Built-in Policies & Registry

    [Fact]
    public async Task AuthenticatedPolicy_WhenAuthenticated_ReturnsSuccess()
    {
        var policy = new AuthenticatedPolicy();
        var context = new TestScopeContext { IsAuthenticated = true, UserId = DefaultId.NewUlid() };

        var result = await policy.AuthorizeAsync(context, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AuthenticatedPolicy_WhenNotAuthenticated_ReturnsFailure()
    {
        var policy = new AuthenticatedPolicy();
        var context = new TestScopeContext { IsAuthenticated = false };

        var result = await policy.AuthorizeAsync(context, null);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedAccessException>(result.Error);
    }

    [Fact]
    public async Task RolePolicy_WhenHasRole_ReturnsSuccess()
    {
        var policy = new RolePolicy("ManagerPolicy", "Manager", "Admin");
        var context = new TestScopeContext
        {
            IsAuthenticated = true,
            Roles = ["Manager"]
        };

        var result = await policy.AuthorizeAsync(context, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RolePolicy_WhenMissingRole_ReturnsFailure()
    {
        var policy = new RolePolicy("ManagerPolicy", "Manager", "Admin");
        var context = new TestScopeContext
        {
            IsAuthenticated = true,
            Roles = ["Customer"]
        };

        var result = await policy.AuthorizeAsync(context, null);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedAccessException>(result.Error);
    }

    [Fact]
    public async Task RolePolicy_WhenNotAuthenticated_ReturnsFailure()
    {
        var policy = new RolePolicy("ManagerPolicy", "Manager");
        var context = new TestScopeContext { IsAuthenticated = false, Roles = ["Manager"] };

        var result = await policy.AuthorizeAsync(context, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AuthorizationPolicyRegistry_RegisterAndRetrieve_WorksCorrectly()
    {
        var registry = new AuthorizationPolicyRegistry();
        var policy = new AuthenticatedPolicy();

        registry.Register(policy);

        Assert.Same(policy, registry.GetPolicy("Authenticated"));
        Assert.Same(policy, registry.GetPolicy("authenticated")); // case-insensitive
        Assert.Null(registry.GetPolicy("Unknown"));
        Assert.Contains("Authenticated", registry.GetPolicyNames());
    }

    #endregion

    #region Unit Tests: Dispatcher Interceptor

    [Fact]
    public async Task Dispatcher_WithAuthorizeAttribute_WhenAuthorized_Succeeds()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();
        registry.Register(new AuthenticatedPolicy());

        var scopeContext = new TestScopeContext { IsAuthenticated = true };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<SecureDataRequest, string>, SecureDataHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.DispatchAsync(new SecureDataRequest("Hello"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Processed: Hello", result.Value);
    }

    [Fact]
    public async Task Dispatcher_WithAuthorizeAttribute_WhenUnauthorized_ReturnsFailure()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();
        registry.Register(new AuthenticatedPolicy());

        var scopeContext = new TestScopeContext { IsAuthenticated = false };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<SecureDataRequest, string>, SecureDataHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.DispatchAsync(new SecureDataRequest("Hello"));

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedAccessException>(result.Error);
    }

    public record GenericSecureRequest(string Value) : IRequest<string>;

    [Authorize<AuthenticatedPolicy>]
    public class GenericSecureHandler : IRequestHandler<GenericSecureRequest, string>
    {
        public Task<Result<string>> HandleAsync(GenericSecureRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"GenericAuth: {request.Value}"));
        }
    }

    [Fact]
    public async Task Dispatcher_WithGenericAuthorizeAttribute_ResolvesStaticPolicyName()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();
        registry.Register(new AuthenticatedPolicy());

        var scopeContext = new TestScopeContext { IsAuthenticated = true };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<GenericSecureRequest, string>, GenericSecureHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.DispatchAsync(new GenericSecureRequest("NoMagicString"));

        Assert.True(result.IsSuccess);
        Assert.Equal("GenericAuth: NoMagicString", result.Value);
    }

    [Fact]
    public async Task Dispatcher_WithMissingPolicy_ReturnsFailureWithError()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();

        var scopeContext = new TestScopeContext { IsAuthenticated = true };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<MissingPolicyRequest, string>, MissingPolicyHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.DispatchAsync(new MissingPolicyRequest());

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.Contains("NonExistentPolicy", result.Error.Message);
    }

    [Fact]
    public async Task Dispatcher_WithMultiplePolicies_WhenAllPass_Succeeds()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();
        registry.Register(new AuthenticatedPolicy());
        registry.Register(new VipPolicy());

        var scopeContext = new TestScopeContext
        {
            IsAuthenticated = true,
            Claims = new Dictionary<string, string> { ["Tier"] = "VIP" }
        };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<MultiPolicyRequest, string>, MultiPolicyHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.DispatchAsync(new MultiPolicyRequest("SpecialAccess"));

        Assert.True(result.IsSuccess);
        Assert.Equal("VIP: SpecialAccess", result.Value);
    }

    [Fact]
    public async Task Dispatcher_WithMultiplePolicies_WhenOneFails_ReturnsFailure()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();
        registry.Register(new AuthenticatedPolicy());
        registry.Register(new VipPolicy());

        var scopeContext = new TestScopeContext
        {
            IsAuthenticated = true,
            Claims = new Dictionary<string, string> { ["Tier"] = "Standard" }
        };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<MultiPolicyRequest, string>, MultiPolicyHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.DispatchAsync(new MultiPolicyRequest("SpecialAccess"));

        Assert.True(result.IsFailure);
        Assert.Contains("VIP tier required", result.Error?.Message ?? "");
    }

    #endregion

    #region Integration Tests: EndpointFilter & Controller ActionFilter

    [Fact]
    public async Task EndpointFilter_WithAuthenticatedPolicy_AnonymousReturns401_AuthenticatedReturns200()
    {
        var client = _factory.CreateClient();

        // 1. Anonymous request to /api/v1/identity/auth/me should be 401
        var unauthResp = await client.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. Login as admin
        var token = await _factory.LoginAsync(client, "admin@localhost", "Admin123!");
        var authClient = _factory.CreateAuthenticatedClient(token);

        // 3. Authenticated request to /api/v1/identity/auth/me should be 200
        var authResp = await authClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.OK, authResp.StatusCode);
    }

    [Fact]
    public async Task ControllerActionFilter_WithSystemPolicy_NonSystemReturns403_SystemReturns200()
    {
        var client = _factory.CreateClient();

        // 1. Register a regular client user
        var clientEmail = $"regular_{DefaultId.NewUlid()}@test.local";
        var regResp = await client.PostAsJsonAsync("/api/v1/identity/users/register", new
        {
            Email = clientEmail,
            UserName = "regularuser",
            Password = "Password123!",
            ProfileName = "Client"
        });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        // 2. Login as regular user
        var userToken = await _factory.LoginAsync(client, clientEmail, "Password123!");
        var userClient = _factory.CreateAuthenticatedClient(userToken);

        // 3. Try to access System-only endpoint /api/v1/kernel/settings (protected by Roles.System)
        var forbiddenResp = await userClient.GetAsync("/api/v1/kernel/settings?section=Application");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

        // 4. Login as System admin
        var adminToken = await _factory.LoginAsync(client, "admin@localhost", "Admin123!");
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        // 5. System user accessing /api/v1/kernel/settings should succeed (200)
        var adminResp = await adminClient.GetAsync("/api/v1/kernel/settings?section=Application");
        Assert.Equal(HttpStatusCode.OK, adminResp.StatusCode);
    }

    #endregion

    #region Direct Filter Tests

    public interface ITestResourceService
    {
        bool CanAccess(string resourceId, DefaultId? userId);
    }

    public class MockResourceService : ITestResourceService
    {
        public bool CanAccess(string resourceId, DefaultId? userId) => resourceId == "allowed-res";
    }

    public class ScopedResourcePolicy : IAuthorizationPolicy
    {
        private readonly ITestResourceService _resourceService;
        public static string PolicyName => "ScopedResource";
        public string Name => PolicyName;

        public ScopedResourcePolicy(ITestResourceService resourceService)
        {
            _resourceService = resourceService;
        }

        public Task<Result> AuthorizeAsync(IScopeContext context, object? resource, CancellationToken cancellationToken = default)
        {
            if (resource is ResourceAccessRequest req && _resourceService.CanAccess(req.ResourceId, context.UserId))
            {
                return Task.FromResult(Result.Success());
            }

            return Task.FromResult(Result.Failure(new UnauthorizedAccessException("Resource access denied.")));
        }
    }

    [Fact]
    public async Task Dispatcher_WithScopedPolicyHavingDependencies_ResolvesViaDI()
    {
        var services = new ServiceCollection();
        var registry = new AuthorizationPolicyRegistry();
        registry.Register("ScopedResource", typeof(ScopedResourcePolicy));

        var scopeContext = new TestScopeContext { IsAuthenticated = true, UserId = DefaultId.NewUlid() };

        services.AddSingleton(registry);
        services.AddSingleton<IScopeContext>(scopeContext);
        services.AddScoped<ITestResourceService, MockResourceService>();
        services.AddScoped<ScopedResourcePolicy>();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<ResourceAccessRequest, string>, ResourceAccessHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var allowedResult = await dispatcher.DispatchAsync(new ResourceAccessRequest("allowed-res"));
        Assert.True(allowedResult.IsSuccess);
        Assert.Equal("Accessed: allowed-res", allowedResult.Value);

        var deniedResult = await dispatcher.DispatchAsync(new ResourceAccessRequest("secret-res"));
        Assert.True(deniedResult.IsFailure);
    }

    public record ResourceAccessRequest(string ResourceId) : IRequest<string>;

    [Authorize("ScopedResource")]
    public class ResourceAccessHandler : IRequestHandler<ResourceAccessRequest, string>
    {
        public Task<Result<string>> HandleAsync(ResourceAccessRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"Accessed: {request.ResourceId}"));
        }
    }

    [Fact]
    public async Task EndpointFilter_WhenPolicyNotFound_Returns500Problem()
    {
        var filter = new AuthorizationEndpointFilter("NonExistent");
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(new AuthorizationPolicyRegistry());
        services.AddSingleton<IScopeContext>(new TestScopeContext());
        httpContext.RequestServices = services.BuildServiceProvider();

        var filterContext = new DefaultEndpointFilterInvocationContext(httpContext);
        var result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>("next"));

        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    [Fact]
    public async Task ActionFilter_WhenPolicyNotFound_Returns500ObjectResult()
    {
        var filter = new AuthorizePolicyAttribute("NonExistent");
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(new AuthorizationPolicyRegistry());
        services.AddSingleton<IScopeContext>(new TestScopeContext());
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

        var executingContext = new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(
            actionContext,
            new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        await filter.OnActionExecutionAsync(executingContext, () => Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext(actionContext, new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(), new object())));

        var objectResult = Assert.IsType<ObjectResult>(executingContext.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    #endregion
}

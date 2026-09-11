using System.Security.Claims;
using Kernel.Constants;
using Kernel.Context;
using Kernel.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Kernel.Tests.Context;

public class WebScopeContextTests
{
    private class MockHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    [Fact]
    public void Anonymous_ShouldHaveNoUserOrRoles()
    {
        var accessor = new MockHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var context = new WebScopeContext(accessor);

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.UserName);
        Assert.False(context.IsSystem);
        Assert.Empty(context.Roles);
    }

    [Fact]
    public void Authenticated_WithUlid_ShouldParseUserId()
    {
        var expectedId = DefaultId.NewUlid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, expectedId.ToString()),
            new Claim(ClaimTypes.Name, "alice"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessor = new MockHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var context = new WebScopeContext(accessor);

        Assert.True(context.IsAuthenticated);
        Assert.Equal(expectedId, context.UserId);
        Assert.Equal("alice", context.UserName);
        Assert.Contains("Admin", context.Roles);
        Assert.Contains("User", context.Roles);
        Assert.True(context.IsInRole("Admin"));
        Assert.False(context.IsSystem);
    }

    [Fact]
    public void IsSystem_WhenInSystemRole_ShouldReturnTrue()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, Roles.System)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessor = new MockHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var context = new WebScopeContext(accessor);

        Assert.True(context.IsSystem);
    }

    [Fact]
    public void CorrelationId_FromHeader_ShouldBeExtracted()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[DiagnosticsConstants.CorrelationIdHeader] = "corr-123-abc";

        var accessor = new MockHttpContextAccessor { HttpContext = httpContext };
        var context = new WebScopeContext(accessor);

        Assert.Equal("corr-123-abc", context.CorrelationId);
    }

    [Fact]
    public void CorrelationId_FromItems_ShouldTakePrecedence()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[DiagnosticsConstants.CorrelationIdItemKey] = "corr-item-456";
        httpContext.Request.Headers[DiagnosticsConstants.CorrelationIdHeader] = "corr-header-123";

        var accessor = new MockHttpContextAccessor { HttpContext = httpContext };
        var context = new WebScopeContext(accessor);

        Assert.Equal("corr-item-456", context.CorrelationId);
    }
}

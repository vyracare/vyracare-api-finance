using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Vyracare.Api.Finance.Common.Tenancy;
using Xunit;

namespace Vyracare.Api.Finance.Tests;

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public async Task AuthenticatedRequestWithoutTenantClaimsIsForbidden()
    {
        var nextCalled = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "test"))
        };

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task AuthenticatedRequestWithTenantClaimsContinues()
    {
        var nextCalled = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tenant_id", "tenant-a"),
                new Claim("membership_id", "membership-a"),
                new Claim("tenant_role", "Owner")
            ], "test"))
        };

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}

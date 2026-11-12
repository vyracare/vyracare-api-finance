namespace Vyracare.Api.Finance.Common.Tenancy;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            (string.IsNullOrWhiteSpace(context.User.FindFirst("tenant_id")?.Value) ||
             string.IsNullOrWhiteSpace(context.User.FindFirst("membership_id")?.Value) ||
             string.IsNullOrWhiteSpace(context.User.FindFirst("tenant_role")?.Value)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Tenant context is required." });
            return;
        }

        await _next(context);
    }
}

using System.Security.Claims;

namespace Vyracare.Api.Finance.Common.Tenancy;

public interface ITenantContext
{
    string TenantId { get; }
    string MembershipId { get; }
    string Role { get; }
}

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public string TenantId => Required("tenant_id");
    public string MembershipId => Required("membership_id");
    public string Role => Required("tenant_role");

    private string Required(string claim)
    {
        var value = _accessor.HttpContext?.User.FindFirstValue(claim);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Required tenant claim '{claim}' is missing.");
    }
}

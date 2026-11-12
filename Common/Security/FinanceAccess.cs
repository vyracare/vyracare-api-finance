using System.Security.Claims;

namespace Vyracare.Api.Finance.Common.Security;

public static class FinanceAccess
{
    public const string ReadPolicy = "Finance.Read";
    public const string ManagePolicy = "Finance.Manage";

    public static bool CanRead(ClaimsPrincipal user)
    {
        var tenantRole = Claim(user, "tenant_role");
        var accessLevel = Claim(user, "access_level");
        var jobRole = Claim(user, "job_role");

        return IsTenantAdministrator(tenantRole)
            || IsAny(accessLevel, "Administrador", "Gestor")
            || Is(jobRole, "Financeiro");
    }

    public static bool CanManage(ClaimsPrincipal user)
    {
        var tenantRole = Claim(user, "tenant_role");
        var accessLevel = Claim(user, "access_level");
        var jobRole = Claim(user, "job_role");

        return IsTenantAdministrator(tenantRole)
            || Is(accessLevel, "Administrador")
            || (Is(jobRole, "Financeiro") && IsAny(accessLevel, "Gestor", "Operacional"));
    }

    private static string? Claim(ClaimsPrincipal user, string name) => user.FindFirst(name)?.Value;
    private static bool IsTenantAdministrator(string? value) => IsAny(value, "Owner", "Administrator", "Proprietario", "Administrador");
    private static bool IsAny(string? value, params string[] candidates) => candidates.Any(candidate => Is(value, candidate));
    private static bool Is(string? value, string candidate) => string.Equals(value?.Trim(), candidate, StringComparison.OrdinalIgnoreCase);
}

using System.Security.Claims;
using Vyracare.Api.Finance.Common.Security;
using Xunit;

namespace Vyracare.Api.Finance.Tests;

public sealed class FinanceAccessTests
{
    [Theory]
    [InlineData("Owner", null, null, true, true)]
    [InlineData(null, "Administrador", "Clinico", true, true)]
    [InlineData(null, "Gestor", "Financeiro", true, true)]
    [InlineData(null, "Leitura", "Financeiro", true, false)]
    [InlineData(null, "Operacional", "Clinico", false, false)]
    public void ResolvesReadAndManagePermissions(string? tenantRole, string? accessLevel, string? jobRole, bool canRead, bool canManage)
    {
        var claims = new List<Claim>();
        if (tenantRole is not null) claims.Add(new Claim("tenant_role", tenantRole));
        if (accessLevel is not null) claims.Add(new Claim("access_level", accessLevel));
        if (jobRole is not null) claims.Add(new Claim("job_role", jobRole));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Equal(canRead, FinanceAccess.CanRead(principal));
        Assert.Equal(canManage, FinanceAccess.CanManage(principal));
    }
}

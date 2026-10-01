namespace Vyracare.Api.Finance.Common.Configuration;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "vyracare_db";
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public string AllowedOrigins { get; set; } = "*";
}

public sealed class FinanceOptions
{
    public const string SectionName = "Finance";
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

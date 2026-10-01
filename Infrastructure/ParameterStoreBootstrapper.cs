using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace Vyracare.Api.Finance.Infrastructure;

public static class ParameterStoreBootstrapper
{
    public static async Task ApplyAsync(ConfigurationManager configuration)
    {
        var overrides = new Dictionary<string, string?>();
        await LoadAsync(configuration, overrides, "Mongo:ConnectionString", "MONGO_URI",
            "MONGO_PARAMETER_NAME", "Parameters:MongoParameterName", "ConnectionString");
        await LoadAsync(configuration, overrides, "Jwt:Key", "JWT_KEY",
            "JWT_PARAMETER_NAME", "Parameters:JwtParameterName", "Key");
        if (overrides.Count > 0) configuration.AddInMemoryCollection(overrides);
    }

    private static async Task LoadAsync(
        IConfiguration configuration,
        IDictionary<string, string?> overrides,
        string targetKey,
        string fallbackEnvironmentVariable,
        string parameterEnvironmentVariable,
        string parameterConfigurationKey,
        string jsonProperty)
    {
        if (!string.IsNullOrWhiteSpace(configuration[targetKey])) return;
        var fallback = Environment.GetEnvironmentVariable(fallbackEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            overrides[targetKey] = fallback;
            return;
        }

        var parameterName = Environment.GetEnvironmentVariable(parameterEnvironmentVariable)
            ?? configuration[parameterConfigurationKey];
        if (string.IsNullOrWhiteSpace(parameterName)) return;
        if (parameterName.Contains('/') && !parameterName.StartsWith('/')) parameterName = "/" + parameterName;
        using var client = new AmazonSimpleSystemsManagementClient();
        var response = await client.GetParameterAsync(new GetParameterRequest { Name = parameterName, WithDecryption = true });
        var value = response.Parameter?.Value;
        if (!string.IsNullOrWhiteSpace(value)) overrides[targetKey] = ExtractValue(value, jsonProperty);
    }

    private static string ExtractValue(string value, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }
        return value;
    }
}

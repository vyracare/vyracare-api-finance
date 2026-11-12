using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Vyracare.Api.Finance.Infrastructure.Persistence;

public sealed class MongoFinanceIndexInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MongoFinanceIndexInitializer> _logger;

    public MongoFinanceIndexInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<MongoFinanceIndexInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var entries = database.GetCollection<BsonDocument>("financial_entries");
        var invoices = database.GetCollection<BsonDocument>("invoices");

        try
        {
            await entries.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                    .Ascending("tenantId").Descending("OccurredAt")),
                cancellationToken: cancellationToken);
            await invoices.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                    .Ascending("tenantId").Ascending("Status").Ascending("DueAt")),
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            _logger.LogWarning(exception, "Nao foi possivel reconciliar os indices financeiros durante a inicializacao.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

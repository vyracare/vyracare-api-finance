using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Vyracare.Api.Finance.Features.Finance.Shared.Domain;
using Vyracare.Api.Finance.Features.Finance.Shared.Ports;

namespace Vyracare.Api.Finance.Infrastructure.Persistence;

public sealed class MongoFinanceRepository : IFinanceRepository
{
    private readonly IMongoCollection<FinancialEntryDocument> _entries;
    private readonly IMongoCollection<InvoiceDocument> _invoices;

    public MongoFinanceRepository(IMongoDatabase database)
    {
        _entries = database.GetCollection<FinancialEntryDocument>("financial_entries");
        _invoices = database.GetCollection<InvoiceDocument>("invoices");
    }

    public async Task<FinancialEntry> AddEntryAsync(FinancialEntry entry, CancellationToken cancellationToken)
    {
        var document = ToDocument(entry);
        await _entries.InsertOneAsync(document, cancellationToken: cancellationToken);
        entry.Id = document.Id;
        return entry;
    }

    public async Task<Invoice> AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var document = ToDocument(invoice);
        await _invoices.InsertOneAsync(document, cancellationToken: cancellationToken);
        invoice.Id = document.Id;
        return invoice;
    }

    public async Task<Invoice?> GetInvoiceAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var document = await _invoices.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    public Task ReplaceInvoiceAsync(Invoice invoice, CancellationToken cancellationToken) =>
        _invoices.ReplaceOneAsync(x => x.Id == invoice.Id, ToDocument(invoice), cancellationToken: cancellationToken);

    public async Task<decimal> SumConfirmedEntriesAsync(
        FinancialEntryType type,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var amounts = await _entries.Find(x =>
                x.Type == type && x.Status == FinancialEntryStatus.Confirmed &&
                x.OccurredAt >= fromUtc && x.OccurredAt < toUtc)
            .Project(x => x.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    public async Task<(long Count, decimal Amount)> GetPendingInvoicesAsync(CancellationToken cancellationToken)
    {
        var amounts = await _invoices.Find(x => x.Status == InvoiceStatus.Pending)
            .Project(x => x.Amount)
            .ToListAsync(cancellationToken);
        return (amounts.Count, amounts.Sum());
    }

    private static FinancialEntryDocument ToDocument(FinancialEntry value) => new()
    {
        Id = value.Id,
        Type = value.Type,
        Status = value.Status,
        Amount = value.Amount,
        Description = value.Description,
        OccurredAt = value.OccurredAt,
        CreatedAt = value.CreatedAt
    };

    private static InvoiceDocument ToDocument(Invoice value) => new()
    {
        Id = value.Id,
        Description = value.Description,
        Amount = value.Amount,
        DueAt = value.DueAt,
        Status = value.Status,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static Invoice ToDomain(InvoiceDocument value) => new()
    {
        Id = value.Id,
        Description = value.Description,
        Amount = value.Amount,
        DueAt = value.DueAt,
        Status = value.Status,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private sealed class FinancialEntryDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        [BsonRepresentation(BsonType.String)] public FinancialEntryType Type { get; set; }
        [BsonRepresentation(BsonType.String)] public FinancialEntryStatus Status { get; set; }
        [BsonRepresentation(BsonType.Decimal128)] public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class InvoiceDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        public string Description { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.Decimal128)] public decimal Amount { get; set; }
        public DateTime DueAt { get; set; }
        [BsonRepresentation(BsonType.String)] public InvoiceStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

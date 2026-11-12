using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Vyracare.Api.Finance.Common.Tenancy;
using Vyracare.Api.Finance.Features.Finance.Shared.Domain;
using Vyracare.Api.Finance.Features.Finance.Shared.Ports;

namespace Vyracare.Api.Finance.Infrastructure.Persistence;

public sealed class MongoFinanceRepository : IFinanceRepository
{
    private readonly IMongoCollection<FinancialEntryDocument> _entries;
    private readonly IMongoCollection<InvoiceDocument> _invoices;
    private readonly string _tenantId;

    public MongoFinanceRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _entries = database.GetCollection<FinancialEntryDocument>("financial_entries");
        _invoices = database.GetCollection<InvoiceDocument>("invoices");
        _tenantId = tenantContext.TenantId;
    }

    public async Task<FinancialEntry> AddEntryAsync(FinancialEntry entry, CancellationToken cancellationToken)
    {
        entry.TenantId = _tenantId;
        var document = ToDocument(entry);
        await _entries.InsertOneAsync(document, cancellationToken: cancellationToken);
        entry.Id = document.Id;
        return entry;
    }

    public async Task<IReadOnlyCollection<FinancialEntry>> ListEntriesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        FinancialEntryType? type,
        FinancialEntryStatus? status,
        FinancialEntryKind? kind,
        string? search,
        CancellationToken cancellationToken)
    {
        var builder = Builders<FinancialEntryDocument>.Filter;
        var filter = builder.Eq(item => item.TenantId, _tenantId) &
                     builder.Gte(item => item.OccurredAt, fromUtc) &
                     builder.Lt(item => item.OccurredAt, toUtc);
        if (type.HasValue) filter &= builder.Eq(item => item.Type, type.Value);
        if (status.HasValue) filter &= builder.Eq(item => item.Status, status.Value);
        if (kind.HasValue) filter &= builder.Eq(item => item.Kind, kind.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = System.Text.RegularExpressions.Regex.Escape(search.Trim());
            var regex = new BsonRegularExpression(pattern, "i");
            filter &= builder.Or(builder.Regex(item => item.Description, regex), builder.Regex(item => item.Counterparty, regex));
        }

        var documents = await _entries.Find(filter)
            .SortByDescending(item => item.OccurredAt)
            .ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public async Task<FinancialEntry?> GetEntryAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var document = await _entries.Find(item => item.TenantId == _tenantId && item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    public Task ReplaceEntryAsync(FinancialEntry entry, CancellationToken cancellationToken) =>
        _entries.ReplaceOneAsync(
            item => item.TenantId == _tenantId && item.Id == entry.Id,
            ToDocument(entry),
            cancellationToken: cancellationToken);

    public async Task<Invoice> AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        invoice.TenantId = _tenantId;
        var document = ToDocument(invoice);
        await _invoices.InsertOneAsync(document, cancellationToken: cancellationToken);
        invoice.Id = document.Id;
        return invoice;
    }

    public async Task<IReadOnlyCollection<Invoice>> ListInvoicesAsync(
        InvoiceStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var builder = Builders<InvoiceDocument>.Filter;
        var filter = builder.Eq(item => item.TenantId, _tenantId);
        if (status.HasValue) filter &= builder.Eq(item => item.Status, status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = System.Text.RegularExpressions.Regex.Escape(search.Trim());
            var regex = new BsonRegularExpression(pattern, "i");
            filter &= builder.Or(builder.Regex(item => item.Description, regex), builder.Regex(item => item.CustomerName, regex));
        }

        var documents = await _invoices.Find(filter)
            .SortBy(item => item.DueAt)
            .ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public async Task<Invoice?> GetInvoiceAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var document = await _invoices.Find(item => item.TenantId == _tenantId && item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    public Task ReplaceInvoiceAsync(Invoice invoice, CancellationToken cancellationToken) =>
        _invoices.ReplaceOneAsync(
            item => item.TenantId == _tenantId && item.Id == invoice.Id,
            ToDocument(invoice),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyCollection<Invoice>> ListUnreadInvoiceNotificationsAsync(
        DateTime dueThroughUtc,
        CancellationToken cancellationToken)
    {
        var documents = await _invoices.Find(item =>
                item.TenantId == _tenantId &&
                item.Status == InvoiceStatus.Pending &&
                item.NotificationReadAt == null &&
                item.DueAt <= dueThroughUtc)
            .SortBy(item => item.DueAt)
            .ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public async Task<decimal> SumConfirmedEntriesAsync(
        FinancialEntryType type,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var amounts = await _entries.Find(item =>
                item.TenantId == _tenantId &&
                item.Type == type &&
                item.Status == FinancialEntryStatus.Confirmed &&
                item.OccurredAt >= fromUtc && item.OccurredAt < toUtc)
            .Project(item => item.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    public async Task<(long Count, decimal Amount)> GetPendingInvoicesAsync(CancellationToken cancellationToken)
    {
        var amounts = await _invoices.Find(item => item.TenantId == _tenantId && item.Status == InvoiceStatus.Pending)
            .Project(item => item.Amount)
            .ToListAsync(cancellationToken);
        return (amounts.Count, amounts.Sum());
    }

    private static FinancialEntryDocument ToDocument(FinancialEntry value) => new()
    {
        Id = value.Id,
        TenantId = value.TenantId,
        Type = value.Type,
        Status = value.Status,
        Amount = value.Amount,
        Description = value.Description,
        Kind = value.Kind,
        PaymentMethod = value.PaymentMethod,
        Counterparty = value.Counterparty,
        Category = value.Category,
        Notes = value.Notes,
        InstallmentNumber = value.InstallmentNumber,
        InstallmentCount = value.InstallmentCount,
        InstallmentGroupId = value.InstallmentGroupId,
        OccurredAt = value.OccurredAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static FinancialEntry ToDomain(FinancialEntryDocument value) => new()
    {
        Id = value.Id,
        TenantId = value.TenantId,
        Type = value.Type,
        Status = value.Status,
        Amount = value.Amount,
        Description = value.Description,
        Kind = value.Kind,
        PaymentMethod = value.PaymentMethod,
        Counterparty = value.Counterparty,
        Category = value.Category,
        Notes = value.Notes,
        InstallmentNumber = value.InstallmentNumber,
        InstallmentCount = value.InstallmentCount,
        InstallmentGroupId = value.InstallmentGroupId,
        OccurredAt = value.OccurredAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static InvoiceDocument ToDocument(Invoice value) => new()
    {
        Id = value.Id,
        TenantId = value.TenantId,
        Description = value.Description,
        CustomerName = value.CustomerName,
        PaymentMethod = value.PaymentMethod,
        InstallmentNumber = value.InstallmentNumber,
        InstallmentCount = value.InstallmentCount,
        InstallmentGroupId = value.InstallmentGroupId,
        Amount = value.Amount,
        DueAt = value.DueAt,
        Status = value.Status,
        NotificationReadAt = value.NotificationReadAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static Invoice ToDomain(InvoiceDocument value) => new()
    {
        Id = value.Id,
        TenantId = value.TenantId,
        Description = value.Description,
        CustomerName = value.CustomerName,
        PaymentMethod = value.PaymentMethod,
        InstallmentNumber = value.InstallmentNumber,
        InstallmentCount = value.InstallmentCount,
        InstallmentGroupId = value.InstallmentGroupId,
        Amount = value.Amount,
        DueAt = value.DueAt,
        Status = value.Status,
        NotificationReadAt = value.NotificationReadAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    internal sealed class FinancialEntryDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        [BsonElement("tenantId")] public string TenantId { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.String)] public FinancialEntryType Type { get; set; }
        [BsonRepresentation(BsonType.String)] public FinancialEntryStatus Status { get; set; }
        [BsonRepresentation(BsonType.Decimal128)] public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.String)] public FinancialEntryKind Kind { get; set; }
        [BsonRepresentation(BsonType.String)] public PaymentMethod PaymentMethod { get; set; }
        public string? Counterparty { get; set; }
        public string? Category { get; set; }
        public string? Notes { get; set; }
        public int? InstallmentNumber { get; set; }
        public int? InstallmentCount { get; set; }
        public string? InstallmentGroupId { get; set; }
        public DateTime OccurredAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    internal sealed class InvoiceDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        [BsonElement("tenantId")] public string TenantId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        [BsonRepresentation(BsonType.String)] public PaymentMethod PaymentMethod { get; set; }
        public int InstallmentNumber { get; set; } = 1;
        public int InstallmentCount { get; set; } = 1;
        public string? InstallmentGroupId { get; set; }
        [BsonRepresentation(BsonType.Decimal128)] public decimal Amount { get; set; }
        public DateTime DueAt { get; set; }
        [BsonRepresentation(BsonType.String)] public InvoiceStatus Status { get; set; }
        public DateTime? NotificationReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

namespace Vyracare.Api.Finance.Features.Finance.Shared.Domain;

public enum FinancialEntryType { Revenue, Expense }
public enum FinancialEntryStatus { Planned, Confirmed, Cancelled }
public enum InvoiceStatus { Pending, Paid, Cancelled }

public sealed class FinancialEntry
{
    public string? Id { get; set; }
    public FinancialEntryType Type { get; set; }
    public FinancialEntryStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Invoice
{
    public string? Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueAt { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

namespace Vyracare.Api.Finance.Features.Finance.Shared.Domain;

public enum FinancialEntryType { Revenue, Expense }
public enum FinancialEntryStatus { Planned, Confirmed, Cancelled }
public enum InvoiceStatus { Pending, Paid, Cancelled }
public enum FinancialEntryKind { Unspecified, ClientReceipt, ProductPurchase, Withdrawal, ProLabore, OperatingExpense, OtherRevenue, OtherExpense }
public enum PaymentMethod { Unspecified, Pix, Cash, DebitCard, CreditCard, BankTransfer, Boleto, Other }

public sealed class FinancialEntry
{
    public string? Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public FinancialEntryType Type { get; set; }
    public FinancialEntryStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public FinancialEntryKind Kind { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
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

public sealed class Invoice
{
    public string? Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int InstallmentNumber { get; set; } = 1;
    public int InstallmentCount { get; set; } = 1;
    public string? InstallmentGroupId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueAt { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime? NotificationReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

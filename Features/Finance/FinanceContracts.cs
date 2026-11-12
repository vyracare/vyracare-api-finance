using Vyracare.Api.Finance.Features.Finance.Shared.Domain;

namespace Vyracare.Api.Finance.Features.Finance;

public sealed record CreateFinancialEntryRequest(
    FinancialEntryType Type,
    FinancialEntryStatus Status,
    decimal Amount,
    string Description,
    DateTime OccurredAt,
    FinancialEntryKind Kind = FinancialEntryKind.Unspecified,
    PaymentMethod PaymentMethod = PaymentMethod.Unspecified,
    string? Counterparty = null,
    string? Category = null,
    string? Notes = null);

public sealed record CreateInvoiceRequest(
    string Description,
    decimal Amount,
    DateTime DueAt,
    string? CustomerName = null,
    PaymentMethod PaymentMethod = PaymentMethod.Unspecified,
    int InstallmentCount = 1);
public sealed record UpdateFinancialEntryStatusRequest(FinancialEntryStatus Status);
public sealed record UpdateInvoiceStatusRequest(InvoiceStatus Status);
public sealed record FinancialEntryResponse(
    string Id,
    FinancialEntryType Type,
    FinancialEntryStatus Status,
    decimal Amount,
    string Description,
    DateTime OccurredAt,
    FinancialEntryKind Kind,
    PaymentMethod PaymentMethod,
    string? Counterparty,
    string? Category,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
public sealed record InvoiceResponse(
    string Id,
    string Description,
    decimal Amount,
    DateTime DueAt,
    InvoiceStatus Status,
    string? CustomerName,
    PaymentMethod PaymentMethod,
    int InstallmentNumber,
    int InstallmentCount,
    string? InstallmentGroupId,
    DateTime? NotificationReadAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
public sealed record FinanceNotificationResponse(
    string Id,
    string Title,
    string Message,
    DateTime DueAt,
    string Severity,
    string Route);
public sealed record AmountVariationMetric(decimal Amount, decimal ChangePercentage);
public sealed record PendingInvoicesMetric(long Count, decimal Amount);
public sealed record FinanceDashboardSummaryResponse(
    string ReferenceMonth,
    string TimeZone,
    AmountVariationMetric ConfirmedRevenue,
    AmountVariationMetric OperatingExpenses,
    PendingInvoicesMetric PendingInvoices);

using Vyracare.Api.Finance.Features.Finance.Shared.Domain;

namespace Vyracare.Api.Finance.Features.Finance;

public sealed record CreateFinancialEntryRequest(
    FinancialEntryType Type,
    FinancialEntryStatus Status,
    decimal Amount,
    string Description,
    DateTime OccurredAt);

public sealed record CreateInvoiceRequest(string Description, decimal Amount, DateTime DueAt);
public sealed record UpdateInvoiceStatusRequest(InvoiceStatus Status);
public sealed record AmountVariationMetric(decimal Amount, decimal ChangePercentage);
public sealed record PendingInvoicesMetric(long Count, decimal Amount);
public sealed record FinanceDashboardSummaryResponse(
    string ReferenceMonth,
    string TimeZone,
    AmountVariationMetric ConfirmedRevenue,
    AmountVariationMetric OperatingExpenses,
    PendingInvoicesMetric PendingInvoices);

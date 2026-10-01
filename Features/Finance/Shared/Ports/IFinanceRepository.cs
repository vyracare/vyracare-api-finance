using Vyracare.Api.Finance.Features.Finance.Shared.Domain;

namespace Vyracare.Api.Finance.Features.Finance.Shared.Ports;

public interface IFinanceRepository
{
    Task<FinancialEntry> AddEntryAsync(FinancialEntry entry, CancellationToken cancellationToken);
    Task<Invoice> AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken);
    Task<Invoice?> GetInvoiceAsync(string id, CancellationToken cancellationToken);
    Task ReplaceInvoiceAsync(Invoice invoice, CancellationToken cancellationToken);
    Task<decimal> SumConfirmedEntriesAsync(FinancialEntryType type, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<(long Count, decimal Amount)> GetPendingInvoicesAsync(CancellationToken cancellationToken);
}

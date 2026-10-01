using Microsoft.Extensions.Options;
using Vyracare.Api.Finance.Common.Configuration;
using Vyracare.Api.Finance.Features.Finance;
using Vyracare.Api.Finance.Features.Finance.Shared.Domain;
using Vyracare.Api.Finance.Features.Finance.Shared.Ports;
using Xunit;

namespace Vyracare.Api.Finance.Tests;

public sealed class FinanceServiceTests
{
    [Fact]
    public async Task DashboardSummaryCalculatesAmountsAndMonthlyVariation()
    {
        var repository = new FakeFinanceRepository
        {
            Revenue = 112m,
            PreviousRevenue = 100m,
            Expenses = 94m,
            PreviousExpenses = 100m,
            PendingInvoices = (8, 9600m)
        };
        var service = new FinanceService(repository, Options.Create(new FinanceOptions { TimeZone = "America/Sao_Paulo" }));

        var result = await service.GetDashboardSummaryAsync(new DateOnly(2026, 9, 1), CancellationToken.None);

        Assert.Equal(12m, result.ConfirmedRevenue.ChangePercentage);
        Assert.Equal(-6m, result.OperatingExpenses.ChangePercentage);
        Assert.Equal(8, result.PendingInvoices.Count);
        Assert.Equal(9600m, result.PendingInvoices.Amount);
    }

    [Fact]
    public async Task CreateEntryRejectsNonPositiveAmount()
    {
        var service = new FinanceService(new FakeFinanceRepository(), Options.Create(new FinanceOptions()));
        var request = new CreateFinancialEntryRequest(FinancialEntryType.Revenue, FinancialEntryStatus.Confirmed, 0, "Receita", DateTime.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateEntryAsync(request, CancellationToken.None));
    }

    private sealed class FakeFinanceRepository : IFinanceRepository
    {
        private int _revenueCalls;
        private int _expenseCalls;
        public decimal Revenue { get; init; }
        public decimal PreviousRevenue { get; init; }
        public decimal Expenses { get; init; }
        public decimal PreviousExpenses { get; init; }
        public (long Count, decimal Amount) PendingInvoices { get; init; }
        public Task<FinancialEntry> AddEntryAsync(FinancialEntry entry, CancellationToken cancellationToken) => Task.FromResult(entry);
        public Task<Invoice> AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken) => Task.FromResult(invoice);
        public Task<Invoice?> GetInvoiceAsync(string id, CancellationToken cancellationToken) => Task.FromResult<Invoice?>(null);
        public Task ReplaceInvoiceAsync(Invoice invoice, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<decimal> SumConfirmedEntriesAsync(FinancialEntryType type, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            if (type == FinancialEntryType.Revenue) return Task.FromResult(_revenueCalls++ == 0 ? Revenue : PreviousRevenue);
            return Task.FromResult(_expenseCalls++ == 0 ? Expenses : PreviousExpenses);
        }
        public Task<(long Count, decimal Amount)> GetPendingInvoicesAsync(CancellationToken cancellationToken) => Task.FromResult(PendingInvoices);
    }
}

using Microsoft.Extensions.Options;
using Vyracare.Api.Finance.Common.Configuration;
using Vyracare.Api.Finance.Features.Finance.Shared.Domain;
using Vyracare.Api.Finance.Features.Finance.Shared.Ports;

namespace Vyracare.Api.Finance.Features.Finance;

public sealed class FinanceService
{
    private readonly IFinanceRepository _repository;
    private readonly FinanceOptions _options;

    public FinanceService(IFinanceRepository repository, IOptions<FinanceOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<FinancialEntry> CreateEntryAsync(CreateFinancialEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) throw new ArgumentException("O valor deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(request.Description)) throw new ArgumentException("A descricao e obrigatoria.");
        return await _repository.AddEntryAsync(new FinancialEntry
        {
            Type = request.Type,
            Status = request.Status,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            OccurredAt = EnsureUtc(request.OccurredAt),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<Invoice> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) throw new ArgumentException("O valor deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(request.Description)) throw new ArgumentException("A descricao e obrigatoria.");
        var now = DateTime.UtcNow;
        return await _repository.AddInvoiceAsync(new Invoice
        {
            Description = request.Description.Trim(),
            Amount = request.Amount,
            DueAt = EnsureUtc(request.DueAt),
            Status = InvoiceStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }

    public async Task<Invoice?> UpdateInvoiceStatusAsync(string id, UpdateInvoiceStatusRequest request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null) return null;
        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _repository.ReplaceInvoiceAsync(invoice, cancellationToken);
        return invoice;
    }

    public async Task<FinanceDashboardSummaryResponse> GetDashboardSummaryAsync(DateOnly? referenceMonth, CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var selected = referenceMonth ?? new DateOnly(localNow.Year, localNow.Month, 1);
        var month = new DateOnly(selected.Year, selected.Month, 1);
        var previousMonth = month.AddMonths(-1);

        var currentFrom = ToUtc(month, timeZone);
        var currentTo = ToUtc(month.AddMonths(1), timeZone);
        var previousFrom = ToUtc(previousMonth, timeZone);

        var revenueTask = _repository.SumConfirmedEntriesAsync(FinancialEntryType.Revenue, currentFrom, currentTo, cancellationToken);
        var previousRevenueTask = _repository.SumConfirmedEntriesAsync(FinancialEntryType.Revenue, previousFrom, currentFrom, cancellationToken);
        var expensesTask = _repository.SumConfirmedEntriesAsync(FinancialEntryType.Expense, currentFrom, currentTo, cancellationToken);
        var previousExpensesTask = _repository.SumConfirmedEntriesAsync(FinancialEntryType.Expense, previousFrom, currentFrom, cancellationToken);
        var invoicesTask = _repository.GetPendingInvoicesAsync(cancellationToken);
        await Task.WhenAll(revenueTask, previousRevenueTask, expensesTask, previousExpensesTask, invoicesTask);

        return new FinanceDashboardSummaryResponse(
            month.ToString("yyyy-MM"),
            _options.TimeZone,
            new AmountVariationMetric(revenueTask.Result, CalculateVariation(revenueTask.Result, previousRevenueTask.Result)),
            new AmountVariationMetric(expensesTask.Result, CalculateVariation(expensesTask.Result, previousExpensesTask.Result)),
            new PendingInvoicesMetric(invoicesTask.Result.Count, invoicesTask.Result.Amount));
    }

    private static decimal CalculateVariation(decimal current, decimal previous)
    {
        if (previous == 0) return current == 0 ? 0 : 100;
        return Math.Round((current - previous) * 100 / Math.Abs(previous), 1, MidpointRounding.AwayFromZero);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), timeZone);
}

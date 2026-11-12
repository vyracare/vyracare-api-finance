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

    public async Task<FinancialEntryResponse> CreateEntryAsync(
        CreateFinancialEntryRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAmountAndDescription(request.Amount, request.Description);
        if (!Enum.IsDefined(request.Type) || !Enum.IsDefined(request.Status))
            throw new ArgumentException("O tipo ou status do lancamento e invalido.");
        ValidateKindMatchesType(request.Type, request.Kind);

        var now = DateTime.UtcNow;
        var entry = await _repository.AddEntryAsync(new FinancialEntry
        {
            Type = request.Type,
            Status = request.Status,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Kind = ResolveKind(request.Type, request.Kind),
            PaymentMethod = request.PaymentMethod,
            Counterparty = Clean(request.Counterparty),
            Category = Clean(request.Category),
            Notes = Clean(request.Notes),
            OccurredAt = EnsureUtc(request.OccurredAt),
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
        return ToResponse(entry);
    }

    public async Task<IReadOnlyCollection<FinancialEntryResponse>> ListEntriesAsync(
        DateOnly? referenceMonth,
        FinancialEntryType? type,
        FinancialEntryStatus? status,
        FinancialEntryKind? kind,
        string? search,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc, _) = ResolveMonth(referenceMonth);
        var entries = await _repository.ListEntriesAsync(fromUtc, toUtc, type, status, kind, search, cancellationToken);
        return entries.Select(ToResponse).ToArray();
    }

    public async Task<FinancialEntryResponse?> UpdateEntryStatusAsync(
        string id,
        UpdateFinancialEntryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) throw new ArgumentException("O status do lancamento e invalido.");
        var entry = await _repository.GetEntryAsync(id, cancellationToken);
        if (entry is null) return null;
        entry.Status = request.Status;
        entry.UpdatedAt = DateTime.UtcNow;
        await _repository.ReplaceEntryAsync(entry, cancellationToken);
        return ToResponse(entry);
    }

    public async Task<IReadOnlyCollection<InvoiceResponse>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAmountAndDescription(request.Amount, request.Description);
        if (request.InstallmentCount is < 1 or > 60) throw new ArgumentException("A quantidade de parcelas deve estar entre 1 e 60.");
        var now = DateTime.UtcNow;
        var groupId = request.InstallmentCount > 1 ? Guid.NewGuid().ToString("N") : null;
        var baseAmount = Math.Floor(request.Amount / request.InstallmentCount * 100) / 100;
        var remainder = request.Amount - baseAmount * request.InstallmentCount;
        var created = new List<InvoiceResponse>(request.InstallmentCount);
        for (var installment = 1; installment <= request.InstallmentCount; installment++)
        {
            var invoice = await _repository.AddInvoiceAsync(new Invoice
            {
                Description = request.InstallmentCount > 1 ? $"{request.Description.Trim()} ({installment}/{request.InstallmentCount})" : request.Description.Trim(),
                CustomerName = Clean(request.CustomerName),
                PaymentMethod = request.PaymentMethod,
                InstallmentNumber = installment,
                InstallmentCount = request.InstallmentCount,
                InstallmentGroupId = groupId,
                Amount = baseAmount + (installment == request.InstallmentCount ? remainder : 0),
                DueAt = EnsureUtc(request.DueAt).AddMonths(installment - 1),
                Status = InvoiceStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
            created.Add(ToResponse(invoice));
        }
        return created;
    }

    public async Task<IReadOnlyCollection<InvoiceResponse>> ListInvoicesAsync(
        InvoiceStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var invoices = await _repository.ListInvoicesAsync(status, search, cancellationToken);
        return invoices.Select(ToResponse).ToArray();
    }

    public async Task<InvoiceResponse?> UpdateInvoiceStatusAsync(
        string id,
        UpdateInvoiceStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) throw new ArgumentException("O status do boleto e invalido.");
        var invoice = await _repository.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null) return null;
        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTime.UtcNow;
        if (request.Status != InvoiceStatus.Pending) invoice.NotificationReadAt ??= invoice.UpdatedAt;
        await _repository.ReplaceInvoiceAsync(invoice, cancellationToken);
        return ToResponse(invoice);
    }

    public async Task<IReadOnlyCollection<FinanceNotificationResponse>> ListUnreadNotificationsAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var invoices = await _repository.ListUnreadInvoiceNotificationsAsync(now.AddDays(3), cancellationToken);
        return invoices.Select(invoice => new FinanceNotificationResponse(
            invoice.Id!,
            invoice.DueAt < now ? "Boleto vencido" : "Boleto proximo do vencimento",
            $"{invoice.Description}: {invoice.Amount.ToString("C", new System.Globalization.CultureInfo("pt-BR"))}",
            invoice.DueAt,
            invoice.DueAt < now ? "Critical" : "Warning",
            "/financeiro/recebimentos")).ToArray();
    }

    public async Task<bool> MarkNotificationReadAsync(string invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null) return false;
        invoice.NotificationReadAt ??= DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _repository.ReplaceInvoiceAsync(invoice, cancellationToken);
        return true;
    }

    public async Task<FinanceDashboardSummaryResponse> GetDashboardSummaryAsync(
        DateOnly? referenceMonth,
        CancellationToken cancellationToken)
    {
        var (currentFrom, currentTo, month) = ResolveMonth(referenceMonth);
        var previousFrom = ToUtc(month.AddMonths(-1), ResolveTimeZone());

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

    private (DateTime FromUtc, DateTime ToUtc, DateOnly Month) ResolveMonth(DateOnly? referenceMonth)
    {
        var timeZone = ResolveTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var selected = referenceMonth ?? new DateOnly(localNow.Year, localNow.Month, 1);
        var month = new DateOnly(selected.Year, selected.Month, 1);
        return (ToUtc(month, timeZone), ToUtc(month.AddMonths(1), timeZone), month);
    }

    private TimeZoneInfo ResolveTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);

    private static void ValidateAmountAndDescription(decimal amount, string description)
    {
        if (amount <= 0) throw new ArgumentException("O valor deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("A descricao e obrigatoria.");
    }

    private static FinancialEntryResponse ToResponse(FinancialEntry entry) => new(
        entry.Id!, entry.Type, entry.Status, entry.Amount, entry.Description,
        entry.OccurredAt,
        entry.Kind, entry.PaymentMethod, entry.Counterparty, entry.Category, entry.Notes,
        entry.CreatedAt, entry.UpdatedAt);

    private static InvoiceResponse ToResponse(Invoice invoice) => new(
        invoice.Id!, invoice.Description, invoice.Amount, invoice.DueAt, invoice.Status,
        invoice.CustomerName, invoice.PaymentMethod, invoice.InstallmentNumber, invoice.InstallmentCount, invoice.InstallmentGroupId,
        invoice.NotificationReadAt, invoice.CreatedAt, invoice.UpdatedAt);

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

    private static FinancialEntryKind ResolveKind(FinancialEntryType type, FinancialEntryKind kind) =>
        kind != FinancialEntryKind.Unspecified ? kind : type == FinancialEntryType.Revenue ? FinancialEntryKind.OtherRevenue : FinancialEntryKind.OtherExpense;

    private static void ValidateKindMatchesType(FinancialEntryType type, FinancialEntryKind kind)
    {
        if (kind == FinancialEntryKind.Unspecified) return;
        var revenueKind = kind is FinancialEntryKind.ClientReceipt or FinancialEntryKind.OtherRevenue;
        if ((type == FinancialEntryType.Revenue) != revenueKind)
            throw new ArgumentException("A categoria escolhida nao corresponde ao tipo de movimentacao.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
            timeZone);
}

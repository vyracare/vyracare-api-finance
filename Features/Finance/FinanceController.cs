using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vyracare.Api.Finance.Features.Finance.Shared.Domain;
using Vyracare.Api.Finance.Common.Security;

namespace Vyracare.Api.Finance.Features.Finance;

[Authorize(Policy = FinanceAccess.ReadPolicy)]
[ApiController]
[Route("api/finance")]
public sealed class FinanceController : ControllerBase
{
    [HttpGet("entries")]
    public async Task<IActionResult> ListEntries(
        [FromQuery] string? month,
        [FromQuery] FinancialEntryType? type,
        [FromQuery] FinancialEntryStatus? status,
        [FromQuery] FinancialEntryKind? kind,
        [FromQuery] string? search,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken)
    {
        if (!TryParseMonth(month, out var referenceMonth, out var error)) return BadRequest(error);
        return Ok(await service.ListEntriesAsync(referenceMonth, type, status, kind, search, cancellationToken));
    }

    [HttpPost("entries")]
    [Authorize(Policy = FinanceAccess.ManagePolicy)]
    public async Task<IActionResult> CreateEntry(
        [FromBody] CreateFinancialEntryRequest request,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await service.CreateEntryAsync(request, cancellationToken);
            return Created($"/api/finance/entries/{entry.Id}", entry);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPatch("entries/{id}/status")]
    [Authorize(Policy = FinanceAccess.ManagePolicy)]
    public async Task<IActionResult> UpdateEntryStatus(
        string id,
        [FromBody] UpdateFinancialEntryStatusRequest request,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await service.UpdateEntryStatusAsync(id, request, cancellationToken);
            return entry is null ? NotFound(new { message = "Lancamento nao encontrado." }) : Ok(entry);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> ListInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] string? search,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken) =>
        Ok(await service.ListInvoicesAsync(status, search, cancellationToken));

    [HttpPost("invoices")]
    [Authorize(Policy = FinanceAccess.ManagePolicy)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateInvoiceRequest request,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoices = await service.CreateInvoiceAsync(request, cancellationToken);
            return Created("/api/finance/invoices", invoices);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPatch("invoices/{id}/status")]
    [Authorize(Policy = FinanceAccess.ManagePolicy)]
    public async Task<IActionResult> UpdateInvoiceStatus(
        string id,
        [FromBody] UpdateInvoiceStatusRequest request,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await service.UpdateInvoiceStatusAsync(id, request, cancellationToken);
            return invoice is null ? NotFound(new { message = "Boleto nao encontrado." }) : Ok(invoice);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("notifications/unread")]
    public async Task<IActionResult> UnreadNotifications(
        [FromServices] FinanceService service,
        CancellationToken cancellationToken) =>
        Ok(await service.ListUnreadNotificationsAsync(cancellationToken));

    [HttpPost("notifications/{invoiceId}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        string invoiceId,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken) =>
        await service.MarkNotificationReadAsync(invoiceId, cancellationToken)
            ? NoContent()
            : NotFound(new { message = "Notificacao financeira nao encontrada." });

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> DashboardSummary(
        [FromQuery] string? month,
        [FromServices] FinanceService service,
        CancellationToken cancellationToken)
    {
        if (!TryParseMonth(month, out var referenceMonth, out var error)) return BadRequest(error);
        return Ok(await service.GetDashboardSummaryAsync(referenceMonth, cancellationToken));
    }

    private static bool TryParseMonth(string? month, out DateOnly? referenceMonth, out object? error)
    {
        referenceMonth = null;
        error = null;
        if (string.IsNullOrWhiteSpace(month)) return true;
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = new { message = "O mes deve estar no formato yyyy-MM." };
            return false;
        }

        referenceMonth = parsed;
        return true;
    }
}

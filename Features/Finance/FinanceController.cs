using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace Vyracare.Api.Finance.Features.Finance;

[ApiController]
[Route("api/finance")]
public sealed class FinanceController : ControllerBase
{
    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] CreateFinancialEntryRequest request, [FromServices] FinanceService service, CancellationToken cancellationToken)
    {
        try { return Created("/api/finance/entries", await service.CreateEntryAsync(request, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, [FromServices] FinanceService service, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await service.CreateInvoiceAsync(request, cancellationToken);
            return Created($"/api/finance/invoices/{invoice.Id}", invoice);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPatch("invoices/{id}/status")]
    public async Task<IActionResult> UpdateInvoiceStatus(string id, [FromBody] UpdateInvoiceStatusRequest request, [FromServices] FinanceService service, CancellationToken cancellationToken)
    {
        var invoice = await service.UpdateInvoiceStatusAsync(id, request, cancellationToken);
        return invoice is null ? NotFound(new { message = "Boleto nao encontrado." }) : Ok(invoice);
    }

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> DashboardSummary([FromQuery] string? month, [FromServices] FinanceService service, CancellationToken cancellationToken)
    {
        DateOnly? referenceMonth = null;
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return BadRequest(new { message = "O mes deve estar no formato yyyy-MM." });
            referenceMonth = parsed;
        }
        return Ok(await service.GetDashboardSummaryAsync(referenceMonth, cancellationToken));
    }
}

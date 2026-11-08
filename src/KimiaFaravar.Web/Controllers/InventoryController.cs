using System.Security.Claims;
using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using KimiaFaravar.Web.Formatting;
using KimiaFaravar.Web.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize]
public sealed class InventoryController(IInventoryMvpService service, IInventoryReportService reports) : Controller
{
    [Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var data = await service.GetSnapshotAsync(ct);
        return View(new InventoryPageViewModel(InventoryVisibility.FilterForRole(data, User.FindFirstValue(ClaimTypes.Role))));
    }

    [HttpGet, Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> ExportInventoryExcel(CancellationToken ct)
    {
        var data = await GetVisibleSnapshotAsync(ct);
        return File(reports.CreateInventoryExcel(data, CurrentDisplayName()),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"kimia-visible-inventory-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet, Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> ExportInventoryPdf(CancellationToken ct)
    {
        var data = await GetVisibleSnapshotAsync(ct);
        return File(reports.CreateInventoryPdf(data, CurrentDisplayName()), "application/pdf",
            $"kimia-visible-inventory-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    [HttpGet, Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> ExportMovementsExcel(string? from, string? to, CancellationToken ct)
    {
        var selection = await GetMovementSelectionAsync(from, to, ct);
        if (selection is null) return BadRequest("بازه تاریخ شمسی معتبر نیست.");
        return File(reports.CreateMovementsExcel(selection.Value.Movements, CurrentDisplayName(), selection.Value.Caption),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"kimia-stock-movements-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet, Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> ExportMovementsPdf(string? from, string? to, CancellationToken ct)
    {
        var selection = await GetMovementSelectionAsync(from, to, ct);
        if (selection is null) return BadRequest("بازه تاریخ شمسی معتبر نیست.");
        return File(reports.CreateMovementsPdf(selection.Value.Movements, CurrentDisplayName(), selection.Value.Caption),
            "application/pdf", $"kimia-stock-movements-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryReceive)]
    public Task<IActionResult> Receipt(long productId, decimal quantity, string? reason, CancellationToken ct) => ExecuteAsync(() => service.PostReceiptAsync(new(productId, quantity, reason), ct), "ÙˆØ±ÙˆØ¯ Ú©Ø§Ù„Ø§ Ø«Ø¨Øª Ø´Ø¯.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryDispatch)]
    public Task<IActionResult> Dispatch(long productId, decimal quantity, string? reason, CancellationToken ct) => ExecuteAsync(() => service.PostDispatchAsync(new(productId, quantity, reason), ct), "Ø®Ø±ÙˆØ¬ Ú©Ø§Ù„Ø§ Ø«Ø¨Øª Ø´Ø¯.");

    private async Task<InventorySnapshot> GetVisibleSnapshotAsync(CancellationToken ct)
    {
        var data = await service.GetSnapshotAsync(ct);
        return InventoryVisibility.FilterForRole(data, User.FindFirstValue(ClaimTypes.Role));
    }

    private async Task<(IReadOnlyCollection<StockMovementDto> Movements, string Caption)?> GetMovementSelectionAsync(
        string? from, string? to, CancellationToken ct)
    {
        if (!PersianDateTime.TryParseRange(from, to, out var fromUtc, out var toExclusiveUtc))
            return null;
        var visible = await GetVisibleSnapshotAsync(ct);
        var movements = visible.Movements
            .Where(item => item.CreatedAt >= fromUtc && item.CreatedAt < toExclusiveUtc)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        return (movements, $"از {from} تا {to}");
    }

    private string CurrentDisplayName() => User.Identity?.Name ?? "کاربر سامانه";

    private async Task<IActionResult> ExecuteAsync(Func<Task> action, string message)
    {
        try { await action(); TempData["Success"] = message; }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}

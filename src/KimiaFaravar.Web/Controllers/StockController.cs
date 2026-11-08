using System.Security.Claims;
using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Formatting;
using KimiaFaravar.Web.Models;
using KimiaFaravar.Web.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize]
public sealed class StockController(IInventoryMvpService service, IInventoryReportService reports) : Controller
{
    public async Task<IActionResult> Index(long? groupId, string? search, int page = 1, CancellationToken ct = default)
    {
        var data = await SelectionAsync(groupId, search, ct);
        var count = data.Products.Count;
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)count / StockPageViewModel.PageSize)));
        var model = new StockPageViewModel(data with { Products = data.Products.Skip((page - 1) * StockPageViewModel.PageSize)
            .Take(StockPageViewModel.PageSize).ToArray() }, groupId, search, page, count);
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Stock-Partial"] == "true" ? PartialView("_Results", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Excel(long? groupId, string? search, CancellationToken ct)
        => File(reports.CreateInventoryExcel(await SelectionAsync(groupId, search, ct), User.Identity?.Name ?? ""),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kimia-stock.xlsx");

    [HttpGet]
    public async Task<IActionResult> Pdf(long? groupId, string? search, CancellationToken ct)
        => File(reports.CreateInventoryPdf(await SelectionAsync(groupId, search, ct), User.Identity?.Name ?? ""),
            "application/pdf", "kimia-stock.pdf");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryReceive)]
    public Task<IActionResult> Receipt(long productId, decimal quantity, string? reason, long? groupId, string? search, int page, CancellationToken ct)
        => PostAsync(productId, quantity, reason, false, groupId, search, page, ct);

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryDispatch)]
    public Task<IActionResult> Dispatch(long productId, decimal quantity, string? reason, long? groupId, string? search, int page, CancellationToken ct)
        => PostAsync(productId, quantity, reason, true, groupId, search, page, ct);

    private async Task<InventorySnapshot> SelectionAsync(long? groupId, string? search, CancellationToken ct)
        => StockSearch.Filter(InventoryVisibility.FilterForRole(await service.GetSnapshotAsync(ct),
            User.FindFirstValue(ClaimTypes.Role)), groupId, search);

    private async Task<IActionResult> PostAsync(long productId, decimal quantity, string? reason, bool dispatch,
        long? groupId, string? search, int page, CancellationToken ct)
    {
        var visible = await SelectionAsync(null, null, ct);
        if (!visible.Products.Any(product => product.Id == productId)) return Forbid();
        if (!ModelState.IsValid || quantity <= 0 || (dispatch && string.IsNullOrWhiteSpace(reason)))
            TempData["Error"] = "مقدار معتبر و بزرگ‌تر از صفر وارد کنید؛ دلیل خروج الزامی است.";
        else
        {
            try
            {
                if (dispatch) await service.PostDispatchAsync(new(productId, quantity, reason), ct);
                else await service.PostReceiptAsync(new(productId, quantity, reason), ct);
                TempData["Success"] = dispatch ? "خروج کالا ثبت شد." : "ورود کالا ثبت شد.";
            }
            catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        }
        return RedirectToAction(nameof(Index), new { groupId, search, page });
    }
}

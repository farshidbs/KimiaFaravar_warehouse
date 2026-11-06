using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize]
public sealed class InventoryController(IInventoryMvpService service) : Controller
{
    [Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new InventoryPageViewModel(await service.GetSnapshotAsync(ct)));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryReceive)]
    public Task<IActionResult> Receipt(long productId, decimal quantity, string? reason, CancellationToken ct) => ExecuteAsync(() => service.PostReceiptAsync(new(productId, quantity, reason), ct), "ورود کالا ثبت شد.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryDispatch)]
    public Task<IActionResult> Dispatch(long productId, decimal quantity, string? reason, CancellationToken ct) => ExecuteAsync(() => service.PostDispatchAsync(new(productId, quantity, reason), ct), "خروج کالا ثبت شد.");

    private async Task<IActionResult> ExecuteAsync(Func<Task> action, string message)
    {
        try { await action(); TempData["Success"] = message; }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}

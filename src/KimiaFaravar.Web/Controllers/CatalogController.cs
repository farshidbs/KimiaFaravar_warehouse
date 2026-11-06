using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize]
public sealed class CatalogController(IInventoryMvpService service) : Controller
{
    [Authorize(Policy = PermissionCodes.ProductGroupsManage)]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new CatalogPageViewModel(await service.GetSnapshotAsync(ct)));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductGroupsManage)]
    public Task<IActionResult> CreateGroup(string name, string code, long? parentId, CancellationToken ct) => ExecuteAsync(() => service.CreateGroupAsync(new(name, code, parentId), ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.SpecificationsManage)]
    public Task<IActionResult> CreateSpecification(long productGroupId, string name, string code, SpecificationDataType dataType, bool isRequired, bool isVariantKey, CancellationToken ct) => ExecuteAsync(() => service.CreateSpecificationAsync(new(productGroupId, name, code, dataType, isRequired, isVariantKey), ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductsManage)]
    public Task<IActionResult> CreateProduct(long productGroupId, string sku, string name, string baseUnit, CancellationToken ct) => ExecuteAsync(() => service.CreateProductAsync(new(productGroupId, sku, name, baseUnit), ct));

    private async Task<IActionResult> ExecuteAsync(Func<Task<long>> action)
    {
        try { await action(); TempData["Success"] = "اطلاعات با موفقیت ثبت شد."; }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}

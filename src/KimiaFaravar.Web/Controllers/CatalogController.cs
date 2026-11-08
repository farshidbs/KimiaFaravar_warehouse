using System.Security.Claims;
using System.Text.Json;
using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize]
public sealed class CatalogController(IInventoryMvpService service) : Controller
{
    [Authorize(Policy = PermissionCodes.ProductsView)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var data = await service.GetSnapshotAsync(ct);
        return View(new CatalogPageViewModel(InventoryVisibility.FilterForRole(data, User.FindFirstValue(ClaimTypes.Role))));
    }

    [HttpGet, Authorize(Policy = PermissionCodes.ProductsManage)]
    public async Task<IActionResult> EffectiveSpecifications(long groupId, CancellationToken ct)
    {
        try { return Json(await service.GetEffectiveSpecificationsAsync(groupId, ct)); }
        catch (InventoryValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductGroupsManage)]
    public Task<IActionResult> CreateGroup(string name, string code, long? parentId, bool visibleToInternalSales,
        bool visibleToMarketDistribution, CancellationToken ct) =>
        ExecuteAsync(() => service.CreateGroupAsync(new(name, code, parentId, visibleToInternalSales, visibleToMarketDistribution), ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductGroupsManage)]
    [Authorize(Roles = RoleCodes.CEO + "," + RoleCodes.WarehouseManager)]
    public async Task<IActionResult> UpdateGroupVisibility(long groupId, bool visibleToInternalSales,
        bool visibleToMarketDistribution, CancellationToken ct)
    {
        try
        {
            await service.UpdateGroupVisibilityAsync(groupId, visibleToInternalSales, visibleToMarketDistribution, ct);
            TempData["Success"] = "سطح نمایش گروه ذخیره شد.";
        }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return Redirect(Url.Action(nameof(Index)) + "#groups");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.SpecificationsManage)]
    public Task<IActionResult> CreateSpecification(long productGroupId, string name, string code,
        SpecificationDataType dataType, bool isRequired, bool isVariantKey, CancellationToken ct) =>
        ExecuteAsync(() => service.CreateSpecificationAsync(new(productGroupId, name, code, dataType, isRequired, isVariantKey), ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.SpecificationsManage)]
    public Task<IActionResult> CreateSpecificationOption(long specificationId, string value, CancellationToken ct) =>
        ExecuteAsync(() => service.CreateSpecificationOptionAsync(new(specificationId, value), ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductsManage)]
    public async Task<IActionResult> CreateProduct(long productGroupId, string sku, string name,
        string baseUnit, string? valuesJson, CancellationToken ct)
    {
        try
        {
            var values = string.IsNullOrWhiteSpace(valuesJson)
                ? new Dictionary<long, string>()
                : JsonSerializer.Deserialize<Dictionary<long, string>>(valuesJson)
                  ?? new Dictionary<long, string>();
            await service.CreateProductAsync(new(productGroupId, sku, name, baseUnit, values), ct);
            TempData["Success"] = "کالای جدید با مشخصات انتخاب‌شده ثبت شد.";
        }
        catch (JsonException) { TempData["Error"] = "مقادیر مشخصات کالا معتبر نیستند."; }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<long>> action)
    {
        try { await action(); TempData["Success"] = "اطلاعات با موفقیت ثبت شد."; }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}

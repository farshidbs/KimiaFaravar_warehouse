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
    [Authorize(Policy = PermissionCodes.ProductGroupsManage)]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new CatalogPageViewModel(await service.GetSnapshotAsync(ct)));

    [HttpGet, Authorize(Policy = PermissionCodes.ProductsManage)]
    public async Task<IActionResult> EffectiveSpecifications(long groupId, CancellationToken ct)
    {
        try { return Json(await service.GetEffectiveSpecificationsAsync(groupId, ct)); }
        catch (InventoryValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductGroupsManage)]
    public Task<IActionResult> CreateGroup(string name, string code, long? parentId, CancellationToken ct) =>
        ExecuteAsync(() => service.CreateGroupAsync(new(name, code, parentId), ct));

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

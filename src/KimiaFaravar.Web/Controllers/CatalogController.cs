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

    [HttpGet, Authorize(Policy = PermissionCodes.SpecificationsManage)]
    public async Task<IActionResult> GroupSpecifications(long groupId, CancellationToken ct)
    {
        try
        {
            var effective = await service.GetEffectiveSpecificationsAsync(groupId, ct);
            var data = await service.GetSnapshotAsync(ct);
            return Json(effective.Select(spec =>
            {
                var definition = data.Specifications.First(item => item.Id == spec.Id);
                return new { spec.Id, spec.Name, spec.Code, spec.DataType, spec.IsRequired, spec.IsVariantKey, spec.Options,
                    inherited = definition.ProductGroupId != groupId,
                    groupName = data.Groups.FirstOrDefault(group => group.Id == definition.ProductGroupId)?.Name };
            }));
        }
        catch (InventoryValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

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
    public async Task<IActionResult> CreateSpecificationOption(long specificationId, string value, CancellationToken ct)
    {
        var ajax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        try
        {
            var id = await service.CreateSpecificationOptionAsync(new(specificationId, value), ct);
            if (ajax) return Json(new { id, specificationId, value = value.Trim() });
            TempData["Success"] = "گزینه جدید ثبت شد.";
        }
        catch (InventoryValidationException ex)
        {
            if (ajax) return BadRequest(new { error = ex.Message });
            TempData["Error"] = ex.Message;
        }
        return Redirect(Url.Action(nameof(Index)) + "#new-option");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductsManage)]
    [Authorize(Roles = RoleCodes.CEO + "," + RoleCodes.WarehouseManager)]
    public async Task<IActionResult> UpdateProductVisibility(long productId, bool visibleToInternalSales,
        bool visibleToMarketDistribution, CancellationToken ct)
    {
        try
        {
            await service.UpdateProductVisibilityAsync(productId, visibleToInternalSales, visibleToMarketDistribution, ct);
            TempData["Success"] = "سطح نمایش کالا ذخیره شد.";
        }
        catch (InventoryValidationException ex) { TempData["Error"] = ex.Message; }
        return Redirect(Url.Action(nameof(Index)) + "#catalog-view");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.ProductsManage)]
    public async Task<IActionResult> CreateProduct(long productGroupId, string sku, string name,
        string baseUnit, string? valuesJson, bool visibleToInternalSales, bool visibleToMarketDistribution, CancellationToken ct)
    {
        try
        {
            var values = string.IsNullOrWhiteSpace(valuesJson)
                ? new Dictionary<long, string>()
                : JsonSerializer.Deserialize<Dictionary<long, string>>(valuesJson)
                  ?? new Dictionary<long, string>();
            await service.CreateProductAsync(new(productGroupId, sku, name, baseUnit, values,
                visibleToInternalSales, visibleToMarketDistribution), ct);
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

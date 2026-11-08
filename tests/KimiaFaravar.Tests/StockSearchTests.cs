using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Controllers;
using KimiaFaravar.Web.Formatting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Tests;

public sealed class StockSearchTests
{
    private static InventorySnapshot Sample() => new(
        [new(1, "مواد اولیه", "RAW", null, true, false), new(2, "خصوصی", "PRIVATE", null, false, false)],
        [new(1, 1, "رنگ", "COLOR", SpecificationDataType.Select, false, false)],
        [new(1, 1, "RAW-01", "کالای نمونه", "کیلوگرم", 12.5m), new(2, 2, "SECRET", "محرمانه", "عدد", 4m)],
        [], [new(1, 1, "آبی")], [new(1, 1, "1")]);

    [Theory]
    [InlineData("RAW-01")]
    [InlineData("رنگ آبی")]
    [InlineData("مواد اولیه")]
    [InlineData("۱۲.۵ کیلوگرم")]
    [InlineData("كالاي نمونه")]
    public void SearchesEveryDisplayedFieldAndNormalizesPersian(string search)
    {
        var result = StockSearch.Filter(Sample(), null, search);
        Assert.Equal(1L, Assert.Single(result.Products).Id);
    }

    [Fact]
    public void GroupAndTextFiltersIntersectWithoutLeakingHiddenProducts()
    {
        var visible = InventoryVisibility.FilterForRole(Sample(), RoleCodes.InternalSales);
        Assert.Empty(StockSearch.Filter(visible, null, "SECRET").Products);
        Assert.Empty(StockSearch.Filter(visible, 2, null).Products);
        Assert.Empty(StockSearch.Filter(visible, 1, "not-found").Products);
        Assert.Single(StockSearch.Filter(visible, 1, "آبی").Products);
        Assert.Single(StockSearch.Filter(visible, null, null).Products);
    }

    [Theory]
    [InlineData(nameof(StockController.Receipt), PermissionCodes.InventoryReceive)]
    [InlineData(nameof(StockController.Dispatch), PermissionCodes.InventoryDispatch)]
    public void MovementEndpointsRequireTheirPermissionAndCsrfToken(string action, string permission)
    {
        var method = typeof(StockController).GetMethod(action)!;
        Assert.Contains(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == permission);
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpPostAttribute), true));
    }
}

using System.Text.Json.Nodes;
using ClosedXML.Excel;
using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Infrastructure;
using KimiaFaravar.Web.Formatting;
using KimiaFaravar.Web.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KimiaFaravar.Tests;

[Collection("Json storage")]
public sealed class ProductVisibilityTests
{
    public static IEnumerable<object[]> Combinations()
    {
        foreach (var groupInternal in new[] { false, true })
        foreach (var groupMarket in new[] { false, true })
        foreach (var productInternal in new[] { false, true })
        foreach (var productMarket in new[] { false, true })
            yield return [groupInternal, groupMarket, productInternal, productMarket];
    }

    [Theory, MemberData(nameof(Combinations))]
    public void SalesRequireBothGroupAndProductPermissionWhileManagersAlwaysSeeAll(
        bool groupInternal, bool groupMarket, bool productInternal, bool productMarket)
    {
        var data = new InventorySnapshot([new(1, "Group", "G", null, groupInternal, groupMarket)],
            [new(1, 1, "Color", "COLOR", SpecificationDataType.Text, false, false)],
            [new(1, 1, "SKU-1", "Product", "unit", 3m, productInternal, productMarket)],
            [new(1, 1, "Product", MovementType.Receipt, 3m, "unit", "reason", DateTimeOffset.UtcNow)],
            [], [new(1, 1, "Secret value")]);
        foreach (var role in new[] { RoleCodes.CEO, RoleCodes.WarehouseManager })
            Assert.Same(data, InventoryVisibility.FilterForRole(data, role));
        foreach (var (role, allowed) in new[] {
            (RoleCodes.InternalSales, groupInternal && productInternal),
            (RoleCodes.MarketDistribution, groupMarket && productMarket), ("Unknown", false) })
        {
            var visible = InventoryVisibility.FilterForRole(data, role);
            var expected = allowed ? 1 : 0;
            Assert.Equal(expected, visible.Products.Count);
            Assert.Equal(expected, visible.Movements.Count);
            Assert.Equal(expected, visible.ProductValues.Count);
            Assert.Equal(expected, StockSearch.Filter(visible, null, "Secret").Products.Count);
        }
    }

    [Fact]
    public void InventoryAndMovementExportsExcludeProductHiddenInsideVisibleGroup()
    {
        var data = new InventorySnapshot([new(1, "Group", "G", null, true, true)], [],
            [new(1, 1, "PUBLIC", "Public", "unit", 1), new(2, 1, "SECRET-SKU", "SECRET-PRODUCT", "unit", 999, false, false)],
            [new(1, 1, "Public", MovementType.Receipt, 1, "unit", null, DateTimeOffset.UtcNow),
             new(2, 2, "SECRET-PRODUCT", MovementType.Receipt, 999, "unit", "SECRET-REASON", DateTimeOffset.UtcNow)], [], []);
        var reports = new InventoryReportService();
        foreach (var role in new[] { RoleCodes.InternalSales, RoleCodes.MarketDistribution })
        {
            var visible = InventoryVisibility.FilterForRole(data, role);
            foreach (var bytes in new[] { reports.CreateInventoryExcel(visible, "Test"),
                reports.CreateMovementsExcel(visible.Movements, "Test", "Range") })
            {
                using var stream = new MemoryStream(bytes);
                using var workbook = new XLWorkbook(stream);
                var content = string.Join("|", workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()).Select(cell => cell.GetString()));
                Assert.DoesNotContain("SECRET", content);
                Assert.DoesNotContain("999", content);
            }
            // Both PDF reports receive the same filtered snapshot/movement collection.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            Assert.True(reports.CreateInventoryPdf(visible, "Test").Length > 1000);
            Assert.True(reports.CreateMovementsPdf(visible.Movements, "Test", "Range").Length > 1000);
        }
    }

    [Fact]
    public async Task ProductFlagsPersistAndLegacyRecordsKeepGroupBasedVisibility()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kimia-product-visibility-" + Guid.NewGuid().ToString("N"));
        var previous = AppDomain.CurrentDomain.GetData("DataDirectory");
        Directory.CreateDirectory(directory);
        AppDomain.CurrentDomain.SetData("DataDirectory", directory);
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Provider"] = "Json" }).Build();
            using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IInventoryMvpService>();
            var group = await service.CreateGroupAsync(new("Group", "G", null, true, true));
            var product = await service.CreateProductAsync(new(group, "SKU", "Product", "unit"));
            await service.PostReceiptAsync(new(product, 7, "Test"));
            var path = Path.Combine(directory, "inventory-operations.json");
            var legacy = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            var record = legacy["products"]![0]!.AsObject();
            record.Remove("visibleToInternalSales");
            record.Remove("visibleToMarketDistribution");
            await File.WriteAllTextAsync(path, legacy.ToJsonString());
            var snapshot = await service.GetSnapshotAsync();
            Assert.True(Assert.Single(snapshot.Products).VisibleToInternalSales);
            Assert.True(Assert.Single(snapshot.Products).VisibleToMarketDistribution);
            foreach (var internalSales in new[] { false, true })
            foreach (var market in new[] { false, true })
            {
                await service.UpdateProductVisibilityAsync(product, internalSales, market);
                using var fresh = provider.CreateScope();
                snapshot = await fresh.ServiceProvider.GetRequiredService<IInventoryMvpService>().GetSnapshotAsync();
                var saved = Assert.Single(snapshot.Products);
                Assert.Equal(internalSales, saved.VisibleToInternalSales);
                Assert.Equal(market, saved.VisibleToMarketDistribution);
                Assert.Equal(7m, saved.Quantity);
                Assert.Single(snapshot.Movements);
                Assert.True(Assert.Single(snapshot.Groups).VisibleToMarketDistribution);
            }
            var restricted = await service.CreateProductAsync(new(group, "PRIVATE", "Private", "unit", null, false, false));
            Assert.False((await service.GetSnapshotAsync()).Products.Single(item => item.Id == restricted).VisibleToInternalSales);
            await Assert.ThrowsAsync<InventoryValidationException>(() => service.UpdateProductVisibilityAsync(999, true, true));
        }
        finally
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", previous);
            Directory.Delete(directory, recursive: true);
        }
    }
}

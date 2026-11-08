using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KimiaFaravar.Tests;

public sealed class GroupVisibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UpdatingVisibilityPersistsAndControlsSalesAccess(bool internalSales, bool marketSales)
    {
        var directory = Path.Combine(Path.GetTempPath(), "kimia-visibility-" + Guid.NewGuid().ToString("N"));
        var previousDirectory = AppDomain.CurrentDomain.GetData("DataDirectory");
        Directory.CreateDirectory(directory);
        AppDomain.CurrentDomain.SetData("DataDirectory", directory);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Storage:Provider"] = "Json" }).Build();
            using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IInventoryMvpService>();
            var groupId = await service.CreateGroupAsync(new("گروه آزمایشی", "TEST", null, !internalSales, !marketSales));
            var productId = await service.CreateProductAsync(new(groupId, "TEST-01", "کالای آزمایشی", "عدد"));
            await service.PostReceiptAsync(new(productId, 5m, "موجودی آزمایشی"));

            await service.UpdateGroupVisibilityAsync(groupId, internalSales, marketSales);

            using var freshScope = provider.CreateScope();
            var snapshot = await freshScope.ServiceProvider.GetRequiredService<IInventoryMvpService>().GetSnapshotAsync();
            var group = Assert.Single(snapshot.Groups);
            Assert.Equal(internalSales, group.VisibleToInternalSales);
            Assert.Equal(marketSales, group.VisibleToMarketDistribution);
            Assert.Equal("TEST", group.Code);
            Assert.Equal(5m, Assert.Single(snapshot.Products).Quantity);
            Assert.Single(snapshot.Movements);
            foreach (var role in new[] { RoleCodes.CEO, RoleCodes.WarehouseManager })
                Assert.Single(InventoryVisibility.FilterForRole(snapshot, role).Products);
            Assert.Equal(internalSales ? 1 : 0, InventoryVisibility.FilterForRole(snapshot, RoleCodes.InternalSales).Products.Count);
            Assert.Equal(marketSales ? 1 : 0, InventoryVisibility.FilterForRole(snapshot, RoleCodes.MarketDistribution).Products.Count);
            await Assert.ThrowsAsync<InventoryValidationException>(() => service.UpdateGroupVisibilityAsync(groupId + 1, true, true));
        }
        finally
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", previousDirectory);
            Directory.Delete(directory, recursive: true);
        }
    }
}

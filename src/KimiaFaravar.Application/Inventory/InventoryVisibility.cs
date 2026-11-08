using KimiaFaravar.Application.Security;

namespace KimiaFaravar.Application.Inventory;

public static class InventoryVisibility
{
    public static InventorySnapshot FilterForRole(InventorySnapshot source, string? roleCode)
    {
        if (roleCode is RoleCodes.CEO or RoleCodes.WarehouseManager)
            return source;

        Func<ProductGroupDto, bool> isVisible = roleCode switch
        {
            RoleCodes.InternalSales => group => group.VisibleToInternalSales,
            RoleCodes.MarketDistribution => group => group.VisibleToMarketDistribution,
            _ => _ => false
        };

        var directlyVisibleGroupIds = source.Groups.Where(isVisible).Select(group => group.Id).ToHashSet();
        var displayedGroupIds = new HashSet<long>(directlyVisibleGroupIds);
        var groupsById = source.Groups.ToDictionary(group => group.Id);
        foreach (var groupId in directlyVisibleGroupIds)
        {
            var current = groupsById.GetValueOrDefault(groupId);
            while (current?.ParentId is long parentId && groupsById.TryGetValue(parentId, out var parent))
            {
                displayedGroupIds.Add(parentId);
                current = parent;
            }
        }

        var products = source.Products.Where(product => directlyVisibleGroupIds.Contains(product.ProductGroupId)).ToArray();
        var productIds = products.Select(product => product.Id).ToHashSet();
        var specifications = source.Specifications.Where(spec => displayedGroupIds.Contains(spec.ProductGroupId)).ToArray();
        var specificationIds = specifications.Select(spec => spec.Id).ToHashSet();

        return new InventorySnapshot(
            source.Groups.Where(group => displayedGroupIds.Contains(group.Id)).ToArray(),
            specifications,
            products,
            source.Movements.Where(movement => productIds.Contains(movement.ProductId)).ToArray(),
            source.SpecificationOptions.Where(option => specificationIds.Contains(option.SpecificationId)).ToArray(),
            source.ProductValues.Where(value => productIds.Contains(value.ProductId)).ToArray());
    }
}
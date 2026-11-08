using System.Globalization;
using KimiaFaravar.Application.Inventory;

namespace KimiaFaravar.Web.Formatting;

public static class StockSearch
{
    public static string GroupPath(InventorySnapshot data, long groupId)
    {
        var names = new Stack<string>();
        var visited = new HashSet<long>();
        var group = data.Groups.FirstOrDefault(item => item.Id == groupId);
        while (group is not null && visited.Add(group.Id))
        {
            names.Push(group.Name);
            group = data.Groups.FirstOrDefault(item => item.Id == group.ParentId);
        }
        return string.Join(" / ", names);
    }

    public static string Specifications(InventorySnapshot data, long productId) => string.Join("، ",
        data.ProductValues.Where(value => value.ProductId == productId).Select(value =>
        {
            var spec = data.Specifications.FirstOrDefault(item => item.Id == value.SpecificationId);
            var display = spec?.DataType == SpecificationDataType.Select && long.TryParse(value.Value, out var optionId)
                ? data.SpecificationOptions.FirstOrDefault(option => option.Id == optionId)?.Value ?? value.Value
                : spec?.DataType == SpecificationDataType.Boolean
                    ? value.Value == "true" ? "بله" : "خیر" : value.Value;
            return $"{spec?.Name}: {display}";
        }));

    public static InventorySnapshot Filter(InventorySnapshot data, long? groupId, string? search)
    {
        var terms = Normalize(search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var products = data.Products.Where(product => !groupId.HasValue || product.ProductGroupId == groupId)
            .Where(product =>
            {
                var text = Normalize(string.Join(" ", product.Sku, product.Name, product.BaseUnit,
                    product.Quantity.ToString(CultureInfo.InvariantCulture), GroupPath(data, product.ProductGroupId),
                    data.Groups.FirstOrDefault(group => group.Id == product.ProductGroupId)?.Code,
                    Specifications(data, product.Id)));
                return terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
            }).OrderBy(product => product.Name).ThenBy(product => product.Id).ToArray();
        var ids = products.Select(product => product.Id).ToHashSet();
        return data with { Products = products, ProductValues = data.ProductValues.Where(value => ids.Contains(value.ProductId)).ToArray(),
            Movements = data.Movements.Where(movement => ids.Contains(movement.ProductId)).ToArray() };
    }

    private static string Normalize(string text)
    {
        const string persian = "۰۱۲۳۴۵۶۷۸۹";
        const string arabic = "٠١٢٣٤٥٦٧٨٩";
        return string.Concat(text.Replace('ي', 'ی').Replace('ك', 'ک').Replace('\u200c', ' ').Select(c =>
            persian.IndexOf(c) is var p && p >= 0 ? (char)('0' + p) :
            arabic.IndexOf(c) is var a && a >= 0 ? (char)('0' + a) : c));
    }
}

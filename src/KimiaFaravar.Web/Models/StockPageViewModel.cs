using KimiaFaravar.Application.Inventory;

namespace KimiaFaravar.Web.Models;

public sealed record StockPageViewModel(InventorySnapshot Data, long? GroupId, string? Search, int Page, int TotalCount)
{
    public const int PageSize = 30;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

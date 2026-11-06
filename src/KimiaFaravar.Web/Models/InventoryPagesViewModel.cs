using KimiaFaravar.Application.Inventory;
namespace KimiaFaravar.Web.Models;
public sealed record CatalogPageViewModel(InventorySnapshot Data);
public sealed record InventoryPageViewModel(InventorySnapshot Data);

namespace KimiaFaravar.Application.Security;

public static class RoleCodes
{
    public const string CEO = "CEO";
    public const string WarehouseManager = "WarehouseManager";
    public const string WarehouseOperator = "WarehouseOperator";
    public const string InternalSales = "InternalSales";
    public const string MarketDistribution = "MarketDistribution";
}

public static class PermissionCodes
{
    public const string RolesManage = "Roles.Manage";
    public const string ProductsManage = "Products.Manage";
    public const string ProductGroupsManage = "ProductGroups.Manage";
    public const string SpecificationsManage = "Specifications.Manage";
    public const string InventoryView = "Inventory.View";
    public const string InventoryReceive = "Inventory.Receive";
    public const string InventoryDispatch = "Inventory.Dispatch";
    public static readonly string[] All = ["Users.Manage", RolesManage, "Products.View", ProductsManage, ProductGroupsManage, SpecificationsManage, InventoryView, "Inventory.ViewAll", InventoryReceive, InventoryDispatch, "Inventory.DispatchDirect", "Inventory.DispatchApprove", "Inventory.Documents.EditPosted", "Inventory.Reserve", "Inventory.Import", "Reports.View", "Reports.ViewAll"];
}

public sealed record AuthenticatedUser(long Id, string AliasName, string Username, string RoleCode, IReadOnlyCollection<string> Permissions, string SessionToken);
public interface IAuthenticationService
{
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task RevokeSessionAsync(string sessionToken, CancellationToken cancellationToken = default);
}

public interface IStorageInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

public interface ISessionValidator
{
    Task<bool> IsActiveAsync(long userId, string sessionToken, CancellationToken ct = default);
}

public sealed record PermissionSummary(string Code, string Name);
public sealed record RoleSummary(long Id, string Code, string Name, bool IsEnabled, IReadOnlyCollection<PermissionSummary> Permissions);

public interface IAccessControlQueryService
{
    Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(CancellationToken ct = default);
}

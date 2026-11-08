namespace KimiaFaravar.Application.Security;

public static class RoleCodes
{
    public const string CEO = "CEO";
    public const string WarehouseManager = "WarehouseManager";
    public const string InternalSales = "InternalSales";
    public const string MarketDistribution = "MarketDistribution";
}

public static class PermissionCodes
{
    public const string ProductsView = "Products.View";
    public const string RolesManage = "Roles.Manage";
    public const string ProductsManage = "Products.Manage";
    public const string ProductGroupsManage = "ProductGroups.Manage";
    public const string SpecificationsManage = "Specifications.Manage";
    public const string InventoryView = "Inventory.View";
    public const string InventoryReceive = "Inventory.Receive";
    public const string InventoryDispatch = "Inventory.Dispatch";
    public static readonly string[] All = ["Users.Manage", RolesManage, ProductsView, ProductsManage, ProductGroupsManage, SpecificationsManage, InventoryView, "Inventory.ViewAll", InventoryReceive, InventoryDispatch, "Inventory.DispatchDirect", "Inventory.DispatchApprove", "Inventory.Documents.EditPosted", "Inventory.Reserve", "Inventory.Import", "Reports.View", "Reports.ViewAll"];

    public static IReadOnlyCollection<string> ForRole(string roleCode) => roleCode switch
    {
        RoleCodes.CEO => All,
        RoleCodes.WarehouseManager => [ProductsView, ProductsManage, ProductGroupsManage, SpecificationsManage, InventoryView, "Inventory.ViewAll", InventoryReceive, InventoryDispatch, "Inventory.DispatchDirect", "Inventory.DispatchApprove", "Inventory.Documents.EditPosted", "Inventory.Reserve", "Inventory.Import", "Reports.View", "Reports.ViewAll"],
        RoleCodes.InternalSales => [ProductsView, InventoryView, "Inventory.Reserve", "Reports.View"],
        RoleCodes.MarketDistribution => [ProductsView, InventoryView, "Reports.View"],
        _ => []
    };
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

public sealed record UserSummary(long Id, string AliasName, string Username, long RoleId, string RoleCode, string RoleName, bool IsEnabled);
public sealed record CreateUserCommand(string AliasName, string Username, string Password, long RoleId);
public sealed class UserManagementException(string message) : Exception(message);

public sealed record UserAccessHistory(long Id, long UserId, string AliasName, string Username, string Action, DateTimeOffset OccurredAt);
public interface IAccessHistoryService
{
    Task<IReadOnlyCollection<UserAccessHistory>> GetRecentAsync(int count = 100, CancellationToken ct = default);
}
public interface IUserManagementService
{
    Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken ct = default);
    Task<long> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default);
    Task ChangeRoleAsync(long userId, long roleId, long currentUserId, CancellationToken ct = default);
    Task DisableUserAsync(long userId, long currentUserId, CancellationToken ct = default);
    Task EnableUserAsync(long userId, CancellationToken ct = default);
    Task ChangeOwnPasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task ResetPasswordAsync(long userId, string newPassword, long currentAdminId, CancellationToken ct = default);
}
public interface IAccessControlQueryService
{
    Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(CancellationToken ct = default);
}

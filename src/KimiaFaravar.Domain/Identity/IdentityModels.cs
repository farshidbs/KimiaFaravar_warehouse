namespace KimiaFaravar.Domain.Identity;

public abstract class Entity { public long Id { get; protected set; } }

public sealed class User : Entity
{
    private User() { }
    public User(string aliasName, string username, long roleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        AliasName = aliasName.Trim(); Username = username.Trim().ToLowerInvariant(); RoleId = roleId;
        IsEnabled = true; CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }
    public string AliasName { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public long RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void SetPasswordHash(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); PasswordHash = value; UpdatedAt = DateTimeOffset.UtcNow; }
    public void RecordLogin(DateTimeOffset now) { LastLoginAt = now; UpdatedAt = now; }
    public void ChangeRole(long roleId) { RoleId = roleId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Disable() { IsEnabled = false; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Enable() { IsEnabled = true; UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class Role : Entity
{
    private Role() { }
    public Role(string name, string code, string? description = null) { Name = name; Code = code; Description = description; IsEnabled = true; }
    public string Name { get; private set; } = "";
    public string Code { get; private set; } = "";
    public string? Description { get; private set; }
    public bool IsEnabled { get; private set; }
    public ICollection<RolePermission> RolePermissions { get; } = [];
}

public sealed class Permission : Entity
{
    private Permission() { }
    public Permission(string code, string name, string? description = null) { Code = code; Name = name; Description = description; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
}

public sealed class RolePermission
{
    private RolePermission() { }
    public RolePermission(long roleId, long permissionId) { RoleId = roleId; PermissionId = permissionId; }
    public long RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public long PermissionId { get; private set; }
    public Permission Permission { get; private set; } = null!;
}

public sealed class UserSession : Entity
{
    private UserSession() { }
    public UserSession(long userId, string tokenHash, DateTimeOffset expiresAt) { UserId = userId; TokenHash = tokenHash; ExpiresAt = expiresAt; CreatedAt = DateTimeOffset.UtcNow; }
    public long UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}

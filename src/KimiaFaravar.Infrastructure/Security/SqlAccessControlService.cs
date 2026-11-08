using KimiaFaravar.Application.Security;
using KimiaFaravar.Domain.Identity;
using KimiaFaravar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KimiaFaravar.Infrastructure.Security;

internal sealed class SqlAccessControlService(AppDbContext db, IPasswordHasher<User> hasher)
    : ISessionValidator, IAccessControlQueryService, IUserManagementService, IAccessHistoryService
{
    public async Task<bool> IsActiveAsync(long userId, string sessionToken, CancellationToken ct = default)
    {
        var hash = AuthenticationService.Hash(sessionToken);
        return await db.UserSessions.AnyAsync(session => session.UserId == userId && session.TokenHash == hash &&
            session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow && session.User.IsEnabled, ct);
    }

    public async Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(CancellationToken ct = default) =>
        await db.Roles.AsNoTracking().Where(role => role.Code != "WarehouseOperator").OrderBy(role => role.Id)
            .Select(role => new RoleSummary(role.Id, role.Code, role.Name, role.IsEnabled,
                role.RolePermissions.Select(mapping => new PermissionSummary(mapping.Permission.Code, mapping.Permission.Name)).ToArray()))
            .ToArrayAsync(ct);

    public async Task<IReadOnlyCollection<UserAccessHistory>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        var take = Math.Clamp(count, 1, 500);
        return await (from audit in db.AuditLogs.AsNoTracking()
                      join user in db.Users.AsNoTracking() on audit.UserId equals user.Id
                      where audit.EntityName == "UserAccess" && (audit.Action == "Login" || audit.Action == "Logout")
                      orderby audit.CreatedAt descending
                      select new UserAccessHistory(audit.Id, user.Id, user.AliasName, user.Username, audit.Action, audit.CreatedAt))
            .Take(take).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Include(user => user.Role).OrderByDescending(user => user.IsEnabled).ThenBy(user => user.AliasName)
            .Select(user => new UserSummary(user.Id, user.AliasName, user.Username, user.RoleId,
                user.Role.Code, user.Role.Name, user.IsEnabled)).ToArrayAsync(ct);

    public async Task<long> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        ValidateUser(command);
        var username = command.Username.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Username == username, ct)) throw new UserManagementException("نام کاربری تکراری است.");
        if (!await db.Roles.AnyAsync(role => role.Id == command.RoleId && role.Code != "WarehouseOperator" && role.IsEnabled, ct))
            throw new UserManagementException("گروه کاربری معتبر نیست.");
        var user = new User(command.AliasName, username, command.RoleId);
        user.SetPasswordHash(hasher.HashPassword(user, command.Password));
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task ChangeRoleAsync(long userId, long roleId, long currentUserId, CancellationToken ct = default)
    {
        var user = await db.Users.Include(item => item.Role).SingleOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new UserManagementException("کاربر پیدا نشد.");
        var role = await db.Roles.SingleOrDefaultAsync(item => item.Id == roleId && item.Code != "WarehouseOperator" && item.IsEnabled, ct)
            ?? throw new UserManagementException("گروه کاربری معتبر نیست.");
        if (user.Role.Code == RoleCodes.CEO && role.Code != RoleCodes.CEO &&
            await db.Users.CountAsync(item => item.IsEnabled && item.Role.Code == RoleCodes.CEO, ct) <= 1)
            throw new UserManagementException("نقش آخرین مدیرعامل فعال قابل تغییر نیست.");
        user.ChangeRole(role.Id);
        await RevokeSessionsAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DisableUserAsync(long userId, long currentUserId, CancellationToken ct = default)
    {
        if (userId == currentUserId) throw new UserManagementException("نمی‌توانید حساب کاربری خودتان را حذف کنید.");
        var user = await db.Users.Include(item => item.Role).SingleOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new UserManagementException("کاربر پیدا نشد.");
        if (user.Role.Code == RoleCodes.CEO && user.IsEnabled &&
            await db.Users.CountAsync(item => item.IsEnabled && item.Role.Code == RoleCodes.CEO, ct) <= 1)
            throw new UserManagementException("آخرین مدیرعامل فعال قابل حذف نیست.");
        user.Disable();
        await RevokeSessionsAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task EnableUserAsync(long userId, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new UserManagementException("کاربر پیدا نشد.");
        user.Enable();
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeOwnPasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId && item.IsEnabled, ct)
            ?? throw new UserManagementException("کاربر فعال پیدا نشد.");
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            throw new UserManagementException("گذرواژه فعلی نادرست است.");
        user.SetPasswordHash(hasher.HashPassword(user, newPassword));
        await RevokeSessionsAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(long userId, string newPassword, long currentAdminId, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        var adminIsCeo = await db.Users.AnyAsync(user => user.Id == currentAdminId && user.IsEnabled && user.Role.Code == RoleCodes.CEO, ct);
        if (!adminIsCeo) throw new UserManagementException("فقط مدیرعامل می‌تواند گذرواژه دیگران را بازنشانی کند.");
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId && item.IsEnabled, ct)
            ?? throw new UserManagementException("کاربر فعال پیدا نشد.");
        user.SetPasswordHash(hasher.HashPassword(user, newPassword));
        await RevokeSessionsAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new UserManagementException("گذرواژه باید حداقل ۸ نویسه باشد.");
    }
    private async Task RevokeSessionsAsync(long userId, CancellationToken ct)
    {
        var sessions = await db.UserSessions.Where(session => session.UserId == userId && session.RevokedAt == null).ToListAsync(ct);
        foreach (var session in sessions) session.Revoke(DateTimeOffset.UtcNow);
    }

    private static void ValidateUser(CreateUserCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AliasName) || command.AliasName.Trim().Length > 100) throw new UserManagementException("نام فارسی کاربر معتبر نیست.");
        if (string.IsNullOrWhiteSpace(command.Username) || command.Username.Trim().Length > 100 ||
            command.Username.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new UserManagementException("نام کاربری فقط می‌تواند شامل حروف، اعداد، نقطه، خط تیره و زیرخط باشد.");
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8) throw new UserManagementException("گذرواژه باید حداقل ۸ نویسه باشد.");
    }
}
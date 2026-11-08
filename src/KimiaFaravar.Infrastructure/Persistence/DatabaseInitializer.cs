using KimiaFaravar.Application.Security;
using KimiaFaravar.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KimiaFaravar.Infrastructure.Persistence;

public sealed class DatabaseInitializer(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration configuration) : IStorageInitializer
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);

        var requiredRoles = new[]
        {
            (RoleCodes.CEO, "مدیرعامل"),
            (RoleCodes.WarehouseManager, "مدیر انبار"),
            (RoleCodes.InternalSales, "همکاران فروش داخلی"),
            (RoleCodes.MarketDistribution, "همکاران فروش و توزیع بازار")
        };
        foreach (var (code, name) in requiredRoles)
            if (!await db.Roles.AnyAsync(role => role.Code == code, ct)) db.Roles.Add(new Role(name, code));
        await db.SaveChangesAsync(ct);

        foreach (var code in PermissionCodes.All)
            if (!await db.Permissions.AnyAsync(permission => permission.Code == code, ct)) db.Permissions.Add(new Permission(code, code));
        await db.SaveChangesAsync(ct);

        var roles = await db.Roles.Where(role => role.Code != "WarehouseOperator").ToDictionaryAsync(role => role.Code, ct);
        var permissions = await db.Permissions.ToDictionaryAsync(permission => permission.Code, ct);
        var existingMappings = await db.RolePermissions.Select(mapping => new { mapping.RoleId, mapping.PermissionId }).ToListAsync(ct);
        foreach (var role in roles.Values)
        foreach (var permissionCode in PermissionCodes.ForRole(role.Code))
        {
            var permission = permissions[permissionCode];
            if (existingMappings.All(mapping => mapping.RoleId != role.Id || mapping.PermissionId != permission.Id))
                db.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
        }
        await db.SaveChangesAsync(ct);

        var username = configuration["BootstrapAdmin:Username"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password) && !await db.Users.AnyAsync(ct))
        {
            var roleId = roles[RoleCodes.CEO].Id;
            var user = new User(configuration["BootstrapAdmin:AliasName"] ?? "مدیر سیستم", username, roleId);
            user.SetPasswordHash(hasher.HashPassword(user, password));
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
    }
}
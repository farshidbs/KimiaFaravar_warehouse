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
  if (!await db.Roles.AnyAsync(ct)) { db.Roles.AddRange(new Role("مدیرعامل",RoleCodes.CEO),new Role("مدیر انبار",RoleCodes.WarehouseManager),new Role("کاربر انبار",RoleCodes.WarehouseOperator),new Role("همکاران فروش داخلی",RoleCodes.InternalSales),new Role("همکاران توزیع بازار",RoleCodes.MarketDistribution)); await db.SaveChangesAsync(ct); }
  if (!await db.Permissions.AnyAsync(ct)) { db.Permissions.AddRange(PermissionCodes.All.Select(x=>new Permission(x,x))); await db.SaveChangesAsync(ct); var ceo=await db.Roles.SingleAsync(x=>x.Code==RoleCodes.CEO,ct); db.RolePermissions.AddRange(await db.Permissions.Select(x=>new RolePermission(ceo.Id,x.Id)).ToListAsync(ct)); await db.SaveChangesAsync(ct); }
  var username=configuration["BootstrapAdmin:Username"]?.Trim().ToLowerInvariant(); var password=configuration["BootstrapAdmin:Password"];
  if (!string.IsNullOrWhiteSpace(username)&&!string.IsNullOrWhiteSpace(password)&&!await db.Users.AnyAsync(ct)) { var roleId=await db.Roles.Where(x=>x.Code==RoleCodes.CEO).Select(x=>x.Id).SingleAsync(ct); var user=new User(configuration["BootstrapAdmin:AliasName"]??"مدیر سیستم",username,roleId); user.SetPasswordHash(hasher.HashPassword(user,password)); db.Users.Add(user); await db.SaveChangesAsync(ct); }
 }
}

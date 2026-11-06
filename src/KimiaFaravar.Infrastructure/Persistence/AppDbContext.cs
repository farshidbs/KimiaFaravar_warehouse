using KimiaFaravar.Domain.Auditing;
using KimiaFaravar.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace KimiaFaravar.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e => { e.ToTable("Users"); e.HasKey(x => x.Id); e.Property(x => x.AliasName).HasMaxLength(100); e.Property(x => x.Username).HasMaxLength(100); e.HasIndex(x => x.Username).IsUnique(); e.Property(x => x.PasswordHash).HasMaxLength(500); e.Property(x => x.RowVersion).IsRowVersion(); e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<Role>(e => { e.ToTable("Roles"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Code).HasMaxLength(64); e.HasIndex(x => x.Code).IsUnique(); });
        modelBuilder.Entity<Permission>(e => { e.ToTable("Permissions"); e.HasKey(x => x.Id); e.Property(x => x.Code).HasMaxLength(128); e.Property(x => x.Name).HasMaxLength(150); e.HasIndex(x => x.Code).IsUnique(); });
        modelBuilder.Entity<RolePermission>(e => { e.ToTable("RolePermissions"); e.HasKey(x => new { x.RoleId, x.PermissionId }); e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId); e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId); });
        modelBuilder.Entity<UserSession>(e => { e.ToTable("UserSessions"); e.HasKey(x => x.Id); e.Property(x => x.TokenHash).HasMaxLength(64); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => new { x.UserId, x.ExpiresAt }); e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<AuditLog>(e => { e.ToTable("AuditLogs"); e.HasKey(x => x.Id); e.Property(x => x.Action).HasMaxLength(100); e.Property(x => x.EntityName).HasMaxLength(150); e.Property(x => x.EntityId).HasMaxLength(100); e.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAt }); });
    }
}

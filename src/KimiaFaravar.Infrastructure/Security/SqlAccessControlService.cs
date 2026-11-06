using KimiaFaravar.Application.Security;
using KimiaFaravar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KimiaFaravar.Infrastructure.Security;

internal sealed class SqlAccessControlService(AppDbContext db) : ISessionValidator, IAccessControlQueryService
{
    public async Task<bool> IsActiveAsync(long userId, string sessionToken, CancellationToken ct = default)
    {
        var hash = AuthenticationService.Hash(sessionToken);
        return await db.UserSessions.AnyAsync(x => x.UserId == userId && x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow && x.User.IsEnabled, ct);
    }

    public async Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(CancellationToken ct = default)
    {
        return await db.Roles.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new RoleSummary(x.Id, x.Code, x.Name, x.IsEnabled,
                x.RolePermissions.Select(rp => new PermissionSummary(rp.Permission.Code, rp.Permission.Name)).ToArray()))
            .ToArrayAsync(ct);
    }
}

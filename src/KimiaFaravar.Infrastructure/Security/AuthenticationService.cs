using System.Security.Cryptography;
using System.Text;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Domain.Identity;
using KimiaFaravar.Domain.Auditing;
using KimiaFaravar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KimiaFaravar.Infrastructure.Security;

internal sealed class AuthenticationService(AppDbContext db, IPasswordHasher<User> hasher) : IAuthenticationService
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await db.Users.Include(x => x.Role).ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Username == normalized && x.IsEnabled && x.Role.IsEnabled, ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.UserSessions.Add(new UserSession(user.Id, Hash(token), DateTimeOffset.UtcNow.AddHours(8)));
        user.RecordLogin(DateTimeOffset.UtcNow);
        db.AuditLogs.Add(new AuditLog(user.Id, "Login", "UserAccess", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null));
        await db.SaveChangesAsync(ct);
        return new(user.Id, user.AliasName, user.Username, user.Role.Code, user.Role.RolePermissions.Select(x => x.Permission.Code).ToArray(), token);
    }

    public async Task RevokeSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        var session = await db.UserSessions.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == Hash(sessionToken), ct);
        if (session is not null && session.RevokedAt is null) { session.Revoke(DateTimeOffset.UtcNow); db.AuditLogs.Add(new AuditLog(session.UserId, "Logout", "UserAccess", session.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null)); await db.SaveChangesAsync(ct); }
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

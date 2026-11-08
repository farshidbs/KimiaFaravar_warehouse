using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace KimiaFaravar.Infrastructure.JsonStorage;

internal sealed class JsonAuthenticationService(IPasswordHasher<User> hasher, IConfiguration configuration)
    : IAuthenticationService, IStorageInitializer, ISessionValidator, IAccessControlQueryService, IUserManagementService, IAccessHistoryService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static string FilePath => Path.Combine((string?)AppDomain.CurrentDomain.GetData("DataDirectory") ?? AppContext.BaseDirectory, "inventory-demo.json");

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (File.Exists(FilePath)) return;
            var username = configuration["JsonStore:AdminUsername"] ?? "admin";
            var password = configuration["JsonStore:AdminPassword"] ?? "Demo@12345";
            var identity = new User("Ù…Ø¯ÛŒØ± Ø³ÛŒØ³ØªÙ…", username, 1);
            var data = new JsonData
            {
                Roles =
                [
                    new(1, RoleCodes.CEO, "Ù…Ø¯ÛŒØ±Ø¹Ø§Ù…Ù„"), new(2, RoleCodes.WarehouseManager, "Ù…Ø¯ÛŒØ± Ø§Ù†Ø¨Ø§Ø±"),
                    new(4, RoleCodes.InternalSales, "همکاران فروش داخلی"),
                    new(5, RoleCodes.MarketDistribution, "Ù‡Ù…Ú©Ø§Ø±Ø§Ù† ØªÙˆØ²ÛŒØ¹ Ø¨Ø§Ø²Ø§Ø±")
                ],
                Permissions = PermissionCodes.All.ToList(),
                Users = [new(1, identity.AliasName, identity.Username, hasher.HashPassword(identity, password), 1, true)]
            };
            await SaveAsync(data, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var normalizedUsername = username.Trim().ToLowerInvariant();
            var item = data.Users.SingleOrDefault(x => x.Enabled && x.Username == normalizedUsername);
            if (item is null) return null;
            var identity = new User(item.AliasName, item.Username, item.RoleId);
            if (hasher.VerifyHashedPassword(identity, item.PasswordHash, password) == PasswordVerificationResult.Failed) return null;
            var role = data.Roles.Single(x => x.Id == item.RoleId);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            data.Sessions.Add(new(Hash(token), item.Id, DateTimeOffset.UtcNow.AddHours(8), null));
            AddActivity(data, item, "Login");
            await SaveAsync(data, ct);
            var permissions = PermissionCodes.ForRole(role.Code);
            return new(item.Id, item.AliasName, item.Username, role.Code, permissions, token);
        }
        finally { Gate.Release(); }
    }

    public async Task RevokeSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct); var hash = Hash(sessionToken);
            var index = data.Sessions.FindIndex(x => x.Token == hash);
            if (index >= 0 && data.Sessions[index].RevokedAt is null)
            {
                var session = data.Sessions[index];
                data.Sessions[index] = session with { RevokedAt = DateTimeOffset.UtcNow };
                var user = data.Users.SingleOrDefault(item => item.Id == session.UserId);
                if (user is not null) AddActivity(data, user, "Logout");
                await SaveAsync(data, ct);
            }
        }
        finally { Gate.Release(); }
    }

    public async Task<bool> IsActiveAsync(long userId, string sessionToken, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct); var hash = Hash(sessionToken);
            return data.Users.Any(x => x.Id == userId && x.Enabled)
                && data.Sessions.Any(x => x.UserId == userId && x.Token == hash && x.RevokedAt is null && x.ExpiresAt > DateTimeOffset.UtcNow);
        }
        finally { Gate.Release(); }
    }

    public async Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            return data.Roles.Select(role => new RoleSummary(role.Id, role.Code, role.Name, true,
                PermissionCodes.ForRole(role.Code).Select(x => new PermissionSummary(x, x)).ToArray())).ToArray();
        }
        finally { Gate.Release(); }
    }

    public async Task<IReadOnlyCollection<UserAccessHistory>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            return data.Activities.OrderByDescending(item => item.OccurredAt).Take(Math.Clamp(count, 1, 500))
                .Select(item => new UserAccessHistory(item.Id, item.UserId, item.AliasName, item.Username, item.Action, item.OccurredAt)).ToArray();
        }
        finally { Gate.Release(); }
    }
    public async Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            return data.Users.Select(user =>
            {
                var role = data.Roles.SingleOrDefault(item => item.Id == user.RoleId);
                return new UserSummary(user.Id, user.AliasName, user.Username, user.RoleId,
                    role?.Code ?? "Unknown", role?.Name ?? "نقش حذف‌شده", user.Enabled);
            }).OrderByDescending(user => user.IsEnabled).ThenBy(user => user.AliasName).ToArray();
        }
        finally { Gate.Release(); }
    }

    public async Task<long> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        ValidateUser(command.AliasName, command.Username, command.Password);
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var username = command.Username.Trim().ToLowerInvariant();
            if (data.Users.Any(user => user.Username == username)) throw new UserManagementException("نام کاربری تکراری است.");
            var role = data.Roles.SingleOrDefault(item => item.Id == command.RoleId)
                ?? throw new UserManagementException("گروه کاربری معتبر نیست.");
            var id = data.Users.Count == 0 ? 1 : data.Users.Max(user => user.Id) + 1;
            var identity = new User(command.AliasName.Trim(), username, role.Id);
            data.Users.Add(new(id, identity.AliasName, identity.Username,
                hasher.HashPassword(identity, command.Password), role.Id, true));
            await SaveAsync(data, ct);
            return id;
        }
        finally { Gate.Release(); }
    }

    public async Task ChangeRoleAsync(long userId, long roleId, long currentUserId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var index = data.Users.FindIndex(user => user.Id == userId);
            if (index < 0) throw new UserManagementException("کاربر پیدا نشد.");
            var role = data.Roles.SingleOrDefault(item => item.Id == roleId)
                ?? throw new UserManagementException("گروه کاربری معتبر نیست.");
            var currentRole = data.Roles.SingleOrDefault(item => item.Id == data.Users[index].RoleId);
            if (currentRole?.Code == RoleCodes.CEO && role.Code != RoleCodes.CEO &&
                data.Users.Count(user => user.Enabled && data.Roles.Any(item => item.Id == user.RoleId && item.Code == RoleCodes.CEO)) <= 1)
                throw new UserManagementException("نقش آخرین مدیرعامل فعال قابل تغییر نیست.");
            data.Users[index] = data.Users[index] with { RoleId = roleId };
            RevokeUserSessions(data, userId);
            await SaveAsync(data, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task DisableUserAsync(long userId, long currentUserId, CancellationToken ct = default)
    {
        if (userId == currentUserId) throw new UserManagementException("نمی‌توانید حساب کاربری خودتان را حذف کنید.");
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var index = data.Users.FindIndex(user => user.Id == userId);
            if (index < 0) throw new UserManagementException("کاربر پیدا نشد.");
            var user = data.Users[index];
            var role = data.Roles.SingleOrDefault(item => item.Id == user.RoleId);
            if (role?.Code == RoleCodes.CEO && user.Enabled &&
                data.Users.Count(item => item.Enabled && data.Roles.Any(r => r.Id == item.RoleId && r.Code == RoleCodes.CEO)) <= 1)
                throw new UserManagementException("آخرین مدیرعامل فعال قابل حذف نیست.");
            data.Users[index] = user with { Enabled = false };
            RevokeUserSessions(data, userId);
            await SaveAsync(data, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task EnableUserAsync(long userId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var index = data.Users.FindIndex(user => user.Id == userId);
            if (index < 0) throw new UserManagementException("کاربر پیدا نشد.");
            data.Users[index] = data.Users[index] with { Enabled = true };
            await SaveAsync(data, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task ChangeOwnPasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var index = data.Users.FindIndex(user => user.Id == userId && user.Enabled);
            if (index < 0) throw new UserManagementException("کاربر فعال پیدا نشد.");
            var item = data.Users[index];
            var identity = new User(item.AliasName, item.Username, item.RoleId);
            if (hasher.VerifyHashedPassword(identity, item.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
                throw new UserManagementException("گذرواژه فعلی نادرست است.");
            data.Users[index] = item with { PasswordHash = hasher.HashPassword(identity, newPassword) };
            RevokeUserSessions(data, userId);
            await SaveAsync(data, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task ResetPasswordAsync(long userId, string newPassword, long currentAdminId, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var admin = data.Users.SingleOrDefault(user => user.Id == currentAdminId && user.Enabled);
            var adminRole = admin is null ? null : data.Roles.SingleOrDefault(role => role.Id == admin.RoleId);
            if (adminRole?.Code != RoleCodes.CEO) throw new UserManagementException("فقط مدیرعامل می‌تواند گذرواژه دیگران را بازنشانی کند.");
            var index = data.Users.FindIndex(user => user.Id == userId && user.Enabled);
            if (index < 0) throw new UserManagementException("کاربر فعال پیدا نشد.");
            var item = data.Users[index];
            var identity = new User(item.AliasName, item.Username, item.RoleId);
            data.Users[index] = item with { PasswordHash = hasher.HashPassword(identity, newPassword) };
            RevokeUserSessions(data, userId);
            await SaveAsync(data, ct);
        }
        finally { Gate.Release(); }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new UserManagementException("گذرواژه باید حداقل ۸ نویسه باشد.");
    }
    private static void ValidateUser(string aliasName, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(aliasName) || aliasName.Trim().Length > 100) throw new UserManagementException("نام فارسی کاربر معتبر نیست.");
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 100 ||
            username.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new UserManagementException("نام کاربری فقط می‌تواند شامل حروف، اعداد، نقطه، خط تیره و زیرخط باشد.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) throw new UserManagementException("گذرواژه باید حداقل ۸ نویسه باشد.");
    }

    private static void AddActivity(JsonData data, JsonUser user, string action)
    {
        var id = data.Activities.Count == 0 ? 1 : data.Activities.Max(item => item.Id) + 1;
        data.Activities.Add(new(id, user.Id, user.AliasName, user.Username, action, DateTimeOffset.UtcNow));
        if (data.Activities.Count > 2000)
            data.Activities = data.Activities.OrderByDescending(item => item.OccurredAt).Take(2000).OrderBy(item => item.Id).ToList();
    }
    private static void RevokeUserSessions(JsonData data, long userId)
    {
        for (var index = 0; index < data.Sessions.Count; index++)
            if (data.Sessions[index].UserId == userId && data.Sessions[index].RevokedAt is null)
                data.Sessions[index] = data.Sessions[index] with { RevokedAt = DateTimeOffset.UtcNow };
    }
    private static async Task<JsonData> LoadAsync(CancellationToken ct)
    {
        await using var stream = File.OpenRead(FilePath);
        var data = await JsonSerializer.DeserializeAsync<JsonData>(stream, JsonOptions, ct) ?? new JsonData();
        data.Roles.RemoveAll(role => role.Code == "WarehouseOperator");
        return data;
    }
    private static async Task SaveAsync(JsonData data, CancellationToken ct)
    {
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions, ct);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class JsonData
    {
        public List<JsonUser> Users { get; set; } = [];
        public List<JsonRole> Roles { get; set; } = [];
        public List<string> Permissions { get; set; } = [];
        public List<JsonSession> Sessions { get; set; } = [];
        public List<JsonUserActivity> Activities { get; set; } = [];
    }
    private sealed record JsonUser(long Id, string AliasName, string Username, string PasswordHash, long RoleId, bool Enabled);
    private sealed record JsonRole(long Id, string Code, string Name);
    private sealed record JsonSession(string Token, long UserId, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);
    private sealed record JsonUserActivity(long Id, long UserId, string AliasName, string Username, string Action, DateTimeOffset OccurredAt);
}

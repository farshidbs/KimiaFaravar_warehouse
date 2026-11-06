using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace KimiaFaravar.Infrastructure.JsonStorage;

internal sealed class JsonAuthenticationService(IPasswordHasher<User> hasher, IConfiguration configuration)
    : IAuthenticationService, IStorageInitializer, ISessionValidator, IAccessControlQueryService
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
            var identity = new User("مدیر سیستم", username, 1);
            var data = new JsonData
            {
                Roles =
                [
                    new(1, RoleCodes.CEO, "مدیرعامل"), new(2, RoleCodes.WarehouseManager, "مدیر انبار"),
                    new(3, RoleCodes.WarehouseOperator, "کاربر انبار"), new(4, RoleCodes.InternalSales, "همکاران فروش داخلی"),
                    new(5, RoleCodes.MarketDistribution, "همکاران توزیع بازار")
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
            await SaveAsync(data, ct);
            var permissions = role.Code == RoleCodes.CEO ? data.Permissions : [];
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
            if (index >= 0) { data.Sessions[index] = data.Sessions[index] with { RevokedAt = DateTimeOffset.UtcNow }; await SaveAsync(data, ct); }
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
                role.Code == RoleCodes.CEO ? data.Permissions.Select(x => new PermissionSummary(x, x)).ToArray() : [])).ToArray();
        }
        finally { Gate.Release(); }
    }

    private static async Task<JsonData> LoadAsync(CancellationToken ct)
    {
        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<JsonData>(stream, JsonOptions, ct) ?? new JsonData();
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
    }
    private sealed record JsonUser(long Id, string AliasName, string Username, string PasswordHash, long RoleId, bool Enabled);
    private sealed record JsonRole(long Id, string Code, string Name);
    private sealed record JsonSession(string Token, long UserId, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);
}

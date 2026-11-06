using System.Text.Json;
using KimiaFaravar.Application.Inventory;

namespace KimiaFaravar.Infrastructure.JsonStorage;

internal sealed class JsonInventoryMvpService : IInventoryMvpService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static string FilePath => Path.Combine((string?)AppDomain.CurrentDomain.GetData("DataDirectory") ?? AppContext.BaseDirectory, "inventory-operations.json");

    public async Task<InventorySnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try { return ToSnapshot(await LoadAsync(ct)); }
        finally { Gate.Release(); }
    }

    public async Task<long> CreateGroupAsync(CreateProductGroupCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Name, "نام گروه"); ValidateText(command.Code, "کد گروه");
        return await MutateAsync(data =>
        {
            var code = command.Code.Trim().ToUpperInvariant();
            if (data.Groups.Any(x => x.Code == code)) throw new InventoryValidationException("کد گروه تکراری است.");
            if (command.ParentId is not null && data.Groups.All(x => x.Id != command.ParentId)) throw new InventoryValidationException("گروه والد معتبر نیست.");
            var id = NextId(data.Groups.Select(x => x.Id)); data.Groups.Add(new(id, command.Name.Trim(), code, command.ParentId)); return id;
        }, ct);
    }

    public async Task<long> CreateSpecificationAsync(CreateSpecificationCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Name, "نام مشخصه"); ValidateText(command.Code, "کد مشخصه");
        return await MutateAsync(data =>
        {
            if (data.Groups.All(x => x.Id != command.ProductGroupId)) throw new InventoryValidationException("گروه محصول معتبر نیست.");
            var code = command.Code.Trim().ToUpperInvariant();
            if (data.Specifications.Any(x => x.ProductGroupId == command.ProductGroupId && x.Code == code)) throw new InventoryValidationException("این مشخصه قبلاً برای گروه تعریف شده است.");
            var id = NextId(data.Specifications.Select(x => x.Id)); data.Specifications.Add(new(id, command.ProductGroupId, command.Name.Trim(), code, command.DataType, command.IsRequired, command.IsVariantKey)); return id;
        }, ct);
    }

    public async Task<long> CreateProductAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Name, "نام محصول"); ValidateText(command.Sku, "SKU"); ValidateText(command.BaseUnit, "واحد پایه");
        return await MutateAsync(data =>
        {
            if (data.Groups.All(x => x.Id != command.ProductGroupId)) throw new InventoryValidationException("گروه محصول معتبر نیست.");
            var sku = command.Sku.Trim().ToUpperInvariant();
            if (data.Products.Any(x => x.Sku == sku)) throw new InventoryValidationException("SKU تکراری است.");
            var id = NextId(data.Products.Select(x => x.Id)); data.Products.Add(new(id, command.ProductGroupId, sku, command.Name.Trim(), command.BaseUnit.Trim(), 0)); return id;
        }, ct);
    }

    public Task PostReceiptAsync(PostMovementCommand command, CancellationToken ct = default) => PostAsync(command, MovementType.Receipt, ct);
    public Task PostDispatchAsync(PostMovementCommand command, CancellationToken ct = default) => PostAsync(command, MovementType.Dispatch, ct);

    private static async Task PostAsync(PostMovementCommand command, MovementType type, CancellationToken ct)
    {
        if (command.Quantity <= 0) throw new InventoryValidationException("مقدار باید بزرگ‌تر از صفر باشد.");
        await MutateAsync<object?>(data =>
        {
            var index = data.Products.FindIndex(x => x.Id == command.ProductId);
            if (index < 0) throw new InventoryValidationException("محصول معتبر نیست.");
            var product = data.Products[index]; var delta = type == MovementType.Receipt ? command.Quantity : -command.Quantity;
            if (product.Quantity + delta < 0) throw new InventoryValidationException("موجودی برای این خروج کافی نیست.");
            data.Products[index] = product with { Quantity = product.Quantity + delta };
            data.Movements.Add(new(NextId(data.Movements.Select(x => x.Id)), product.Id, type, command.Quantity, product.BaseUnit, command.Reason?.Trim(), DateTimeOffset.UtcNow));
            return null;
        }, ct);
    }

    private static async Task<T> MutateAsync<T>(Func<JsonInventoryData, T> action, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { var data = await LoadAsync(ct); var result = action(data); await SaveAsync(data, ct); return result; }
        finally { Gate.Release(); }
    }
    private static InventorySnapshot ToSnapshot(JsonInventoryData d) => new(d.Groups.Select(x => new ProductGroupDto(x.Id, x.Name, x.Code, x.ParentId)).ToArray(), d.Specifications.Select(x => new SpecificationDto(x.Id, x.ProductGroupId, x.Name, x.Code, x.DataType, x.IsRequired, x.IsVariantKey)).ToArray(), d.Products.Select(x => new ProductDto(x.Id, x.ProductGroupId, x.Sku, x.Name, x.BaseUnit, x.Quantity)).ToArray(), d.Movements.OrderByDescending(x => x.CreatedAt).Select(x => { var p = d.Products.Single(p => p.Id == x.ProductId); return new StockMovementDto(x.Id, x.ProductId, p.Name, x.Type, x.Quantity, x.Unit, x.Reason, x.CreatedAt); }).ToArray());
    private static async Task<JsonInventoryData> LoadAsync(CancellationToken ct) { if (!File.Exists(FilePath)) return new(); await using var s = File.OpenRead(FilePath); return await JsonSerializer.DeserializeAsync<JsonInventoryData>(s, Options, ct) ?? new(); }
    private static async Task SaveAsync(JsonInventoryData data, CancellationToken ct) { var temp = FilePath + ".tmp"; await using (var s = File.Create(temp)) await JsonSerializer.SerializeAsync(s, data, Options, ct); File.Move(temp, FilePath, true); }
    private static long NextId(IEnumerable<long> values) => values.DefaultIfEmpty().Max() + 1;
    private static void ValidateText(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new InventoryValidationException($"{name} الزامی است."); }

    private sealed class JsonInventoryData { public List<JsonGroup> Groups { get; set; } = []; public List<JsonSpecification> Specifications { get; set; } = []; public List<JsonProduct> Products { get; set; } = []; public List<JsonMovement> Movements { get; set; } = []; }
    private sealed record JsonGroup(long Id, string Name, string Code, long? ParentId);
    private sealed record JsonSpecification(long Id, long ProductGroupId, string Name, string Code, SpecificationDataType DataType, bool IsRequired, bool IsVariantKey);
    private sealed record JsonProduct(long Id, long ProductGroupId, string Sku, string Name, string BaseUnit, decimal Quantity);
    private sealed record JsonMovement(long Id, long ProductId, MovementType Type, decimal Quantity, string Unit, string? Reason, DateTimeOffset CreatedAt);
}

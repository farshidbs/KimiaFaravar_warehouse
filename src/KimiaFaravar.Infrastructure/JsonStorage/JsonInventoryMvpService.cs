using System.Globalization;
using System.Text.Json;
using KimiaFaravar.Application.Inventory;

namespace KimiaFaravar.Infrastructure.JsonStorage;

// Temporary single-process JSON store. SQL Server migration must replace the storage adapter.
internal sealed class JsonInventoryMvpService : IInventoryMvpService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static string FilePath => Path.Combine(
        (string?)AppDomain.CurrentDomain.GetData("DataDirectory") ?? AppContext.BaseDirectory,
        "inventory-operations.json");

    public async Task<InventorySnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try { return ToSnapshot(await LoadAsync(ct)); }
        finally { Gate.Release(); }
    }

    public async Task<IReadOnlyCollection<EffectiveSpecificationDto>> GetEffectiveSpecificationsAsync(long groupId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            return ResolveSpecifications(data, groupId)
                .Select(s => new EffectiveSpecificationDto(s.Id, s.Name, s.Code, s.DataType, s.IsRequired,
                    s.IsVariantKey, data.SpecificationOptions.Where(o => o.SpecificationId == s.Id)
                        .Select(o => new SpecificationOptionDto(o.Id, o.SpecificationId, o.Value)).ToArray()))
                .ToArray();
        }
        finally { Gate.Release(); }
    }

    public Task<long> CreateGroupAsync(CreateProductGroupCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Name, "نام گروه");
        ValidateText(command.Code, "کد گروه");
        return MutateAsync(data =>
        {
            var code = command.Code.Trim().ToUpperInvariant();
            if (data.Groups.Any(g => g.Code == code)) throw new InventoryValidationException("کد گروه تکراری است.");
            if (command.ParentId is not null && data.Groups.All(g => g.Id != command.ParentId.Value))
                throw new InventoryValidationException("گروه والد وجود ندارد.");
            var id = NextId(data.Groups.Select(g => g.Id));
            data.Groups.Add(new(id, command.Name.Trim(), code, command.ParentId, command.VisibleToInternalSales, command.VisibleToMarketDistribution));
            return id;
        }, ct);
    }

    public async Task UpdateGroupVisibilityAsync(long groupId, bool visibleToInternalSales, bool visibleToMarketDistribution, CancellationToken ct = default)
    {
        await MutateAsync<object?>(data =>
        {
            var index = data.Groups.FindIndex(group => group.Id == groupId);
            if (index < 0) throw new InventoryValidationException("گروه کالا وجود ندارد.");
            data.Groups[index] = data.Groups[index] with
            {
                VisibleToInternalSales = visibleToInternalSales,
                VisibleToMarketDistribution = visibleToMarketDistribution
            };
            return null;
        }, ct);
    }

    public Task<long> CreateSpecificationAsync(CreateSpecificationCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Name, "نام مشخصه");
        ValidateText(command.Code, "کد مشخصه");
        if (!Enum.IsDefined(command.DataType)) throw new InventoryValidationException("نوع مشخصه نامعتبر است.");
        return MutateAsync(data =>
        {
            var inherited = ResolveSpecifications(data, command.ProductGroupId);
            var code = command.Code.Trim().ToUpperInvariant();
            if (inherited.Any(s => s.Code == code))
                throw new InventoryValidationException("این مشخصه در گروه یا گروه والد قبلاً تعریف شده است.");
            var id = NextId(data.Specifications.Select(s => s.Id));
            data.Specifications.Add(new(id, command.ProductGroupId, command.Name.Trim(), code,
                command.DataType, command.IsRequired, command.IsVariantKey));
            return id;
        }, ct);
    }

    public Task<long> CreateSpecificationOptionAsync(CreateSpecificationOptionCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Value, "مقدار مشخصه");
        return MutateAsync(data =>
        {
            var spec = data.Specifications.SingleOrDefault(s => s.Id == command.SpecificationId);
            if (spec is null || spec.DataType != SpecificationDataType.Select)
                throw new InventoryValidationException("مقدار انتخابی باید به مشخصه از نوع لیست وابسته باشد.");
            var value = command.Value.Trim();
            if (data.SpecificationOptions.Any(o => o.SpecificationId == spec.Id &&
                string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)))
                throw new InventoryValidationException("این گزینه قبلاً تعریف شده است.");
            var id = NextId(data.SpecificationOptions.Select(o => o.Id));
            data.SpecificationOptions.Add(new(id, spec.Id, value));
            return id;
        }, ct);
    }

    public Task<long> CreateProductAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        ValidateText(command.Name, "نام کالا");
        ValidateText(command.Sku, "کد کالا");
        ValidateText(command.BaseUnit, "واحد پایه");
        return MutateAsync(data =>
        {
            var specs = ResolveSpecifications(data, command.ProductGroupId);
            var received = command.SpecificationValues ?? new Dictionary<long, string>();
            if (received.Keys.Except(specs.Select(s => s.Id)).Any())
                throw new InventoryValidationException("یکی از مشخصات ارسالی متعلق به گروه کالا نیست.");
            var normalized = new Dictionary<long, string>();
            foreach (var spec in specs)
            {
                received.TryGetValue(spec.Id, out var raw);
                var value = NormalizeValue(data, spec, raw);
                if (spec.IsRequired && value is null)
                    throw new InventoryValidationException($"مقدار مشخصه «{spec.Name}» الزامی است.");
                if (value is not null) normalized.Add(spec.Id, value);
            }
            var sku = command.Sku.Trim().ToUpperInvariant();
            if (data.Products.Any(p => p.Sku == sku))
                throw new InventoryValidationException("کد کالا تکراری است.");
            var variantKeys = specs.Where(s => s.IsVariantKey).Select(s => s.Id).OrderBy(id => id).ToArray();
            if (variantKeys.Length > 0 && data.Products.Where(p => p.ProductGroupId == command.ProductGroupId)
                .Any(p => variantKeys.All(id =>
                    string.Equals(data.ProductValues.FirstOrDefault(v => v.ProductId == p.Id && v.SpecificationId == id)?.Value,
                        normalized.GetValueOrDefault(id), StringComparison.OrdinalIgnoreCase))))
                throw new InventoryValidationException("کالایی با همین ترکیب مشخصات کلیدی قبلاً ثبت شده است.");

            var productId = NextId(data.Products.Select(p => p.Id));
            data.Products.Add(new(productId, command.ProductGroupId, sku, command.Name.Trim(), command.BaseUnit.Trim(), 0,
                command.VisibleToInternalSales, command.VisibleToMarketDistribution));
            foreach (var (specId, value) in normalized)
                data.ProductValues.Add(new(productId, specId, value));
            return productId;
        }, ct);
    }

    public async Task UpdateProductVisibilityAsync(long productId, bool visibleToInternalSales, bool visibleToMarketDistribution, CancellationToken ct = default)
    {
        await MutateAsync<object?>(data =>
        {
            var index = data.Products.FindIndex(product => product.Id == productId);
            if (index < 0) throw new InventoryValidationException("کالا وجود ندارد.");
            data.Products[index] = data.Products[index] with
            {
                VisibleToInternalSales = visibleToInternalSales,
                VisibleToMarketDistribution = visibleToMarketDistribution
            };
            return null;
        }, ct);
    }

    public Task PostReceiptAsync(PostMovementCommand command, CancellationToken ct = default) => PostAsync(command, MovementType.Receipt, ct);
    public Task PostDispatchAsync(PostMovementCommand command, CancellationToken ct = default) => PostAsync(command, MovementType.Dispatch, ct);

    private static Task<object?> PostAsync(PostMovementCommand command, MovementType type, CancellationToken ct)
    {
        if (command.Quantity <= 0) throw new InventoryValidationException("مقدار باید بزرگ‌تر از صفر باشد.");
        return MutateAsync<object?>(data =>
        {
            var index = data.Products.FindIndex(p => p.Id == command.ProductId);
            if (index < 0) throw new InventoryValidationException("کالا وجود ندارد.");
            var product = data.Products[index];
            var delta = type == MovementType.Receipt ? command.Quantity : -command.Quantity;
            if (product.Quantity + delta < 0) throw new InventoryValidationException("موجودی برای این خروج کافی نیست.");
            data.Products[index] = product with { Quantity = product.Quantity + delta };
            data.Movements.Add(new(NextId(data.Movements.Select(m => m.Id)), product.Id, type,
                command.Quantity, product.BaseUnit, command.Reason?.Trim(), DateTimeOffset.UtcNow));
            return null;
        }, ct);
    }

    private static string? NormalizeValue(JsonInventoryData data, JsonSpecification spec, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        switch (spec.DataType)
        {
            case SpecificationDataType.Text:
                if (value.Length > 300) throw new InventoryValidationException($"مقدار {spec.Name} بیش از حد طولانی است.");
                return value.Normalize().ToUpperInvariant();
            case SpecificationDataType.Integer:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    throw new InventoryValidationException($"مقدار {spec.Name} باید عدد صحیح باشد.");
                return number.ToString(CultureInfo.InvariantCulture);
            case SpecificationDataType.Decimal:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
                    throw new InventoryValidationException($"مقدار {spec.Name} باید عدد اعشاری معتبر باشد.");
                return dec.ToString("G29", CultureInfo.InvariantCulture);
            case SpecificationDataType.Boolean:
                if (value is "true" or "1") return "true";
                if (value is "false" or "0") return "false";
                throw new InventoryValidationException($"مقدار {spec.Name} باید بله یا خیر باشد.");
            case SpecificationDataType.Select:
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var optionId) ||
                    !data.SpecificationOptions.Any(o => o.Id == optionId && o.SpecificationId == spec.Id))
                    throw new InventoryValidationException($"گزینه انتخاب‌شده برای {spec.Name} معتبر نیست.");
                return optionId.ToString(CultureInfo.InvariantCulture);
            default:
                throw new InventoryValidationException("نوع مشخصه نامعتبر است.");
        }
    }

    private static JsonSpecification[] ResolveSpecifications(JsonInventoryData data, long groupId)
    {
        var groups = new List<JsonGroup>();
        var visited = new HashSet<long>();
        long? current = groupId;
        while (current is not null)
        {
            if (!visited.Add(current.Value)) throw new InventoryValidationException("چرخه در گروه‌بندی کالا شناسایی شد.");
            var group = data.Groups.SingleOrDefault(g => g.Id == current.Value)
                ?? throw new InventoryValidationException("گروه کالا وجود ندارد.");
            groups.Add(group);
            current = group.ParentId;
        }
        groups.Reverse();
        return groups.SelectMany(g => data.Specifications.Where(s => s.ProductGroupId == g.Id)).ToArray();
    }

    private static InventorySnapshot ToSnapshot(JsonInventoryData d) => new(
        d.Groups.Select(x => new ProductGroupDto(x.Id, x.Name, x.Code, x.ParentId, x.VisibleToInternalSales, x.VisibleToMarketDistribution)).ToArray(),
        d.Specifications.Select(x => new SpecificationDto(x.Id, x.ProductGroupId, x.Name, x.Code, x.DataType, x.IsRequired, x.IsVariantKey)).ToArray(),
        d.Products.Select(x => new ProductDto(x.Id, x.ProductGroupId, x.Sku, x.Name, x.BaseUnit, x.Quantity,
            x.VisibleToInternalSales, x.VisibleToMarketDistribution)).ToArray(),
        d.Movements.OrderByDescending(x => x.CreatedAt).Select(x =>
        {
            var product = d.Products.SingleOrDefault(p => p.Id == x.ProductId);
            return new StockMovementDto(x.Id, x.ProductId, product?.Name ?? "کالای حذف‌شده", x.Type, x.Quantity, x.Unit, x.Reason, x.CreatedAt);
        }).ToArray(),
        d.SpecificationOptions.Select(x => new SpecificationOptionDto(x.Id, x.SpecificationId, x.Value)).ToArray(),
        d.ProductValues.Select(x => new ProductSpecificationValueDto(x.ProductId, x.SpecificationId, x.Value)).ToArray());

    private static async Task<T> MutateAsync<T>(Func<JsonInventoryData, T> action, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var data = await LoadAsync(ct);
            var result = action(data);
            await SaveAsync(data, ct);
            return result;
        }
        finally { Gate.Release(); }
    }

    private static async Task<JsonInventoryData> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath)) return new();
        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<JsonInventoryData>(stream, Options, ct) ?? new();
    }

    private static async Task SaveAsync(JsonInventoryData data, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, data, Options, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static long NextId(IEnumerable<long> values) => values.DefaultIfEmpty().Max() + 1;
    private static void ValidateText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InventoryValidationException($"{name} الزامی است.");
    }

    private sealed class JsonInventoryData
    {
        public List<JsonGroup> Groups { get; set; } = [];
        public List<JsonSpecification> Specifications { get; set; } = [];
        public List<JsonSpecificationOption> SpecificationOptions { get; set; } = [];
        public List<JsonProduct> Products { get; set; } = [];
        public List<JsonProductValue> ProductValues { get; set; } = [];
        public List<JsonMovement> Movements { get; set; } = [];
    }
    private sealed record JsonGroup(long Id, string Name, string Code, long? ParentId, bool VisibleToInternalSales = false, bool VisibleToMarketDistribution = false);
    private sealed record JsonSpecification(long Id, long ProductGroupId, string Name, string Code,
        SpecificationDataType DataType, bool IsRequired, bool IsVariantKey);
    private sealed record JsonSpecificationOption(long Id, long SpecificationId, string Value);
    private sealed record JsonProduct(long Id, long ProductGroupId, string Sku, string Name, string BaseUnit, decimal Quantity,
        bool VisibleToInternalSales = true, bool VisibleToMarketDistribution = true);
    private sealed record JsonProductValue(long ProductId, long SpecificationId, string Value);
    private sealed record JsonMovement(long Id, long ProductId, MovementType Type, decimal Quantity, string Unit, string? Reason, DateTimeOffset CreatedAt);
}

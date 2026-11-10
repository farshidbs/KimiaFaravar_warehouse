namespace KimiaFaravar.Application.Inventory;

public enum SpecificationDataType { Text, Decimal, Integer, Boolean, Select }
public enum MovementType { Receipt, Dispatch }

public sealed record ProductGroupDto(long Id, string Name, string Code, long? ParentId, bool VisibleToInternalSales, bool VisibleToMarketDistribution);
public sealed record SpecificationDto(long Id, long ProductGroupId, string Name, string Code, SpecificationDataType DataType, bool IsRequired, bool IsVariantKey);
public sealed record SpecificationOptionDto(long Id, long SpecificationId, string Value);
public sealed record ProductSpecificationValueDto(long ProductId, long SpecificationId, string Value);
public sealed record ProductDto(long Id, long ProductGroupId, string Sku, string Name, string BaseUnit, decimal Quantity,
    bool VisibleToInternalSales = true, bool VisibleToMarketDistribution = true);
public sealed record StockMovementDto(long Id, long ProductId, string ProductName, MovementType Type, decimal Quantity, string Unit, string? Reason, DateTimeOffset CreatedAt);
public sealed record InventorySnapshot(
    IReadOnlyCollection<ProductGroupDto> Groups,
    IReadOnlyCollection<SpecificationDto> Specifications,
    IReadOnlyCollection<ProductDto> Products,
    IReadOnlyCollection<StockMovementDto> Movements,
    IReadOnlyCollection<SpecificationOptionDto> SpecificationOptions,
    IReadOnlyCollection<ProductSpecificationValueDto> ProductValues);

public sealed record CreateProductGroupCommand(string Name, string Code, long? ParentId, bool VisibleToInternalSales, bool VisibleToMarketDistribution);
public sealed record CreateSpecificationCommand(long ProductGroupId, string Name, string Code, SpecificationDataType DataType, bool IsRequired, bool IsVariantKey);
public sealed record CreateSpecificationOptionCommand(long SpecificationId, string Value);
public sealed record CreateProductCommand(long ProductGroupId, string Sku, string Name, string BaseUnit, IReadOnlyDictionary<long, string>? SpecificationValues = null,
    bool VisibleToInternalSales = true, bool VisibleToMarketDistribution = true);
public sealed record PostMovementCommand(long ProductId, decimal Quantity, string? Reason);
public sealed record EffectiveSpecificationDto(
    long Id, string Name, string Code, SpecificationDataType DataType,
    bool IsRequired, bool IsVariantKey, IReadOnlyCollection<SpecificationOptionDto> Options);

public interface IInventoryMvpService
{
    Task<InventorySnapshot> GetSnapshotAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<EffectiveSpecificationDto>> GetEffectiveSpecificationsAsync(long groupId, CancellationToken ct = default);
    Task<long> CreateGroupAsync(CreateProductGroupCommand command, CancellationToken ct = default);
    Task UpdateGroupVisibilityAsync(long groupId, bool visibleToInternalSales, bool visibleToMarketDistribution, CancellationToken ct = default);
    Task<long> CreateSpecificationAsync(CreateSpecificationCommand command, CancellationToken ct = default);
    Task<long> CreateSpecificationOptionAsync(CreateSpecificationOptionCommand command, CancellationToken ct = default);
    Task<long> CreateProductAsync(CreateProductCommand command, CancellationToken ct = default);
    Task UpdateProductVisibilityAsync(long productId, bool visibleToInternalSales, bool visibleToMarketDistribution, CancellationToken ct = default);
    Task PostReceiptAsync(PostMovementCommand command, CancellationToken ct = default);
    Task PostDispatchAsync(PostMovementCommand command, CancellationToken ct = default);
}

public sealed class InventoryValidationException(string message) : Exception(message);

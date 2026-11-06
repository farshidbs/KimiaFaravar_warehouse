using KimiaFaravar.Domain.Identity;
namespace KimiaFaravar.Domain.Auditing;
public sealed class AuditLog : Entity
{
    private AuditLog() { }
    public AuditLog(long? userId, string action, string entityName, string? entityId, string? oldValues, string? newValues)
    { UserId = userId; Action = action; EntityName = entityName; EntityId = entityId; OldValues = oldValues; NewValues = newValues; CreatedAt = DateTimeOffset.UtcNow; }
    public long? UserId { get; private set; }
    public string Action { get; private set; } = "";
    public string EntityName { get; private set; } = "";
    public string? EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

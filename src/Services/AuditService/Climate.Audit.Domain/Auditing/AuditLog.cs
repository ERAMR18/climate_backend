namespace Climate.Audit.Domain.Auditing;

public sealed class AuditLog
{
    private AuditLog() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string? ResourceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    public static AuditLog Create(Guid id, Guid userId, string userName, string action, string resource,
        string? resourceId, string description, string? ipAddress, DateTimeOffset timestamp)
    {
        if (id == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Identifiers cannot be empty.");
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(action) ||
            string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Audit text fields are required.");
        return new AuditLog { Id=id, UserId=userId, UserName=userName.Trim(), Action=action.Trim(),
            Resource=resource.Trim(), ResourceId=resourceId, Description=description.Trim(),
            IpAddress=ipAddress, Timestamp=timestamp };
    }
}

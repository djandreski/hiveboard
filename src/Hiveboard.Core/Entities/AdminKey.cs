namespace Hiveboard.Core.Entities;

public class AdminKey
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

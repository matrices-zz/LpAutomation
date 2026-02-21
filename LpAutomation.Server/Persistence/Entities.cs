using System;

namespace LpAutomation.Server.Persistence;

public sealed class ConfigVersionEntity
{
    public long Id { get; set; }
    public Guid ConfigId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string CreatedBy { get; set; } = "unknown";
    public string ConfigJson { get; set; } = "";
    public string ConfigHash { get; set; } = "";
}

public sealed class AuditEventEntity
{
    public long Id { get; set; }
    public DateTimeOffset Utc { get; set; }
    public string Actor { get; set; } = "unknown";
    public string EventType { get; set; } = "";
    public string DetailsJson { get; set; } = "";
}

namespace NoteSphere.Api.Models;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? NoteId { get; set; }
    public string Action { get; set; } = "";
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

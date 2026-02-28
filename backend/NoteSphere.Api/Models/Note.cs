namespace NoteSphere.Api.Models;

public sealed class Note
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";

    // Stored as JSON array string like ["work","ideas"]
    public string TagsJson { get; set; } = "[]";

    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

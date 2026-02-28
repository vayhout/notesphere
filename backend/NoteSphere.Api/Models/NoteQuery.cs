namespace NoteSphere.Api.Models;

public sealed class NoteQuery
{
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "updatedAt"; // title | createdAt | updatedAt
    public string? SortDir { get; set; } = "desc";     // asc | desc

    public bool? Pinned { get; set; }
    public bool? Archived { get; set; }
    public string? Tag { get; set; }

    public int? Page { get; set; } = 1;
    public int? PageSize { get; set; } = 20;

    // Trash support
    // includeDeleted = true means include soft-deleted notes in results
    // onlyDeleted = true means return ONLY soft-deleted notes (Trash view)
    public bool? IncludeDeleted { get; set; } = false;
    public bool? OnlyDeleted { get; set; } = false;
}

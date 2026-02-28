namespace NoteSphere.Api.Dtos;

public sealed record NoteCreateRequest(string Title, string Content, List<string>? Tags, bool? IsPinned, bool? IsArchived);
public sealed record NoteUpdateRequest(string Title, string Content, List<string>? Tags, bool? IsPinned, bool? IsArchived);

public sealed record NoteDto(
    Guid Id,
    string Title,
    string Content,
    List<string> Tags,
    bool IsPinned,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

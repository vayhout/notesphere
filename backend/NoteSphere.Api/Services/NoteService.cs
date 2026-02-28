using System.Text.Json;
using NoteSphere.Api.Data;
using NoteSphere.Api.Dtos;
using NoteSphere.Api.Models;

namespace NoteSphere.Api.Services;

public sealed class NoteService
{
    private readonly NoteRepository _repo;
    private readonly AuditRepository _audit;

    public NoteService(NoteRepository repo, AuditRepository audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<PagedResult<NoteDto>> SearchAsync(Guid userId, NoteQuery query)
    {
        var res = await _repo.SearchAsync(userId, query);
        return new PagedResult<NoteDto>(res.Items.Select(Map).ToList(), res.Total, res.Page, res.PageSize);
    }

    public async Task<DashboardStatsDto> GetStatsAsync(Guid userId)
    {
        return await _repo.GetStatsAsync(userId);
    }

    public async Task<NoteDto> GetAsync(Guid userId, Guid noteId)
    {
        var note = await _repo.GetByIdAsync(noteId, userId);
        if (note is null) throw new KeyNotFoundException("Note not found.");
        return Map(note);
    }

    public async Task<NoteDto> CreateAsync(Guid userId, NoteCreateRequest req, AuditContext audit)
    {
        Validate(req.Title, req.Content);

        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = req.Title.Trim(),
            Content = req.Content,
            TagsJson = JsonSerializer.Serialize((req.Tags ?? new List<string>()).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)),
            IsPinned = req.IsPinned ?? false,
            IsArchived = req.IsArchived ?? false,
            IsDeleted = false,
            DeletedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.CreateAsync(note);

        await _audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NoteId = note.Id,
            Action = "NoteCreated",
            Ip = audit.Ip,
            UserAgent = audit.UserAgent,
            MetadataJson = JsonSerializer.Serialize(new { note.Title, Tags = req.Tags ?? new List<string>(), note.IsPinned, note.IsArchived }),
            CreatedAt = DateTime.UtcNow
        });

        return Map(note);
    }

    public async Task<NoteDto> UpdateAsync(Guid userId, Guid noteId, NoteUpdateRequest req, AuditContext audit)
    {
        Validate(req.Title, req.Content);

        var note = await _repo.GetByIdAsync(noteId, userId);
        if (note is null) throw new KeyNotFoundException("Note not found.");

        note.Title = req.Title.Trim();
        note.Content = req.Content;
        note.TagsJson = JsonSerializer.Serialize((req.Tags ?? new List<string>()).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
        note.IsPinned = req.IsPinned ?? note.IsPinned;
        note.IsArchived = req.IsArchived ?? note.IsArchived;
        note.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(note);

        await _audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NoteId = note.Id,
            Action = "NoteUpdated",
            Ip = audit.Ip,
            UserAgent = audit.UserAgent,
            MetadataJson = JsonSerializer.Serialize(new { note.Title, Tags = req.Tags ?? new List<string>(), note.IsPinned, note.IsArchived }),
            CreatedAt = DateTime.UtcNow
        });

        return Map(note);
    }


public async Task RestoreAsync(Guid userId, Guid noteId, AuditContext audit)
{
    await _repo.RestoreAsync(noteId, userId);

    await _audit.AddAsync(new AuditLog
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        NoteId = noteId,
        Action = "NoteRestored",
        Ip = audit.Ip,
        UserAgent = audit.UserAgent,
        MetadataJson = "{}",
        CreatedAt = DateTime.UtcNow
    });
}

public async Task PurgeAsync(Guid userId, Guid noteId, AuditContext audit)
{
    await _repo.PurgeAsync(noteId, userId);

    await _audit.AddAsync(new AuditLog
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        NoteId = noteId,
        Action = "NotePurged",
        Ip = audit.Ip,
        UserAgent = audit.UserAgent,
        MetadataJson = "{}",
        CreatedAt = DateTime.UtcNow
    });
}
    public async Task DeleteAsync(Guid userId, Guid noteId, AuditContext audit)
    {
        await _repo.SoftDeleteAsync(noteId, userId);

        await _audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NoteId = noteId,
            Action = "NoteSoftDeleted",
            Ip = audit.Ip,
            UserAgent = audit.UserAgent,
            MetadataJson = "{}",
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void Validate(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Title is required and must be <= 200 characters.");
        if (content is null) throw new ArgumentException("Content is required.");
    }

    private static NoteDto Map(Note note)
    {
        var tags = new List<string>();
        try
        {
            tags = JsonSerializer.Deserialize<List<string>>(note.TagsJson ?? "[]") ?? new List<string>();
        }
        catch { /* ignore */ }

        // SQL Server stores these columns as datetime2 (no timezone). We treat them as UTC.
        // If DateTimeKind is left as Unspecified, System.Text.Json serializes without a trailing 'Z',
        // and browsers will interpret it as LOCAL time (wrong for most users).
        var createdUtc = DateTime.SpecifyKind(note.CreatedAt, DateTimeKind.Utc);
        var updatedUtc = DateTime.SpecifyKind(note.UpdatedAt, DateTimeKind.Utc);

        return new NoteDto(
            note.Id,
            note.Title,
            note.Content,
            tags,
            note.IsPinned,
            note.IsArchived,
            createdUtc,
            updatedUtc);
    }
}

public sealed record AuditContext(string? Ip, string? UserAgent);

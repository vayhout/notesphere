using Dapper;
using Microsoft.Data.SqlClient;
using NoteSphere.Api.Dtos;
using NoteSphere.Api.Models;

namespace NoteSphere.Api.Data;

public sealed class NoteRepository
{
    private readonly SqlConnectionFactory _factory;

    public NoteRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Note?> GetByIdAsync(Guid noteId, Guid userId)
    {
        const string sql = @"SELECT TOP 1 * FROM dbo.Notes WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0;";
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Note>(sql, new { Id = noteId, UserId = userId });
    }

    public async Task<PagedResult<Note>> SearchAsync(Guid userId, NoteQuery query)
    {
        // basic protection: allowed sort columns only
        var sort = query.SortBy switch
        {
            "title" => "Title",
            "createdAt" => "CreatedAt",
            "updatedAt" => "UpdatedAt",
            _ => "UpdatedAt"
        };
        var direction = query.SortDir?.ToLowerInvariant() == "asc" ? "ASC" : "DESC";

        var where = new List<string> { "UserId = @UserId" };

        var onlyDeleted = query.OnlyDeleted == true;
        var includeDeleted = query.IncludeDeleted == true;
        if (onlyDeleted) where.Add("IsDeleted = 1");
        else if (!includeDeleted) where.Add("IsDeleted = 0");

        // filters
        if (query.Pinned.HasValue) where.Add("IsPinned = @Pinned");
        if (query.Archived.HasValue) where.Add("IsArchived = @Archived");

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            // Tags stored as JSON array string; quick filter by contains (good enough for interview/demo)
            // For production you'd normalize tags into a join table.
            where.Add("TagsJson LIKE @TagLike");
        }

        // search (Full-Text preferred)
        var hasSearch = !string.IsNullOrWhiteSpace(query.Search);
        var whereSql = "WHERE " + string.Join(" AND ", where);

        var pageSize = Math.Clamp(query.PageSize ?? 20, 5, 100);
        var page = Math.Max(query.Page ?? 1, 1);
        var offset = (page - 1) * pageSize;

        using var conn = _factory.Create();

        // params
        var like = $"%{query.Search?.Trim()}%";

        // safer than escaping backslashes; searches for "tag" inside JSON string
        var tag = query.Tag?.Trim() ?? "";
        var tagLike = "%\"" + tag + "\"%";

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);
        parameters.Add("Like", like);
        parameters.Add("TagLike", tagLike);
        parameters.Add("Pinned", query.Pinned);
        parameters.Add("Archived", query.Archived);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        // Try Full-Text search using CONTAINS; if not available, fall back to LIKE.
        if (hasSearch)
        {
            var ft = BuildFullTextQuery(query.Search!.Trim());
            parameters.Add("Ft", ft);
        }

        string sqlCountBase = $@"SELECT COUNT(1) FROM dbo.Notes {whereSql}";
        string sqlPageBase = $@"
SELECT *
FROM dbo.Notes
{whereSql}
ORDER BY {sort} {direction}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        if (!hasSearch)
        {
            var total0 = await conn.ExecuteScalarAsync<int>(sqlCountBase + ";", parameters);
            var items0 = (await conn.QueryAsync<Note>(sqlPageBase, parameters)).ToList();
            return new PagedResult<Note>(items0, total0, page, pageSize);
        }

        // Full-text attempt
        try
        {
            var whereFt = whereSql + " AND CONTAINS((Title, Content), @Ft)";
            var sqlCountFt = $@"SELECT COUNT(1) FROM dbo.Notes {whereFt};";
            var sqlFt = $@"
SELECT *
FROM dbo.Notes
{whereFt}
ORDER BY {sort} {direction}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var total = await conn.ExecuteScalarAsync<int>(sqlCountFt, parameters);
            var items = (await conn.QueryAsync<Note>(sqlFt, parameters)).ToList();
            return new PagedResult<Note>(items, total, page, pageSize);
        }
        catch (SqlException ex) when (ex.Number is 7601 or 7691 or 7646)
        {
            // Full-text not installed/index missing: fallback to LIKE
            var whereLike = whereSql + " AND (Title LIKE @Like OR Content LIKE @Like)";
            var sqlCountLike = $@"SELECT COUNT(1) FROM dbo.Notes {whereLike};";
            var sqlLike = $@"
SELECT *
FROM dbo.Notes
{whereLike}
ORDER BY {sort} {direction}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var total = await conn.ExecuteScalarAsync<int>(sqlCountLike, parameters);
            var items = (await conn.QueryAsync<Note>(sqlLike, parameters)).ToList();
            return new PagedResult<Note>(items, total, page, pageSize);
        }
    }

    public async Task<DashboardStatsDto> GetStatsAsync(Guid userId)
    {
        const string sql = @"
;WITH Base AS (
  SELECT *
  FROM dbo.Notes
  WHERE UserId = @UserId AND IsDeleted = 0
)
SELECT
  (SELECT COUNT(1) FROM Base) AS TotalNotes,
  (SELECT COUNT(1) FROM Base WHERE IsArchived = 0) AS ActiveNotes,
  (SELECT COUNT(1) FROM Base WHERE IsPinned = 1 AND IsArchived = 0) AS PinnedNotes,
  (SELECT COUNT(1) FROM Base WHERE IsArchived = 1) AS ArchivedNotes,
  (SELECT COUNT(1) FROM dbo.Notes WHERE UserId = @UserId AND IsDeleted = 1) AS DeletedNotes,
  (SELECT COUNT(DISTINCT value) FROM Base CROSS APPLY OPENJSON(TagsJson)) AS TagsUsed,
  (SELECT COUNT(1) FROM Base WHERE UpdatedAt >= DATEADD(day, -7, SYSUTCDATETIME())) AS UpdatedLast7Days;
";
        using var conn = _factory.Create();
        return await conn.QuerySingleAsync<DashboardStatsDto>(sql, new { UserId = userId });
    }

    public async Task CreateAsync(Note note)
    {
        const string sql = @"
INSERT INTO dbo.Notes
(Id, UserId, Title, Content, TagsJson, IsPinned, IsArchived, IsDeleted, DeletedAt, CreatedAt, UpdatedAt)
VALUES
(@Id, @UserId, @Title, @Content, @TagsJson, @IsPinned, @IsArchived, @IsDeleted, @DeletedAt, @CreatedAt, @UpdatedAt);";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, note);
    }

    public async Task UpdateAsync(Note note)
    {
        const string sql = @"
UPDATE dbo.Notes
SET Title = @Title,
    Content = @Content,
    TagsJson = @TagsJson,
    IsPinned = @IsPinned,
    IsArchived = @IsArchived,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0;";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, note);
    }

    public async Task RestoreAsync(Guid noteId, Guid userId)
    {
        const string sql = @"
UPDATE dbo.Notes
SET IsDeleted = 0,
    DeletedAt = NULL,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 1;";
        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, new { Id = noteId, UserId = userId });
    }

    public async Task PurgeAsync(Guid noteId, Guid userId)
    {
        const string sql = @"DELETE FROM dbo.Notes WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 1;";
        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, new { Id = noteId, UserId = userId });
    }

    public async Task<int> PurgeExpiredAsync(int retentionDays)
    {
        const string sql = @"
DELETE FROM dbo.Notes
WHERE IsDeleted = 1 AND DeletedAt IS NOT NULL AND DeletedAt < DATEADD(day, -@Days, SYSUTCDATETIME());";
        using var conn = _factory.Create();
        return await conn.ExecuteAsync(sql, new { Days = retentionDays });
    }

    public async Task SoftDeleteAsync(Guid noteId, Guid userId)
    {
        const string sql = @"
UPDATE dbo.Notes
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0;";
        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, new { Id = noteId, UserId = userId });
    }

    private static string BuildFullTextQuery(string input)
    {
        // Prefix search per term: "term*" AND "term2*"
        // Escape quotes by doubling them for CONTAINS.
        var tokens = input
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(8)
            .Select(t => t.Replace("\"", "\"\""))
            .Where(t => t.Length > 0)
            .Select(t => $"\"{t}*\"");

        var ft = string.Join(" AND ", tokens);
        return string.IsNullOrWhiteSpace(ft) ? "\"*\"" : ft;
    }
}
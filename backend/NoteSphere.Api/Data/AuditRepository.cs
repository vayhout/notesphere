using Dapper;
using NoteSphere.Api.Models;

namespace NoteSphere.Api.Data;

public sealed class AuditRepository
{
    private readonly SqlConnectionFactory _factory;

    public AuditRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddAsync(AuditLog log)
    {
        const string sql = @"
INSERT INTO dbo.AuditLogs (Id, UserId, NoteId, Action, Ip, UserAgent, MetadataJson, CreatedAt)
VALUES (@Id, @UserId, @NoteId, @Action, @Ip, @UserAgent, @MetadataJson, @CreatedAt);";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, log);
    }
}

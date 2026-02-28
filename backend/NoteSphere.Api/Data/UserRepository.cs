using Dapper;
using NoteSphere.Api.Models;

namespace NoteSphere.Api.Data;

public sealed class UserRepository
{
    private readonly SqlConnectionFactory _factory;

    public UserRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = @"SELECT TOP 1 * FROM dbo.Users WHERE Email = @Email;";
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(sql, new { Email = email.Trim().ToLowerInvariant() });
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        const string sql = @"SELECT TOP 1 * FROM dbo.Users WHERE Id = @Id;";
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task CreateAsync(User user)
    {
        const string sql = @"
INSERT INTO dbo.Users (Id, Email, PasswordHash, DisplayName, CreatedAt)
VALUES (@Id, @Email, @PasswordHash, @DisplayName, @CreatedAt);";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(sql, user);
    }
}

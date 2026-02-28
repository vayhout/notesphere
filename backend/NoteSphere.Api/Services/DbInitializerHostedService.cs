using Dapper;
using Microsoft.Data.SqlClient;
using NoteSphere.Api.Data;
using NoteSphere.Api.Models;

namespace NoteSphere.Api.Services;

public sealed class DbInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DbInitializerHostedService> _logger;

    public DbInitializerHostedService(IServiceProvider sp, ILogger<DbInitializerHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Retry a few times in case SQL Server is still starting up.
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<SqlConnectionFactory>();
                using var conn = factory.Create();
                await conn.OpenAsync(cancellationToken);

                await EnsureSchemaAsync(conn);
                await SeedAsync(conn);
                _logger.LogInformation("Database ready.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DB init attempt {Attempt} failed. Retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        _logger.LogError("Database initialization failed after retries.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureSchemaAsync(SqlConnection conn)
    {
        // Create DB if not exists (this requires connecting to a DB that exists; if connection string points to
        // NoteSphereDb and it doesn't exist yet, you'll get 4060. In that case, create the DB once manually.
        // (We keep this logic for completeness when DB already exists.)
        await conn.ExecuteAsync("IF DB_ID('NoteSphereDb') IS NULL CREATE DATABASE NoteSphereDb;");
        await conn.ExecuteAsync("USE NoteSphereDb;");

        // Users
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.Users (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Email NVARCHAR(320) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(200) NOT NULL,
    DisplayName NVARCHAR(120) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
  );
END
");

        // Notes (create)
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.Notes', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.Notes (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    TagsJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    IsPinned BIT NOT NULL DEFAULT 0,
    IsArchived BIT NOT NULL DEFAULT 0,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Notes_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
  );
END
");

        // Notes (upgrade columns)
        await conn.ExecuteAsync(@"
IF COL_LENGTH('dbo.Notes','IsDeleted') IS NULL
  ALTER TABLE dbo.Notes ADD IsDeleted BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Notes','DeletedAt') IS NULL
  ALTER TABLE dbo.Notes ADD DeletedAt DATETIME2 NULL;
");

        // Helpful indexes
        await conn.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Notes_UserId_CreatedAt' AND object_id=OBJECT_ID('dbo.Notes'))
  CREATE INDEX IX_Notes_UserId_CreatedAt ON dbo.Notes(UserId, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Notes_UserId_UpdatedAt' AND object_id=OBJECT_ID('dbo.Notes'))
  CREATE INDEX IX_Notes_UserId_UpdatedAt ON dbo.Notes(UserId, UpdatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Notes_UserId_IsDeleted' AND object_id=OBJECT_ID('dbo.Notes'))
  CREATE INDEX IX_Notes_UserId_IsDeleted ON dbo.Notes(UserId, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Notes_Id' AND object_id=OBJECT_ID('dbo.Notes'))
  CREATE UNIQUE INDEX UX_Notes_Id ON dbo.Notes(Id);
");

        // Audit logs
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.AuditLogs (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    NoteId UNIQUEIDENTIFIER NULL,
    Action NVARCHAR(64) NOT NULL,
    Ip NVARCHAR(45) NULL,
    UserAgent NVARCHAR(256) NULL,
    MetadataJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
  );
END
");
        await conn.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_UserId_CreatedAt' AND object_id=OBJECT_ID('dbo.AuditLogs'))
  CREATE INDEX IX_AuditLogs_UserId_CreatedAt ON dbo.AuditLogs(UserId, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_NoteId_CreatedAt' AND object_id=OBJECT_ID('dbo.AuditLogs'))
  CREATE INDEX IX_AuditLogs_NoteId_CreatedAt ON dbo.AuditLogs(NoteId, CreatedAt DESC);
");

        // Full-text (best effort)
        try
        {
            await conn.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'NoteSphereFT')
  CREATE FULLTEXT CATALOG NoteSphereFT AS DEFAULT;
");
            await conn.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes fi WHERE fi.object_id = OBJECT_ID('dbo.Notes'))
BEGIN
  CREATE FULLTEXT INDEX ON dbo.Notes(Title LANGUAGE 1033, Content LANGUAGE 1033)
  KEY INDEX UX_Notes_Id
  WITH STOPLIST = SYSTEM;
END
");
        }
        catch (SqlException)
        {
            // Some environments may not support full-text; app will fall back to LIKE.
        }
    }

    private static async Task SeedAsync(SqlConnection conn)
    {
        // Seed only if no users
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Users;");
        if (count > 0) return;

        var demoUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "demo@notesphere.dev",
            DisplayName = "Demo User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            CreatedAt = DateTime.UtcNow
        };

        await conn.ExecuteAsync(@"
INSERT INTO dbo.Users (Id, Email, PasswordHash, DisplayName, CreatedAt)
VALUES (@Id, @Email, @PasswordHash, @DisplayName, @CreatedAt);", demoUser);

        var now = DateTime.UtcNow;
        var sampleNotes = new List<Note>
        {
            new()
            {
                Id = Guid.NewGuid(), UserId = demoUser.Id,
                Title = "Welcome to NoteSphere ✨",
                Content = "This is your first note. Try **Markdown**!\n\n- Create notes\n- Pin & archive\n- Use tags\n- Search instantly (Full-Text)",
                TagsJson = "[\"welcome\",\"markdown\"]",
                IsPinned = true, IsArchived = false, IsDeleted = false, DeletedAt = null,
                CreatedAt = now.AddMinutes(-50), UpdatedAt = now.AddMinutes(-50)
            },
            new()
            {
                Id = Guid.NewGuid(), UserId = demoUser.Id,
                Title = "Interview Talking Points",
                Content = "Mention:\n1) JWT auth + per-user authorization\n2) Dapper + parameterized queries\n3) Full-text search w/ SQL index + fallback\n4) Soft-delete + audit log\n5) Dashboard stats endpoint",
                TagsJson = "[\"interview\",\"notes\"]",
                IsPinned = false, IsArchived = false, IsDeleted = false, DeletedAt = null,
                CreatedAt = now.AddMinutes(-30), UpdatedAt = now.AddMinutes(-10)
            },
            new()
            {
                Id = Guid.NewGuid(), UserId = demoUser.Id,
                Title = "Archived example",
                Content = "Archived notes stay accessible but hidden by default.",
                TagsJson = "[\"archive\"]",
                IsPinned = false, IsArchived = true, IsDeleted = false, DeletedAt = null,
                CreatedAt = now.AddMinutes(-20), UpdatedAt = now.AddMinutes(-20)
            }
        };

        const string sql = @"
INSERT INTO dbo.Notes
(Id, UserId, Title, Content, TagsJson, IsPinned, IsArchived, IsDeleted, DeletedAt, CreatedAt, UpdatedAt)
VALUES
(@Id, @UserId, @Title, @Content, @TagsJson, @IsPinned, @IsArchived, @IsDeleted, @DeletedAt, @CreatedAt, @UpdatedAt);";

        foreach (var n in sampleNotes)
        {
            await conn.ExecuteAsync(sql, n);
        }
    }
}

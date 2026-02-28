-- NoteSphere schema (v2)
-- Run manually if needed. The API will auto-create/upgrade tables too.

IF DB_ID('NoteSphereDb') IS NULL
BEGIN
  CREATE DATABASE NoteSphereDb;
END
GO

USE NoteSphereDb;
GO

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
GO

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

  CREATE INDEX IX_Notes_UserId_CreatedAt ON dbo.Notes(UserId, CreatedAt DESC);
  CREATE INDEX IX_Notes_UserId_UpdatedAt ON dbo.Notes(UserId, UpdatedAt DESC);
  CREATE INDEX IX_Notes_UserId_IsDeleted ON dbo.Notes(UserId, IsDeleted);
  CREATE UNIQUE INDEX UX_Notes_Id ON dbo.Notes(Id);
END
GO

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

  CREATE INDEX IX_AuditLogs_UserId_CreatedAt ON dbo.AuditLogs(UserId, CreatedAt DESC);
  CREATE INDEX IX_AuditLogs_NoteId_CreatedAt ON dbo.AuditLogs(NoteId, CreatedAt DESC);
END
GO

-- Full-text (optional, but great for demo). Requires SQL Server Full-Text feature (included in official container).
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'NoteSphereFT')
BEGIN
  CREATE FULLTEXT CATALOG NoteSphereFT AS DEFAULT;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes fi WHERE fi.object_id = OBJECT_ID('dbo.Notes'))
BEGIN
  CREATE FULLTEXT INDEX ON dbo.Notes(Title LANGUAGE 1033, Content LANGUAGE 1033)
  KEY INDEX UX_Notes_Id
  WITH STOPLIST = SYSTEM;
END
GO

GO
-- Optional: index for purge job
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Notes_DeletedAt' AND object_id=OBJECT_ID('dbo.Notes'))
  CREATE INDEX IX_Notes_DeletedAt ON dbo.Notes(IsDeleted, DeletedAt);
GO

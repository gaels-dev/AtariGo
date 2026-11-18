USE AtariGoDB;
GO

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.Reports', N'U') IS NULL
            OR OBJECT_ID(N'dbo.Sanctions', N'U') IS NULL
        BEGIN
            ;THROW 51020, 'The legacy report tables are missing.', 1;
        END;

        IF EXISTS
        (
            SELECT 1 FROM dbo.Users
            WHERE State IN ('Banned', 'Suspended')
        )
        BEGIN
            ;THROW 51021, 'A banned or suspended account requires review.', 1;
        END;

        DELETE FROM dbo.Sanctions;
        DELETE FROM dbo.Reports;

        DROP TABLE dbo.Sanctions;
        DROP TABLE dbo.Reports;

        ALTER TABLE dbo.Users DROP CONSTRAINT FK_Users_Role;
        DROP INDEX IX_Users_RoleId ON dbo.Users;
        ALTER TABLE dbo.Users DROP COLUMN RoleId;
        DROP TABLE dbo.Roles;

        ALTER TABLE dbo.Users DROP CONSTRAINT CK_Users_State;
        ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT CK_Users_State
            CHECK (State IN ('Active', 'Inactive'));

        CREATE TABLE dbo.Reports
        (
            ReportId BIGINT IDENTITY(1, 1) NOT NULL,
            ReporterId INT NOT NULL,
            ReportedUserId INT NOT NULL,
            GameId BIGINT NOT NULL,
            Category VARCHAR(20) NOT NULL,
            Description NVARCHAR(2000) NULL,
            CreatedAt DATETIME2(0) NOT NULL
                CONSTRAINT DF_Reports_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_Reports PRIMARY KEY (ReportId),
            CONSTRAINT UQ_Reports_Reporter_Game UNIQUE (ReporterId, GameId),
            CONSTRAINT FK_Reports_Reporter FOREIGN KEY (ReporterId)
                REFERENCES dbo.Users (UserId),
            CONSTRAINT FK_Reports_ReportedUser FOREIGN KEY (ReportedUserId)
                REFERENCES dbo.Users (UserId),
            CONSTRAINT FK_Reports_Game FOREIGN KEY (GameId)
                REFERENCES dbo.Games (GameId),
            CONSTRAINT CK_Reports_DifferentUsers CHECK (ReporterId <> ReportedUserId),
            CONSTRAINT CK_Reports_Category
                CHECK (Category IN ('Chat', 'Inactivity', 'Manipulation'))
        );

        CREATE INDEX IX_Reports_Target_Category_Created
            ON dbo.Reports (ReportedUserId, Category, CreatedAt DESC, ReporterId);

        CREATE TABLE dbo.Sanctions
        (
            SanctionId BIGINT IDENTITY(1, 1) NOT NULL,
            UserId INT NOT NULL,
            TriggeringReportId BIGINT NOT NULL,
            SanctionType VARCHAR(30) NOT NULL,
            Threshold INT NOT NULL,
            StartedAt DATETIME2(0) NOT NULL
                CONSTRAINT DF_Sanctions_StartedAt DEFAULT (SYSUTCDATETIME()),
            EndsAt DATETIME2(0) NOT NULL,
            RevokedAt DATETIME2(0) NULL,
            CONSTRAINT PK_Sanctions PRIMARY KEY (SanctionId),
            CONSTRAINT UQ_Sanctions_TriggeringReport UNIQUE (TriggeringReportId),
            CONSTRAINT FK_Sanctions_User FOREIGN KEY (UserId)
                REFERENCES dbo.Users (UserId),
            CONSTRAINT FK_Sanctions_TriggeringReport FOREIGN KEY (TriggeringReportId)
                REFERENCES dbo.Reports (ReportId),
            CONSTRAINT CK_Sanctions_Type
                CHECK (SanctionType IN ('ChatMute', 'TemporarySuspension')),
            CONSTRAINT CK_Sanctions_Threshold
                CHECK (Threshold >= 3 AND Threshold % 3 = 0),
            CONSTRAINT CK_Sanctions_EndsAt CHECK (EndsAt > StartedAt),
            CONSTRAINT CK_Sanctions_RevokedAt CHECK
                (RevokedAt IS NULL OR RevokedAt >= StartedAt)
        );

        CREATE INDEX IX_Sanctions_User_Dates
            ON dbo.Sanctions (UserId, SanctionType, StartedAt DESC, EndsAt);
    END
    ELSE IF COL_LENGTH(N'dbo.Reports', N'Category') IS NULL
        OR COL_LENGTH(N'dbo.Sanctions', N'TriggeringReportId') IS NULL
    BEGIN
        ;THROW 51022, 'The database is not in a recognized report schema.', 1;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;
    ;THROW;
END CATCH;
GO

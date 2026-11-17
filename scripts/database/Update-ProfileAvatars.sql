USE AtariGoDB;
GO

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Users
        WHERE DATALENGTH(UserName) / 2 > 15
    )
    BEGIN
        ;THROW 51001, 'An existing username exceeds 15 characters.', 1;
    END;

    IF COL_LENGTH(N'dbo.Profiles', N'AvatarId') IS NULL
    BEGIN
        ALTER TABLE dbo.Profiles ADD AvatarId INT NOT NULL
            CONSTRAINT DF_Profiles_AvatarId DEFAULT (1) WITH VALUES;
    END;

    EXEC sys.sp_executesql N'
        INSERT INTO dbo.Profiles (UserId, AvatarId)
        SELECT userAccount.UserId, 1
        FROM dbo.Users AS userAccount
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.Profiles AS profile
            WHERE profile.UserId = userAccount.UserId
        );';

    IF COL_LENGTH(N'dbo.Profiles', N'Avatar') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Profiles DROP COLUMN Avatar;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Profiles')
            AND name = N'CK_Profiles_AvatarId'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Profiles WITH CHECK
            ADD CONSTRAINT CK_Profiles_AvatarId
            CHECK (AvatarId BETWEEN 1 AND 5);';
    END;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Users')
            AND name = N'UserName'
            AND max_length <> 30
    )
    BEGIN
        IF EXISTS
        (
            SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
                AND name = N'CK_Users_UserName_NotGuest'
        )
        BEGIN
            ALTER TABLE dbo.Users DROP CONSTRAINT CK_Users_UserName_NotGuest;
        END;

        IF EXISTS
        (
            SELECT 1 FROM sys.key_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
                AND name = N'UQ_Users_UserName'
        )
        BEGIN
            ALTER TABLE dbo.Users DROP CONSTRAINT UQ_Users_UserName;
        END;

        ALTER TABLE dbo.Users ALTER COLUMN UserName NVARCHAR(15) NOT NULL;

        ALTER TABLE dbo.Users ADD CONSTRAINT UQ_Users_UserName UNIQUE (UserName);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
            AND name = N'CK_Users_UserName_NotGuest'
    )
    BEGIN
        ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT CK_Users_UserName_NotGuest
            CHECK
            (
                LOWER(LTRIM(RTRIM(UserName))) <> N'guest'
                AND LEFT(LOWER(LTRIM(RTRIM(UserName))), 6) <> N'guest-'
            );
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
USE AtariGoDB;
GO

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Profiles', N'AvatarId') IS NULL
    BEGIN
        ;THROW 51001, 'Profiles.AvatarId is missing. Run Update-ProfileAvatars.sql first.', 1;
    END;

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Profiles')
            AND name = N'CK_Profiles_AvatarId'
    )
    BEGIN
        ALTER TABLE dbo.Profiles DROP CONSTRAINT CK_Profiles_AvatarId;
    END;

    ALTER TABLE dbo.Profiles WITH CHECK ADD CONSTRAINT CK_Profiles_AvatarId
        CHECK (AvatarId BETWEEN 1 AND 20);

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

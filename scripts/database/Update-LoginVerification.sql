USE AtariGoDB;
GO

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Credentials', N'AuthenticationVersion') IS NULL
    BEGIN
        ALTER TABLE dbo.Credentials ADD AuthenticationVersion BIGINT NOT NULL
            CONSTRAINT DF_Credentials_AuthenticationVersion DEFAULT (0) WITH VALUES;
    END;
    IF OBJECT_ID(N'dbo.LoginChallenges', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LoginChallenges
        (
            LoginChallengeId BIGINT IDENTITY(1, 1) NOT NULL,
            UserId INT NOT NULL,
            TokenHash VARCHAR(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
            AuthenticationVersion BIGINT NOT NULL,
            ActiveCodeId BIGINT NULL,
            CodeBinding VARCHAR(64) NOT NULL,
            CreatedAt DATETIME2(0) NOT NULL,
            IssueUntil DATETIME2(0) NOT NULL,
            ClosedAt DATETIME2(0) NULL,
            LastSentAt DATETIME2(0) NULL,
            DeliveryState VARCHAR(20) NOT NULL,
            DeliveryFailures INT NOT NULL CONSTRAINT DF_LoginChallenges_DeliveryFailures DEFAULT (0),
            CONSTRAINT PK_LoginChallenges PRIMARY KEY (LoginChallengeId),
            CONSTRAINT UQ_LoginChallenges_TokenHash UNIQUE (TokenHash),
            CONSTRAINT FK_LoginChallenges_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
            CONSTRAINT FK_LoginChallenges_Code FOREIGN KEY (ActiveCodeId)
                REFERENCES dbo.AuthenticationCodes (AuthenticationCodeId),
            CONSTRAINT CK_LoginChallenges_DeliveryState CHECK (DeliveryState IN ('Pending', 'Sent', 'Failed')),
            CONSTRAINT CK_LoginChallenges_DeliveryFailures CHECK (DeliveryFailures >= 0),
            CONSTRAINT CK_LoginChallenges_IssueUntil CHECK (IssueUntil > CreatedAt)
        );
        CREATE INDEX IX_LoginChallenges_User_Closed ON dbo.LoginChallenges (UserId, ClosedAt);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

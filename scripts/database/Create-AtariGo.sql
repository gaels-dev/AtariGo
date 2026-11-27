SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO

USE master;
GO

IF DB_ID(N'AtariGoDB') IS NULL
BEGIN
    CREATE DATABASE AtariGoDB COLLATE Latin1_General_100_CI_AI_SC;
END;
GO

USE AtariGoDB;
GO

CREATE TABLE dbo.Users
(
    UserId INT IDENTITY(1, 1) NOT NULL,
    UserName NVARCHAR(15) NOT NULL,
    Email NVARCHAR(254) NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Users_State DEFAULT ('Active'),
    RegistrationDate DATETIME2(0) NOT NULL
        CONSTRAINT DF_Users_RegistrationDate DEFAULT (SYSUTCDATETIME()),
    LastUpdate DATETIME2(0) NULL,
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_State CHECK (State IN ('Active', 'Inactive')),
    CONSTRAINT CK_Users_LastUpdate CHECK
        (LastUpdate IS NULL OR LastUpdate >= RegistrationDate)
);
GO

CREATE TABLE dbo.Credentials
(
    UserId INT NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    PasswordChangedAt DATETIME2(0) NULL,
    FailedAttempts INT NOT NULL
        CONSTRAINT DF_Credentials_FailedAttempts DEFAULT (0),
    LockedUntil DATETIME2(0) NULL,
    CONSTRAINT PK_Credentials PRIMARY KEY (UserId),
    CONSTRAINT FK_Credentials_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Credentials_FailedAttempts CHECK (FailedAttempts >= 0)
);
GO

CREATE TABLE dbo.Profiles
(
    UserId INT NOT NULL,
    AvatarId INT NOT NULL
        CONSTRAINT DF_Profiles_AvatarId DEFAULT (1),
    Description NVARCHAR(200) NULL,
    LastUpdate DATETIME2(0) NULL,
    CONSTRAINT PK_Profiles PRIMARY KEY (UserId),
    CONSTRAINT FK_Profiles_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Profiles_AvatarId CHECK (AvatarId BETWEEN 1 AND 20)
);
GO

CREATE TABLE dbo.Sessions
(
    SessionId BIGINT IDENTITY(1, 1) NOT NULL,
    UserId INT NOT NULL,
    SessionTokenHash CHAR(64) NOT NULL,
    StartedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Sessions_StartedAt DEFAULT (SYSUTCDATETIME()),
    LastActivityAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Sessions_LastActivityAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt DATETIME2(0) NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Sessions_State DEFAULT ('Active'),
    CONSTRAINT PK_Sessions PRIMARY KEY (SessionId),
    CONSTRAINT UQ_Sessions_TokenHash UNIQUE (SessionTokenHash),
    CONSTRAINT FK_Sessions_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Sessions_State CHECK (State IN ('Active', 'Expired', 'Revoked')),
    CONSTRAINT CK_Sessions_Timestamps CHECK
        (LastActivityAt >= StartedAt AND ExpiresAt > StartedAt)
);
GO

CREATE INDEX IX_Sessions_User_State_Expiry
    ON dbo.Sessions (UserId, State, ExpiresAt);
GO

CREATE TABLE dbo.AuthenticationCodes
(
    AuthenticationCodeId BIGINT IDENTITY(1, 1) NOT NULL,
    UserId INT NOT NULL,
    CodeType VARCHAR(30) NOT NULL,
    CodeHash CHAR(64) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_AuthenticationCodes_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt DATETIME2(0) NOT NULL,
    UsedAt DATETIME2(0) NULL,
    AttemptCount INT NOT NULL
        CONSTRAINT DF_AuthenticationCodes_AttemptCount DEFAULT (0),
    CONSTRAINT PK_AuthenticationCodes PRIMARY KEY (AuthenticationCodeId),
    CONSTRAINT FK_AuthenticationCodes_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_AuthenticationCodes_Type
        CHECK (CodeType IN ('TwoFactor', 'PasswordReset')),
    CONSTRAINT CK_AuthenticationCodes_Expiry CHECK (ExpiresAt > CreatedAt),
    CONSTRAINT CK_AuthenticationCodes_UsedAt CHECK
        (UsedAt IS NULL OR UsedAt >= CreatedAt),
    CONSTRAINT CK_AuthenticationCodes_AttemptCount CHECK (AttemptCount >= 0)
);
GO

CREATE INDEX IX_AuthenticationCodes_User_Type_Expiry
    ON dbo.AuthenticationCodes (UserId, CodeType, ExpiresAt);
GO

CREATE TABLE dbo.UserPreferences
(
    UserId INT NOT NULL,
    SoundEnabled BIT NOT NULL
        CONSTRAINT DF_UserPreferences_SoundEnabled DEFAULT (1),
    MusicEnabled BIT NOT NULL
        CONSTRAINT DF_UserPreferences_MusicEnabled DEFAULT (1),
    CONSTRAINT PK_UserPreferences PRIMARY KEY (UserId),
    CONSTRAINT FK_UserPreferences_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId)
);
GO

CREATE TABLE dbo.FriendRequests
(
    FriendRequestId BIGINT IDENTITY(1, 1) NOT NULL,
    RequesterId INT NOT NULL,
    RecipientId INT NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_FriendRequests_State DEFAULT ('Pending'),
    RequestedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_FriendRequests_RequestedAt DEFAULT (SYSUTCDATETIME()),
    RespondedAt DATETIME2(0) NULL,
    CONSTRAINT PK_FriendRequests PRIMARY KEY (FriendRequestId),
    CONSTRAINT FK_FriendRequests_Requester FOREIGN KEY (RequesterId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_FriendRequests_Recipient FOREIGN KEY (RecipientId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_FriendRequests_DifferentUsers CHECK (RequesterId <> RecipientId),
    CONSTRAINT CK_FriendRequests_State
        CHECK (State IN ('Pending', 'Accepted', 'Rejected', 'Cancelled', 'Expired')),
    CONSTRAINT CK_FriendRequests_Response CHECK
        ((State = 'Pending' AND RespondedAt IS NULL)
        OR (State <> 'Pending' AND RespondedAt IS NOT NULL))
);
GO

CREATE UNIQUE INDEX UX_FriendRequests_PendingPair
    ON dbo.FriendRequests (RequesterId, RecipientId)
    WHERE State = 'Pending';
GO

CREATE INDEX IX_FriendRequests_Recipient_State
    ON dbo.FriendRequests (RecipientId, State, RequestedAt DESC);
GO

CREATE INDEX IX_FriendRequests_Requester
    ON dbo.FriendRequests (RequesterId, RequestedAt DESC);
GO

CREATE TABLE dbo.Friendships
(
    UserId INT NOT NULL,
    FriendUserId INT NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Friendships_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Friendships PRIMARY KEY (UserId, FriendUserId),
    CONSTRAINT FK_Friendships_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Friendships_Friend FOREIGN KEY (FriendUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Friendships_CanonicalPair CHECK (UserId < FriendUserId)
);
GO

CREATE INDEX IX_Friendships_FriendUserId ON dbo.Friendships (FriendUserId, UserId);
GO

CREATE TABLE dbo.Rooms
(
    RoomId INT IDENTITY(1, 1) NOT NULL,
    InvitationCode VARCHAR(10) NOT NULL,
    CreatorId INT NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Rooms_State DEFAULT ('Waiting'),
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Rooms_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt DATETIME2(0) NULL,
    CONSTRAINT PK_Rooms PRIMARY KEY (RoomId),
    CONSTRAINT UQ_Rooms_InvitationCode UNIQUE (InvitationCode),
    CONSTRAINT FK_Rooms_Creator FOREIGN KEY (CreatorId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Rooms_State CHECK (State IN ('Waiting', 'Active', 'Closed', 'Expired')),
    CONSTRAINT CK_Rooms_Expiry CHECK (ExpiresAt IS NULL OR ExpiresAt > CreatedAt)
);
GO

CREATE INDEX IX_Rooms_Creator_State ON dbo.Rooms (CreatorId, State);
GO

CREATE TABLE dbo.Invitations
(
    InvitationId BIGINT IDENTITY(1, 1) NOT NULL,
    RoomId INT NOT NULL,
    SenderId INT NOT NULL,
    RecipientId INT NULL,
    RecipientEmail NVARCHAR(254) NULL,
    InvitationType VARCHAR(20) NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Invitations_State DEFAULT ('Pending'),
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Invitations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    RespondedAt DATETIME2(0) NULL,
    CONSTRAINT PK_Invitations PRIMARY KEY (InvitationId),
    CONSTRAINT FK_Invitations_Room FOREIGN KEY (RoomId)
        REFERENCES dbo.Rooms (RoomId),
    CONSTRAINT FK_Invitations_Sender FOREIGN KEY (SenderId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Invitations_Recipient FOREIGN KEY (RecipientId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Invitations_Type CHECK
        (InvitationType IN ('User', 'Email')),
    CONSTRAINT CK_Invitations_Recipient CHECK
        ((InvitationType = 'User' AND RecipientId IS NOT NULL AND RecipientEmail IS NULL)
        OR (InvitationType = 'Email' AND RecipientId IS NULL AND RecipientEmail IS NOT NULL)),
    CONSTRAINT CK_Invitations_NotSelf CHECK (RecipientId IS NULL OR SenderId <> RecipientId),
    CONSTRAINT CK_Invitations_State
        CHECK (State IN ('Pending', 'Accepted', 'Rejected', 'Cancelled', 'Expired')),
    CONSTRAINT CK_Invitations_Response CHECK
        ((State = 'Pending' AND RespondedAt IS NULL)
        OR (State <> 'Pending' AND RespondedAt IS NOT NULL))
);
GO

CREATE INDEX IX_Invitations_Room_State ON dbo.Invitations (RoomId, State);
GO

CREATE INDEX IX_Invitations_Sender_Created
    ON dbo.Invitations (SenderId, CreatedAt DESC);
GO

CREATE INDEX IX_Invitations_Recipient_State
    ON dbo.Invitations (RecipientId, State)
    WHERE RecipientId IS NOT NULL;
GO

CREATE TABLE dbo.GameConfigurations
(
    GameConfigurationId INT IDENTITY(1, 1) NOT NULL,
    CaptureGoal TINYINT NOT NULL
        CONSTRAINT DF_GameConfigurations_CaptureGoal DEFAULT (1),
    GameTimeSeconds INT NOT NULL,
    TurnTimeSeconds INT NOT NULL,
    CONSTRAINT PK_GameConfigurations PRIMARY KEY (GameConfigurationId),
    CONSTRAINT CK_GameConfigurations_CaptureGoal CHECK (CaptureGoal IN (1, 3, 5)),
    CONSTRAINT CK_GameConfigurations_GameTime CHECK (GameTimeSeconds >= 0),
    CONSTRAINT CK_GameConfigurations_TurnTime CHECK
        (TurnTimeSeconds > 0)
);
GO

CREATE TABLE dbo.Games
(
    GameId BIGINT IDENTITY(1, 1) NOT NULL,
    RoomId INT NULL,
    GameConfigurationId INT NOT NULL,
    GameType VARCHAR(20) NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Games_State DEFAULT ('Waiting'),
    StartedAt DATETIME2(0) NULL,
    FinishedAt DATETIME2(0) NULL,
    EndReason VARCHAR(30) NULL,
    StateVersion BIGINT NOT NULL CONSTRAINT DF_Games_StateVersion DEFAULT (0),
    CONSTRAINT CK_Games_StateVersion CHECK (StateVersion >= 0),
    CONSTRAINT PK_Games PRIMARY KEY (GameId),
    CONSTRAINT FK_Games_Room FOREIGN KEY (RoomId)
        REFERENCES dbo.Rooms (RoomId),
    CONSTRAINT FK_Games_Configuration FOREIGN KEY (GameConfigurationId)
        REFERENCES dbo.GameConfigurations (GameConfigurationId),
    CONSTRAINT CK_Games_Type CHECK (GameType IN ('Public', 'Private')),
    CONSTRAINT CK_Games_RoomByType CHECK
        ((GameType = 'Private' AND RoomId IS NOT NULL)
        OR (GameType = 'Public' AND RoomId IS NULL)),
    CONSTRAINT CK_Games_State
        CHECK (State IN ('Waiting', 'InProgress', 'Completed', 'Abandoned', 'Cancelled')),
    CONSTRAINT CK_Games_FinishedAt CHECK
        (FinishedAt IS NULL OR (StartedAt IS NOT NULL AND FinishedAt >= StartedAt)),
    CONSTRAINT CK_Games_EndReason CHECK
        ((State IN ('Completed', 'Abandoned', 'Cancelled') AND EndReason IS NOT NULL)
        OR (State IN ('Waiting', 'InProgress') AND EndReason IS NULL))
);
GO

CREATE UNIQUE INDEX UX_Games_RoomId
    ON dbo.Games (RoomId)
    WHERE RoomId IS NOT NULL;
GO

CREATE INDEX IX_Games_Configuration ON dbo.Games (GameConfigurationId);
GO

CREATE INDEX IX_Games_State_Type ON dbo.Games (State, GameType);
GO

CREATE TABLE dbo.GamePlayers
(
    GamePlayerId BIGINT IDENTITY(1, 1) NOT NULL,
    GameId BIGINT NOT NULL,
    UserId INT NULL,
    GuestName NVARCHAR(21) NULL,
    GuestIdentityId UNIQUEIDENTIFIER NULL,
    StoneColor VARCHAR(10) NOT NULL,
    TurnOrder TINYINT NOT NULL,
    CapturedStones INT NOT NULL
        CONSTRAINT DF_GamePlayers_CapturedStones DEFAULT (0),
    Result VARCHAR(20) NOT NULL
        CONSTRAINT DF_GamePlayers_Result DEFAULT ('Pending'),
    JoinedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_GamePlayers_JoinedAt DEFAULT (SYSUTCDATETIME()),
    LeftAt DATETIME2(0) NULL,
    CONSTRAINT PK_GamePlayers PRIMARY KEY (GamePlayerId),
    CONSTRAINT UQ_GamePlayers_Game_Player UNIQUE (GameId, GamePlayerId),
    CONSTRAINT UQ_GamePlayers_Game_Color UNIQUE (GameId, StoneColor),
    CONSTRAINT UQ_GamePlayers_Game_TurnOrder UNIQUE (GameId, TurnOrder),
    CONSTRAINT FK_GamePlayers_Game FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId),
    CONSTRAINT FK_GamePlayers_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_GamePlayers_Identity CHECK
        ((UserId IS NOT NULL AND GuestName IS NULL AND GuestIdentityId IS NULL)
        OR (UserId IS NULL AND GuestName IS NOT NULL
            AND LEN(LTRIM(RTRIM(GuestName))) > 0 AND GuestIdentityId IS NOT NULL)),
    CONSTRAINT CK_GamePlayers_Color CHECK (StoneColor IN ('Black', 'White')),
    CONSTRAINT CK_GamePlayers_TurnOrder CHECK (TurnOrder IN (1, 2)),
    CONSTRAINT CK_GamePlayers_CapturedStones CHECK (CapturedStones >= 0),
    CONSTRAINT CK_GamePlayers_Result
        CHECK (Result IN ('Pending', 'Won', 'Lost', 'Surrendered', 'Abandoned')),
    CONSTRAINT CK_GamePlayers_LeftAt CHECK (LeftAt IS NULL OR LeftAt >= JoinedAt)
);
GO

CREATE UNIQUE INDEX UX_GamePlayers_Game_User
    ON dbo.GamePlayers (GameId, UserId)
    WHERE UserId IS NOT NULL;
GO

CREATE INDEX IX_GamePlayers_UserId ON dbo.GamePlayers (UserId);
GO

CREATE TABLE dbo.Moves
(
    MoveId BIGINT IDENTITY(1, 1) NOT NULL,
    GamePlayerId BIGINT NOT NULL,
    TurnNumber INT NOT NULL,
    [Row] TINYINT NOT NULL,
    [Column] TINYINT NOT NULL,
    PlayedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Moves_PlayedAt DEFAULT (SYSUTCDATETIME()),
    CapturedStones INT NOT NULL
        CONSTRAINT DF_Moves_CapturedStones DEFAULT (0),
    CONSTRAINT PK_Moves PRIMARY KEY (MoveId),
    CONSTRAINT UQ_Moves_Player_Turn UNIQUE (GamePlayerId, TurnNumber),
    CONSTRAINT FK_Moves_GamePlayer FOREIGN KEY (GamePlayerId)
        REFERENCES dbo.GamePlayers (GamePlayerId),
    CONSTRAINT CK_Moves_TurnNumber CHECK (TurnNumber > 0),
    CONSTRAINT CK_Moves_Row CHECK ([Row] BETWEEN 0 AND 8),
    CONSTRAINT CK_Moves_Column CHECK ([Column] BETWEEN 0 AND 8),
    CONSTRAINT CK_Moves_CapturedStones CHECK (CapturedStones >= 0)
);
GO

CREATE INDEX IX_Moves_GamePlayer_PlayedAt
    ON dbo.Moves (GamePlayerId, PlayedAt, MoveId);
GO

CREATE TABLE dbo.GameEvents
(
    GameEventId BIGINT IDENTITY(1, 1) NOT NULL,
    GameId BIGINT NOT NULL,
    GamePlayerId BIGINT NULL,
    EventType VARCHAR(30) NOT NULL,
    Description NVARCHAR(200) NULL,
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_GameEvents_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_GameEvents PRIMARY KEY (GameEventId),
    CONSTRAINT FK_GameEvents_Game FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId),
    CONSTRAINT FK_GameEvents_GamePlayer FOREIGN KEY (GameId, GamePlayerId)
        REFERENCES dbo.GamePlayers (GameId, GamePlayerId),
    CONSTRAINT CK_GameEvents_Type CHECK
        (EventType IN
            ('GameStarted', 'GameFinished', 'Capture', 'Disconnect', 'Reconnect',
            'Abandon', 'Surrender', 'Timeout', 'Move', 'ActionRejected'))
);
GO

CREATE INDEX IX_GameEvents_Game_Created
    ON dbo.GameEvents (GameId, CreatedAt, GameEventId);
GO

CREATE TABLE dbo.Reports
(
    ReportId BIGINT IDENTITY(1, 1) NOT NULL,
    ReporterId INT NOT NULL,
    ReportedUserId INT NOT NULL,
    GameId BIGINT NOT NULL,
    Category VARCHAR(20) NOT NULL,
    Description NVARCHAR(200) NULL,
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
GO

CREATE INDEX IX_Reports_Target_Category_Created
    ON dbo.Reports (ReportedUserId, Category, CreatedAt DESC, ReporterId);
GO

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
    CONSTRAINT CK_Sanctions_Threshold CHECK (Threshold >= 3 AND Threshold % 3 = 0),
    CONSTRAINT CK_Sanctions_EndsAt CHECK (EndsAt > StartedAt),
    CONSTRAINT CK_Sanctions_RevokedAt CHECK
        (RevokedAt IS NULL OR RevokedAt >= StartedAt)
);
GO

CREATE INDEX IX_Sanctions_User_Dates
    ON dbo.Sanctions (UserId, SanctionType, StartedAt DESC, EndsAt);
GO

CREATE UNIQUE INDEX UX_GamePlayers_Game_GuestIdentity
    ON dbo.GamePlayers(GameId, GuestIdentityId) WHERE GuestIdentityId IS NOT NULL;
GO

ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT CK_Users_UserName_NotGuest CHECK
    (LOWER(LTRIM(RTRIM(UserName))) <> N'guest'
        AND LEFT(LOWER(LTRIM(RTRIM(UserName))), 6) <> N'guest-');
GO

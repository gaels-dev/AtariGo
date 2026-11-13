-- Fresh installation only. Existing databases must use Update-Gameplay.sql.
USE master;
GO

IF DB_ID(N'AtariGoDB') IS NULL
BEGIN
    CREATE DATABASE AtariGoDB;
END;
GO

USE AtariGoDB;
GO

USE AtariGoDB;
GO

/*
    AtariGO database schema.
    The application owns game rules and validates cross-row rules such as
    starting a game only after exactly two players have joined.
    Foreign keys use NO ACTION by default to preserve game and moderation history.
*/

CREATE TABLE dbo.Roles
(
    RoleId INT IDENTITY(1, 1) NOT NULL,
    Name NVARCHAR(30) NOT NULL,
    Description NVARCHAR(500) NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
);
GO

CREATE TABLE dbo.Users
(
    UserId INT IDENTITY(1, 1) NOT NULL,
    UserName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(254) NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Users_State DEFAULT ('Active'),
    RegistrationDate DATETIME2(0) NOT NULL
        CONSTRAINT DF_Users_RegistrationDate DEFAULT (SYSUTCDATETIME()),
    LastUpdate DATETIME2(0) NULL,
    RoleId INT NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId)
        REFERENCES dbo.Roles (RoleId),
    CONSTRAINT CK_Users_State CHECK (State IN ('Active', 'Inactive', 'Suspended', 'Banned')),
    CONSTRAINT CK_Users_LastUpdate CHECK
        (LastUpdate IS NULL OR LastUpdate >= RegistrationDate)
);
GO

CREATE INDEX IX_Users_RoleId ON dbo.Users (RoleId);
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
    Avatar VARBINARY(MAX) NULL,
    Description NVARCHAR(500) NULL,
    LastUpdate DATETIME2(0) NULL,
    CONSTRAINT PK_Profiles PRIMARY KEY (UserId),
    CONSTRAINT FK_Profiles_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId)
);
GO

CREATE TABLE dbo.Sessions
(
    SessionId BIGINT IDENTITY(1, 1) NOT NULL,
    UserId INT NOT NULL,
    SessionTokenHash VARCHAR(255) NOT NULL,
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
    CodeHash VARCHAR(255) NOT NULL,
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
    InvitationCode VARCHAR(20) NOT NULL,
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
    -- Legacy compatibility field; gameplay only uses TurnTimeSeconds.
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
    CurrentTurnGamePlayerId BIGINT NULL,
    StartedAt DATETIME2(0) NULL,
    FinishedAt DATETIME2(0) NULL,
    EndReason VARCHAR(30) NULL,
    BoardStateJson NVARCHAR(MAX) NULL,
    StateVersion BIGINT NOT NULL CONSTRAINT DF_Games_StateVersion DEFAULT (0),
    TurnDeadlineUtc DATETIME2(3) NULL,
    CONSTRAINT CK_Games_StateVersion CHECK (StateVersion >= 0),
    CONSTRAINT CK_Games_BoardStateJson CHECK (BoardStateJson IS NULL OR ISJSON(BoardStateJson) = 1),
    CONSTRAINT CK_Games_GameplayState CHECK
        ((BoardStateJson IS NULL AND TurnDeadlineUtc IS NULL)
         OR (BoardStateJson IS NOT NULL AND TurnDeadlineUtc IS NOT NULL)),
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
    GuestName NVARCHAR(100) NULL,
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
        ((UserId IS NOT NULL AND GuestName IS NULL)
         OR (UserId IS NULL AND GuestName IS NOT NULL AND LEN(LTRIM(RTRIM(GuestName))) > 0)),
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

ALTER TABLE dbo.Games
    ADD CONSTRAINT FK_Games_CurrentTurnPlayer FOREIGN KEY
        (GameId, CurrentTurnGamePlayerId)
        REFERENCES dbo.GamePlayers (GameId, GamePlayerId);
GO

CREATE TABLE dbo.Moves
(
    MoveId BIGINT IDENTITY(1, 1) NOT NULL,
    GamePlayerId BIGINT NOT NULL,
    -- Global turn number; automatic timeout passes also advance this counter.
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
    ActionId VARCHAR(64) NULL,
    ActionRequestJson NVARCHAR(MAX) NULL,
    ActionResultJson NVARCHAR(MAX) NULL,
    CONSTRAINT CK_GameEvents_Action CHECK
        ((ActionId IS NULL AND ActionRequestJson IS NULL AND ActionResultJson IS NULL)
         OR (ActionId IS NOT NULL AND GamePlayerId IS NOT NULL
             AND ActionRequestJson IS NOT NULL AND ISJSON(ActionRequestJson) = 1
             AND ActionResultJson IS NOT NULL AND ISJSON(ActionResultJson) = 1)),
    Description NVARCHAR(1000) NULL,
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
    GameId BIGINT NULL,
    ReportReason VARCHAR(30) NOT NULL,
    Description NVARCHAR(2000) NOT NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Reports_State DEFAULT ('Pending'),
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Reports_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ReviewedAt DATETIME2(0) NULL,
    ReviewerId INT NULL,
    CONSTRAINT PK_Reports PRIMARY KEY (ReportId),
    CONSTRAINT FK_Reports_Reporter FOREIGN KEY (ReporterId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Reports_ReportedUser FOREIGN KEY (ReportedUserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Reports_Game FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId),
    CONSTRAINT FK_Reports_Reviewer FOREIGN KEY (ReviewerId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Reports_DifferentUsers CHECK (ReporterId <> ReportedUserId),
    CONSTRAINT CK_Reports_Reason
        CHECK (ReportReason IN ('OffensiveLanguage', 'Cheating', 'Other')),
    CONSTRAINT CK_Reports_State
        CHECK (State IN ('Pending', 'Reviewed', 'ActionTaken', 'Dismissed')),
    CONSTRAINT CK_Reports_Review CHECK
        ((State = 'Pending' AND ReviewedAt IS NULL AND ReviewerId IS NULL)
         OR (State <> 'Pending' AND ReviewedAt IS NOT NULL AND ReviewerId IS NOT NULL)),
    CONSTRAINT CK_Reports_ReviewedAt CHECK
        (ReviewedAt IS NULL OR ReviewedAt >= CreatedAt)
);
GO

CREATE INDEX IX_Reports_State_Created ON dbo.Reports (State, CreatedAt);
GO

CREATE INDEX IX_Reports_ReportedUser ON dbo.Reports (ReportedUserId, CreatedAt DESC);
GO

CREATE INDEX IX_Reports_Reviewer ON dbo.Reports (ReviewerId)
    WHERE ReviewerId IS NOT NULL;
GO

CREATE TABLE dbo.Sanctions
(
    SanctionId BIGINT IDENTITY(1, 1) NOT NULL,
    UserId INT NOT NULL,
    AdministratorId INT NOT NULL,
    ReportId BIGINT NULL,
    SanctionType VARCHAR(30) NOT NULL,
    Reason NVARCHAR(1000) NOT NULL,
    StartedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Sanctions_StartedAt DEFAULT (SYSUTCDATETIME()),
    EndsAt DATETIME2(0) NULL,
    State VARCHAR(20) NOT NULL
        CONSTRAINT DF_Sanctions_State DEFAULT ('Active'),
    CONSTRAINT PK_Sanctions PRIMARY KEY (SanctionId),
    CONSTRAINT FK_Sanctions_User FOREIGN KEY (UserId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Sanctions_Administrator FOREIGN KEY (AdministratorId)
        REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Sanctions_Report FOREIGN KEY (ReportId)
        REFERENCES dbo.Reports (ReportId),
    CONSTRAINT CK_Sanctions_Type
        CHECK (SanctionType IN ('Warning', 'TemporarySuspension', 'PermanentBan')),
    CONSTRAINT CK_Sanctions_State CHECK (State IN ('Active', 'Expired', 'Revoked')),
    CONSTRAINT CK_Sanctions_EndsAt CHECK (EndsAt IS NULL OR EndsAt > StartedAt)
);
GO

CREATE INDEX IX_Sanctions_User_State ON dbo.Sanctions (UserId, State, StartedAt DESC);
GO

CREATE INDEX IX_Sanctions_Administrator ON dbo.Sanctions (AdministratorId, StartedAt DESC);
GO

CREATE INDEX IX_Sanctions_Report ON dbo.Sanctions (ReportId)
    WHERE ReportId IS NOT NULL;
GO

-- Guest credentials are issued by the application; names are not authentication keys.
CREATE UNIQUE INDEX UX_GamePlayers_Game_GuestIdentity
    ON dbo.GamePlayers(GameId, GuestIdentityId) WHERE GuestIdentityId IS NOT NULL;
GO

-- Minimal application prerequisite; no users, passwords or sessions are created.
INSERT INTO dbo.Roles(Name, Description) VALUES (N'Player', N'Registered player');
GO

ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT CK_Users_UserName_NotGuest CHECK
    (LOWER(LTRIM(RTRIM(UserName))) <> N'guest'
        AND LEFT(LOWER(LTRIM(RTRIM(UserName))), 6) <> N'guest-');
GO

CREATE INDEX IX_Games_State_TurnDeadline ON dbo.Games(State, TurnDeadlineUtc);
GO
CREATE UNIQUE INDEX UX_GameEvents_Player_Action
    ON dbo.GameEvents(GameId, GamePlayerId, ActionId) WHERE ActionId IS NOT NULL;
GO

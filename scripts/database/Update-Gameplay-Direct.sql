SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Games', N'U') IS NULL OR OBJECT_ID(N'dbo.GameEvents', N'U') IS NULL
        THROW 51000, 'Missing AtariGo schema. Use Create-AtariGo.sql for a fresh database.', 1;

    IF COL_LENGTH(N'dbo.Games', N'BoardStateJson') IS NULL
        ALTER TABLE dbo.Games ADD BoardStateJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'dbo.Games', N'StateVersion') IS NULL
        ALTER TABLE dbo.Games ADD StateVersion BIGINT NOT NULL
            CONSTRAINT DF_Games_StateVersion DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.Games', N'TurnDeadlineUtc') IS NULL
        ALTER TABLE dbo.Games ADD TurnDeadlineUtc DATETIME2(3) NULL;
    IF COL_LENGTH(N'dbo.GameEvents', N'ActionId') IS NULL
        ALTER TABLE dbo.GameEvents ADD ActionId VARCHAR(64) NULL;
    IF COL_LENGTH(N'dbo.GameEvents', N'ActionRequestJson') IS NULL
        ALTER TABLE dbo.GameEvents ADD ActionRequestJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'dbo.GameEvents', N'ActionResultJson') IS NULL
        ALTER TABLE dbo.GameEvents ADD ActionResultJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'dbo.GamePlayers', N'GuestIdentityId') IS NULL
        ALTER TABLE dbo.GamePlayers ADD GuestIdentityId UNIQUEIDENTIFIER NULL;

    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.GameConfigurations')
            AND name = N'CK_GameConfigurations_GameTime')
        ALTER TABLE dbo.GameConfigurations DROP CONSTRAINT CK_GameConfigurations_GameTime;
    ALTER TABLE dbo.GameConfigurations WITH CHECK ADD CONSTRAINT CK_GameConfigurations_GameTime
        CHECK (GameTimeSeconds >= 0);
    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.GameConfigurations')
            AND name = N'CK_GameConfigurations_TurnTime')
        ALTER TABLE dbo.GameConfigurations DROP CONSTRAINT CK_GameConfigurations_TurnTime;
    ALTER TABLE dbo.GameConfigurations WITH CHECK ADD CONSTRAINT CK_GameConfigurations_TurnTime
        CHECK (TurnTimeSeconds > 0);

    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.GameEvents') AND name = N'CK_GameEvents_Type')
        ALTER TABLE dbo.GameEvents DROP CONSTRAINT CK_GameEvents_Type;
    ALTER TABLE dbo.GameEvents WITH CHECK ADD CONSTRAINT CK_GameEvents_Type CHECK
        (EventType IN ('GameStarted', 'GameFinished', 'Capture', 'Disconnect', 'Reconnect',
            'Abandon', 'Surrender', 'Timeout', 'Move', 'ActionRejected'));

    EXEC(N'-- An old unplayed game can be initialized; played games need an existing recoverable state.
IF EXISTS (SELECT 1 FROM dbo.Games AS game
    WHERE game.State IN (''Waiting'', ''InProgress'') AND game.BoardStateJson IS NULL
      AND EXISTS (SELECT 1 FROM dbo.Moves AS move
          JOIN dbo.GamePlayers AS player ON player.GamePlayerId = move.GamePlayerId
          WHERE player.GameId = game.GameId))
    THROW 51001, ''An active legacy game contains moves without saved board state. No changes were committed.'', 1;

DECLARE @waitingDeadline DATETIME2(3) = DATEADD(SECOND, 120, SYSUTCDATETIME());
UPDATE game SET BoardStateJson = CONCAT(N''{"Board":['', REPLICATE(N''0,'', 80), N''0],'',
    N''"CurrentTurn":1,"BlackCaptures":0,"WhiteCaptures":0,"CaptureGoal":'',
    configuration.CaptureGoal, N'',"TurnNumber":1,"Winner":0,"LastMove":null,"PositionHistory":["'',
    REPLICATE(N''0'', 81), N''"]}''), StateVersion = 0,
    TurnDeadlineUtc = @waitingDeadline, State = ''Waiting''
FROM dbo.Games AS game JOIN dbo.GameConfigurations AS configuration
    ON configuration.GameConfigurationId = game.GameConfigurationId
WHERE game.State IN (''Waiting'', ''InProgress'') AND game.BoardStateJson IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N''CK_Games_StateVersion''
    AND parent_object_id = OBJECT_ID(N''dbo.Games''))
    ALTER TABLE dbo.Games WITH CHECK ADD CONSTRAINT CK_Games_StateVersion CHECK (StateVersion >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N''CK_Games_BoardStateJson''
    AND parent_object_id = OBJECT_ID(N''dbo.Games''))
    ALTER TABLE dbo.Games WITH CHECK ADD CONSTRAINT CK_Games_BoardStateJson
        CHECK (BoardStateJson IS NULL OR ISJSON(BoardStateJson) = 1);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N''CK_Games_GameplayState''
    AND parent_object_id = OBJECT_ID(N''dbo.Games''))
    ALTER TABLE dbo.Games WITH CHECK ADD CONSTRAINT CK_Games_GameplayState CHECK
        ((BoardStateJson IS NULL AND TurnDeadlineUtc IS NULL)
         OR (BoardStateJson IS NOT NULL AND TurnDeadlineUtc IS NOT NULL));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N''CK_GameEvents_Action''
    AND parent_object_id = OBJECT_ID(N''dbo.GameEvents''))
    ALTER TABLE dbo.GameEvents WITH CHECK ADD CONSTRAINT CK_GameEvents_Action CHECK
        ((ActionId IS NULL AND ActionRequestJson IS NULL AND ActionResultJson IS NULL)
         OR (ActionId IS NOT NULL AND GamePlayerId IS NOT NULL
             AND ActionRequestJson IS NOT NULL AND ISJSON(ActionRequestJson) = 1
             AND ActionResultJson IS NOT NULL AND ISJSON(ActionResultJson) = 1));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Games'')
    AND name = N''IX_Games_State_TurnDeadline'')
    CREATE INDEX IX_Games_State_TurnDeadline ON dbo.Games(State, TurnDeadlineUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.GameEvents'')
    AND name = N''UX_GameEvents_Player_Action'')
    CREATE UNIQUE INDEX UX_GameEvents_Player_Action ON dbo.GameEvents(GameId, GamePlayerId, ActionId)
        WHERE ActionId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.GamePlayers'')
    AND name = N''UX_GamePlayers_Game_GuestIdentity'')
    CREATE UNIQUE INDEX UX_GamePlayers_Game_GuestIdentity ON dbo.GamePlayers(GameId, GuestIdentityId)
        WHERE GuestIdentityId IS NOT NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

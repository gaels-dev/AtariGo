-- Stop the Atari Go server and run this single script against its existing database.
-- Migrates the previous deployment, removes its two auxiliary tables, and expands existing tables.
-- Also supports the original schema and repeated execution. All changes are one transaction.
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

    IF OBJECT_ID(N'dbo.GameProgresses', N'U') IS NOT NULL
    BEGIN
    EXEC(N'-- Read only the old persisted state; never reset a played board or its repetition history.
IF EXISTS (SELECT 1 FROM dbo.GameProgresses WHERE ISJSON(StateJson) <> 1)
    THROW 51002, ''Invalid saved game JSON. No changes were committed.'', 1;
IF EXISTS (SELECT 1 FROM dbo.GameProgresses
    WHERE JSON_QUERY(StateJson, ''$.Board'') IS NULL
       OR JSON_VALUE(StateJson, ''$.Status'') IS NULL
       OR JSON_VALUE(StateJson, ''$.Status'') NOT IN (''Waiting'', ''InProgress'', ''Completed'', ''Cancelled''))
    THROW 51002, ''Incomplete saved game state. No changes were committed.'', 1;
IF EXISTS (SELECT 1 FROM dbo.GameProgresses AS source
    JOIN dbo.Games AS target ON target.GameId = source.GameId
    WHERE target.BoardStateJson IS NOT NULL)
    THROW 51003, ''Both storage layouts contain game state. Resolve the conflict before migrating.'', 1;

UPDATE target SET
    BoardStateJson = JSON_QUERY(source.StateJson, ''$.Board''),
    StateVersion = source.Version,
    TurnDeadlineUtc = source.NextDeadlineUtc,
    State = JSON_VALUE(source.StateJson, ''$.Status''),
    EndReason = CASE WHEN source.IsTerminal = 1
        THEN NULLIF(JSON_VALUE(source.StateJson, ''$.EndReason''), '''') ELSE NULL END
FROM dbo.Games AS target
JOIN dbo.GameProgresses AS source ON source.GameId = target.GameId;');
    END;

    IF OBJECT_ID(N'dbo.GameActionReceipts', N'U') IS NOT NULL
    BEGIN
    EXEC(N'IF EXISTS (SELECT 1 FROM dbo.GameActionReceipts WHERE ISJSON(RequestJson) <> 1)
    THROW 51004, ''Invalid saved action JSON. No changes were committed.'', 1;

SELECT source.GameId, player.GamePlayerId, source.ActionId,
    source.RequestJson, source.Rejection, source.IsAccepted,
    (SELECT source.ActionId AS ActionId, source.IsAccepted AS IsAccepted,
        source.Rejection AS Rejection, source.Version AS Version
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS ResultJson,
    (SELECT TOP (1) history.GameEventId FROM dbo.GameEvents AS history
        WHERE history.GameId = source.GameId AND history.GamePlayerId = player.GamePlayerId
          AND history.Description = source.ActionId AND history.EventType IN (''Move'', ''Surrender'')
          AND history.ActionId IS NULL
        ORDER BY history.GameEventId) AS ExistingEventId
INTO #ActionsToMigrate
FROM dbo.GameActionReceipts AS source
LEFT JOIN dbo.GamePlayers AS player ON player.GameId = source.GameId AND source.PlayerKey =
    CASE WHEN player.UserId IS NOT NULL THEN CONCAT(''user:'', player.UserId)
    ELSE CONCAT(''guest:'', LOWER(REPLACE(CONVERT(VARCHAR(36), player.GuestIdentityId), ''-'', ''''))) END;

IF EXISTS (SELECT 1 FROM #ActionsToMigrate WHERE GamePlayerId IS NULL)
    THROW 51005, ''An action cannot be linked to its participant. No changes were committed.'', 1;
IF EXISTS (SELECT 1 FROM #ActionsToMigrate AS source JOIN dbo.GameEvents AS target
    ON target.GameId = source.GameId AND target.GamePlayerId = source.GamePlayerId
        AND target.ActionId = source.ActionId)
    THROW 51006, ''Both storage layouts contain the same action. No changes were committed.'', 1;

-- Reuse original move/resignation events, preserving their identity and timestamp.
UPDATE target SET ActionId = source.ActionId,
    ActionRequestJson = source.RequestJson, ActionResultJson = source.ResultJson
FROM dbo.GameEvents AS target JOIN #ActionsToMigrate AS source
    ON source.ExistingEventId = target.GameEventId;

INSERT INTO dbo.GameEvents(GameId, GamePlayerId, EventType, Description, CreatedAt,
    ActionId, ActionRequestJson, ActionResultJson)
SELECT source.GameId, source.GamePlayerId,
    CASE WHEN source.IsAccepted = 0 THEN ''ActionRejected''
         WHEN JSON_VALUE(source.RequestJson, ''$.IsSurrender'') = ''true'' THEN ''Surrender''
         ELSE ''Move'' END,
    source.Rejection, SYSUTCDATETIME(), source.ActionId, source.RequestJson, source.ResultJson
FROM #ActionsToMigrate AS source WHERE source.ExistingEventId IS NULL;
DROP TABLE #ActionsToMigrate;');
    END;

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

    -- Drop only after every copy and target constraint has succeeded.
    IF OBJECT_ID(N'dbo.GameActionReceipts', N'U') IS NOT NULL DROP TABLE dbo.GameActionReceipts;
    IF OBJECT_ID(N'dbo.GameProgresses', N'U') IS NOT NULL DROP TABLE dbo.GameProgresses;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

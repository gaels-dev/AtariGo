using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal static class GameSessionTestData
{
    public static GameSessionSnapshot Snapshot(long version)
    {
        GameSessionSnapshot snapshot = new GameSessionSnapshot
        {
            GameId = 1,
            BoardSize = 9,
            CaptureGoal = 1,
            TurnTimeSeconds = 30,
            PlayerName = "Black",
            OpponentName = "White",
            PlayerStoneColor = "Black",
            OpponentStoneColor = "White",
            OpponentConnected = true,
            MaximumChatMessageLength = 100,
            Status = "InProgress",
            Version = version,
            CurrentTurn = GameStoneState.Black,
            BlackRemainingMilliseconds = 30000,
            WhiteRemainingMilliseconds = 30000
        };
        for (int row = 0; row < snapshot.BoardSize; row++)
        {
            for (int column = 0; column < snapshot.BoardSize; column++)
            {
                snapshot.Cells.Add(new GameBoardCell
                {
                    Row = row,
                    Column = column,
                    Stone = GameStoneState.Empty
                });
            }
        }
        return snapshot;
    }
}

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public static class GameSessionSnapshotValidator
{
    public static bool IsValid(GameSessionSnapshot snapshot, long gameId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!MatchesHeader(snapshot, gameId))
        {
            return false;
        }
        HashSet<int> cellIndexes = new HashSet<int>();
        bool valid = true;
        foreach (GameBoardCell cell in snapshot.Cells)
        {
            if (!IsWithinBoard(cell, snapshot.BoardSize)
                || !cellIndexes.Add(cell.Row * snapshot.BoardSize + cell.Column))
            {
                valid = false;
                break;
            }
        }
        return valid;
    }

    private static bool MatchesHeader(GameSessionSnapshot snapshot, long gameId)
    {
        return snapshot.GameId == gameId && snapshot.BoardSize == GameRules.BoardSize
            && snapshot.Cells.Count == GameRules.BoardSize * GameRules.BoardSize
            && IsValidColor(snapshot.PlayerStoneColor) && IsValidColor(snapshot.OpponentStoneColor)
            && snapshot.MaximumChatMessageLength > 0;
    }

    private static bool IsWithinBoard(GameBoardCell cell, int boardSize)
    {
        return cell.Row >= 0 && cell.Row < boardSize && cell.Column >= 0 && cell.Column < boardSize;
    }

    private static bool IsValidColor(string color)
    {
        return color is "Black" or "White";
    }
}

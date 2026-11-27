using AtariGo.GameLogic.Models;

namespace AtariGo.GameLogic.Rules;

internal static class BoardGroups
{
    public static IEnumerable<int> GetNeighbors(int index)
    {
        int row = index / GameState.BoardSize;
        int column = index % GameState.BoardSize;
        if (row > 0)
        {
            yield return index - GameState.BoardSize;
        }
        if (row < GameState.BoardSize - 1)
        {
            yield return index + GameState.BoardSize;
        }
        if (column > 0)
        {
            yield return index - 1;
        }
        if (column < GameState.BoardSize - 1)
        {
            yield return index + 1;
        }
    }

    public static HashSet<int> FindGroup(StoneColor[] board, int start)
    {
        HashSet<int> group = [start];
        Stack<int> pending = new Stack<int>();
        pending.Push(start);
        while (pending.TryPop(out int current))
        {
            foreach (int neighbor in GetNeighbors(current))
            {
                if (board[neighbor] == board[start] && group.Add(neighbor))
                {
                    pending.Push(neighbor);
                }
            }
        }
        return group;
    }

    public static bool HasLiberties(StoneColor[] board, HashSet<int> group)
    {
        return group.Any(index => GetNeighbors(index)
            .Any(neighbor => board[neighbor] == StoneColor.Empty));
    }

    public static int RemoveCapturedGroups(StoneColor[] board, int placedIndex)
    {
        int captured = 0;
        foreach (int neighbor in GetNeighbors(placedIndex))
        {
            if (board[neighbor] == StoneColor.Empty || board[neighbor] == board[placedIndex])
            {
                continue;
            }
            HashSet<int> group = FindGroup(board, neighbor);
            if (!HasLiberties(board, group))
            {
                foreach (int index in group)
                {
                    board[index] = StoneColor.Empty;
                }
                captured += group.Count;
            }
        }
        return captured;
    }
}

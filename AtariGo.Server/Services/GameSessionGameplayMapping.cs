using AtariGo.Contracts;
using AtariGo.GameLogic.Models;
using AtariGo.Server.Application.Games;

using DomainSnapshot = AtariGo.Server.Application.Games.GameSessionSnapshot;
using ProtoSnapshot = AtariGo.Contracts.GameSessionSnapshot;

namespace AtariGo.Server.Services;

internal static class GameSessionGameplayMapping
{
    public static void Apply(ProtoSnapshot target, DomainSnapshot source, DateTime now)
    {
        GameRuntimeState? progress = source.Progress;
        if (progress is null)
        {
            return;
        }
        GameState board = progress.Board;
        target.Version = source.Version;
        target.Status = progress.Status;
        target.CurrentTurn = MapStone(board.CurrentTurn);
        target.BlackCaptures = board.BlackCaptures;
        target.WhiteCaptures = board.WhiteCaptures;
        target.Winner = MapStone(board.Winner);
        target.EndReason = progress.EndReason;
        target.TurnNumber = board.TurnNumber;
        target.HasLastMove = board.LastMove.HasValue;
        if (board.LastMove is BoardPosition position)
        {
            target.LastMoveRow = position.Row;
            target.LastMoveColumn = position.Column;
        }
        long full = (long)TimeSpan.FromSeconds(source.TurnTimeSeconds).TotalMilliseconds;
        long remaining = Math.Clamp((long)(progress.DeadlineUtc - now).TotalMilliseconds, 0, full);
        bool playing = progress.Status == GameRuntimeState.Playing;
        target.BlackRemainingMilliseconds = playing && board.CurrentTurn == StoneColor.Black
            ? remaining : full;
        target.WhiteRemainingMilliseconds = playing && board.CurrentTurn == StoneColor.White
            ? remaining : full;
        target.TurnEndsAtUnixMilliseconds = new DateTimeOffset(
            DateTime.SpecifyKind(progress.DeadlineUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        target.ServerTimeUnixMilliseconds = new DateTimeOffset(
            DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }

    private static GameStoneState MapStone(StoneColor stone)
    {
        return stone switch
        {
            StoneColor.Black => GameStoneState.Black,
            StoneColor.White => GameStoneState.White,
            _ => GameStoneState.Empty
        };
    }
}

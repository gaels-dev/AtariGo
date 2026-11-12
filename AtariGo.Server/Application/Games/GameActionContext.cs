using AtariGo.GameLogic.Models;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameActionContext
{
    public GameActionCommand Command { get; init; } = null!;
    public Game Game { get; init; } = null!;
    public GameRuntimeState Runtime { get; set; } = null!;
    public GamePlayer Player { get; init; } = null!;
    public DateTime Now { get; set; }
    public StoneColor PlayerColor => Player.StoneColor == GameSessionRules.BlackStoneColor
        ? StoneColor.Black : StoneColor.White;
}

using AtariGo.GameLogic.Models;

namespace AtariGo.GameLogic.Actions;

public sealed record MoveCommand(StoneColor Player, BoardPosition Position);

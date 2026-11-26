namespace AtariGo.Server.Application.Games;

internal sealed record GameActionReceipt(GameActionCommand Command, GameActionResult Result);

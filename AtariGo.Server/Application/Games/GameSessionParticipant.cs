namespace AtariGo.Server.Application.Games;

public sealed record GameSessionParticipant(
    string PlayerKey,
    string DisplayName,
    string StoneColor);

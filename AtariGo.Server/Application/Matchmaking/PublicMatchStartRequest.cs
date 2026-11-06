using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.Matchmaking;

public sealed record PublicMatchStartRequest(
    MatchmakingPlayer BlackPlayer,
    MatchmakingPlayer WhitePlayer,
    LobbyConfiguration Configuration);

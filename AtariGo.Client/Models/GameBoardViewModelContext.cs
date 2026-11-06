using AtariGo.Client.Services;

namespace AtariGo.Client.Models;

public sealed record GameBoardViewModelContext(
    IGameSessionClient? SessionClient,
    bool IsGuest,
    string PlayerName);

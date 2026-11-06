using AtariGo.Contracts;

namespace AtariGo.Client.Models;

public sealed record PublicMatchNavigationContext(
    PublicGameStarted Game,
    bool IsGuest,
    string PlayerName);

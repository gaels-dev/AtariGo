namespace AtariGo.Server.Application.Matchmaking;

public sealed record IssuedGuestIdentity(
    string Credential,
    GuestIdentity Identity);

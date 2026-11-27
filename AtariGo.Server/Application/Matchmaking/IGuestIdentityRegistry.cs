namespace AtariGo.Server.Application.Matchmaking;

public interface IGuestIdentityRegistry
{
    IssuedGuestIdentity Create(string displayName);

    GuestIdentity? Find(string credential);

    bool Revoke(string credential);
}

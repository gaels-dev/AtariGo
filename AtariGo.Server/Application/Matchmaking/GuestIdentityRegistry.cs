using System.Security.Cryptography;
using System.Text;

namespace AtariGo.Server.Application.Matchmaking;

public sealed class GuestIdentityRegistry : IGuestIdentityRegistry
{
    private const int CredentialByteLength = 32;
    private const int MaximumDisplayNameLength = 100;
    private static readonly TimeSpan identityLifetime = TimeSpan.FromHours(1);

    private readonly object _sync = new object();
    private readonly Dictionary<string, GuestIdentity> _identities =
        new Dictionary<string, GuestIdentity>(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public GuestIdentityRegistry(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public IssuedGuestIdentity Create(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(displayName));
        }

        GuestIdentity identity = new GuestIdentity(
            Guid.NewGuid(),
            displayName,
            _timeProvider.GetUtcNow().Add(identityLifetime));
        string credential = CreateCredential(identity);
        return new IssuedGuestIdentity(credential, identity);
    }

    public GuestIdentity? Find(string credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return null;
        }

        string credentialHash = Hash(credential);
        lock (_sync)
        {
            if (!_identities.TryGetValue(credentialHash, out GuestIdentity? identity))
            {
                return null;
            }

            if (identity.ExpiresAt <= _timeProvider.GetUtcNow())
            {
                _identities.Remove(credentialHash);
                return null;
            }

            GuestIdentity renewedIdentity = identity with
            {
                ExpiresAt = _timeProvider.GetUtcNow().Add(identityLifetime)
            };
            _identities[credentialHash] = renewedIdentity;
            return renewedIdentity;
        }
    }

    public bool Revoke(string credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return false;
        }

        lock (_sync)
        {
            return _identities.Remove(Hash(credential));
        }
    }

    private string CreateCredential(GuestIdentity identity)
    {
        string credential;
        string credentialHash;
        do
        {
            credential = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(CredentialByteLength));
            credentialHash = Hash(credential);
        }
        while (!TryAddIdentity(credentialHash, identity));

        return credential;
    }

    private bool TryAddIdentity(string credentialHash, GuestIdentity identity)
    {
        lock (_sync)
        {
            RemoveExpiredIdentities(_timeProvider.GetUtcNow());
            if (_identities.ContainsKey(credentialHash))
            {
                return false;
            }

            _identities.Add(credentialHash, identity);
            return true;
        }
    }

    private void RemoveExpiredIdentities(DateTimeOffset currentTime)
    {
        List<string> expiredCredentials = new List<string>();
        foreach (KeyValuePair<string, GuestIdentity> entry in _identities)
        {
            if (entry.Value.ExpiresAt <= currentTime)
            {
                expiredCredentials.Add(entry.Key);
            }
        }

        foreach (string credentialHash in expiredCredentials)
        {
            _identities.Remove(credentialHash);
        }
    }

    private static string Hash(string credential)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
    }
}

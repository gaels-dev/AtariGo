namespace AtariGo.Client.Models
{
    public sealed class ClientSessionContext
    {
        public bool IsGuest { get; init; }

        public string PlayerName { get; init; } = "Player";

        public string? SessionToken { get; init; }

        public string? GuestCredential { get; init; }
    }
}

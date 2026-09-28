namespace AtariGo.Client.Models
{
    public class PrivateRoomNavigationContext
    {
        public bool IsHost { get; init; }

        public string RoomCode { get; init; } = string.Empty;

        public int TargetCaptures { get; init; }
    }
}
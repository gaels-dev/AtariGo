using AtariGo.Contracts;

namespace AtariGo.Client.Models;

public sealed record PrivateRoomNavigationContext(RoomSnapshot Room, bool IsHost);

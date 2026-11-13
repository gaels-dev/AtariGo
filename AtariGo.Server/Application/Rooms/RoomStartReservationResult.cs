namespace AtariGo.Server.Application.Rooms;

public sealed record RoomStartReservationResult(
    RoomOperationStatus Status,
    PrivateRoom? Room = null,
    RoomStartReservation? Reservation = null);

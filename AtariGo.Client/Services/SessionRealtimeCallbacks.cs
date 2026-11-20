using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed record SessionRealtimeCallbacks(
    Action<SessionServerEvent> EventReceived,
    Action<ClientFailureReason> ConnectionFailed,
    Action SessionExpired);

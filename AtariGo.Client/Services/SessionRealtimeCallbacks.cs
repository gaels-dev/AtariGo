using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed record SessionRealtimeCallbacks(
    Action<SessionServerEvent> EventReceived,
    Action<RpcException> ConnectionFailed,
    Action SessionExpired);

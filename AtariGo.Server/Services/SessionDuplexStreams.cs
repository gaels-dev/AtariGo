using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Server.Services;

internal sealed record SessionDuplexStreams(
    IAsyncStreamReader<SessionClientEvent> RequestStream,
    IServerStreamWriter<SessionServerEvent> ResponseStream);

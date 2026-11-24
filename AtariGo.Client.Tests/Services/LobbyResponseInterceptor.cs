using Grpc.Core.Interceptors;
using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class LobbyResponseInterceptor : Interceptor
{
    internal Task<SendRoomInvitationResponse> Response
    {
        get; set;
    } =
        Task.FromResult(new SendRoomInvitationResponse());

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return new AsyncUnaryCall<TResponse>((Task<TResponse>)(object)Response,
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
            () => new Metadata(), () => { });
    }
}

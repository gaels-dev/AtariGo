using Grpc.Core.Interceptors;
using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class LobbyStreamInterceptor : Interceptor
{
    internal required IAsyncStreamReader<MatchmakingEvent> Reader
    {
        get; init;
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<
        TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return new AsyncServerStreamingCall<TResponse>((IAsyncStreamReader<TResponse>)Reader,
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
            () => new Metadata(), () => { });
    }
}

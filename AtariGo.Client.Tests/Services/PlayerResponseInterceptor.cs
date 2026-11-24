using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AtariGo.Client.Tests.Services;

internal sealed class PlayerResponseInterceptor<T> : Interceptor
{
    internal required Task<T> Response
    {
        get; set;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return new AsyncUnaryCall<TResponse>((Task<TResponse>)(object)Response,
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
            () => new Metadata(), () => { });
    }
}

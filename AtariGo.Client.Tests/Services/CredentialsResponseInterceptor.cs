using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

public sealed class CredentialsResponseInterceptor : Interceptor
{
    public Task<ChangePasswordResponse> Response
    {
        get; set;
    } =
        Task.FromResult(new ChangePasswordResponse { Status = PasswordChangeStatus.Changed });

    public CallOptions Options
    {
        get; private set;
    }

    public int Calls
    {
        get; private set;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        Calls++;
        Options = context.Options;
        return new AsyncUnaryCall<TResponse>((Task<TResponse>)(object)Response,
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
            () => new Metadata(), () => { });
    }
}

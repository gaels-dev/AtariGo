using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

public sealed class RecoveryResponseInterceptor : Interceptor
{
    public Task<PasswordRecoveryResponse> Response { get; set; } =
        Task.FromResult(new PasswordRecoveryResponse { Status = RecoveryStatus.NotSaved });

    public int Calls { get; private set; }

    public CallOptions Options { get; private set; }

    public ResetPasswordRequest? Request { get; private set; }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        Calls++;
        Options = context.Options;
        Request = request as ResetPasswordRequest;
        return new AsyncUnaryCall<TResponse>((Task<TResponse>)(object)Response,
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
            () => new Metadata(), () => { });
    }
}

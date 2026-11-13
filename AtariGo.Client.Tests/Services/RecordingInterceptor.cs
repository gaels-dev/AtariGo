
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AtariGo.Client.Tests.Services
{
    public sealed class RecordingInterceptor : Interceptor
    {
        private readonly List<RecordedGrpcCall> _calls = new List<RecordedGrpcCall>();

        public IReadOnlyList<RecordedGrpcCall> Calls
        {
            get
            {
                return _calls;
            }
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            _calls.Add(new RecordedGrpcCall(
                context.Method.Name,
                context.Options,
                request));
            var response = (TResponse)(Activator.CreateInstance(typeof(TResponse))
                ?? throw new InvalidOperationException("A response instance is required."));

            return new AsyncUnaryCall<TResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<
            TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            _calls.Add(new RecordedGrpcCall(context.Method.Name, context.Options, null));
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "The test recorded the stream options."));
        }
    }
}

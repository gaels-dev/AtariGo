using Grpc.Core;

namespace AtariGo.Client.Tests.Services
{
    public sealed class RecordedGrpcCall
    {
        public RecordedGrpcCall(string methodName, CallOptions options, object request)
        {
            MethodName = methodName;
            Options = options;
            Request = request;
        }

        public string MethodName { get; }

        public CallOptions Options { get; }

        public Metadata Headers
        {
            get
            {
                return Options.Headers ?? new Metadata();
            }
        }

        public object Request { get; }
    }
}

using System.Net.Http;

using Grpc.Core;
using Grpc.Net.Client;

namespace AtariGo.Client.Services
{
    public sealed class GrpcClientConnection : IDisposable
    {
        private const int KeepAlivePingDelaySeconds = 5;
        private const int KeepAlivePingTimeoutSeconds = 5;

        private static readonly Lazy<GrpcClientConnection> sharedConnection =
            new Lazy<GrpcClientConnection>(
                () => new GrpcClientConnection(GrpcClientSettings.ServerAddress));

        private readonly GrpcChannel _channel;

        public GrpcClientConnection(Uri serverAddress)
        {
            ArgumentNullException.ThrowIfNull(serverAddress);

            if (!serverAddress.IsAbsoluteUri)
            {
                throw new ArgumentException(
                    "An absolute HTTPS server address is required.",
                    nameof(serverAddress));
            }

            ServerAddress = GrpcClientSettings.ParseServerAddress(serverAddress.OriginalString);
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                KeepAlivePingDelay = TimeSpan.FromSeconds(KeepAlivePingDelaySeconds),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(KeepAlivePingTimeoutSeconds),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
                EnableMultipleHttp2Connections = true
            };
            _channel = GrpcChannel.ForAddress(ServerAddress, new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            CallInvoker = _channel.CreateCallInvoker();
        }

        public static GrpcClientConnection Shared
        {
            get
            {
                return sharedConnection.Value;
            }
        }

        public Uri ServerAddress
        {
            get;
        }

        public CallInvoker CallInvoker
        {
            get;
        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}

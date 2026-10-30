using System;

using Grpc.Core;
using Grpc.Net.Client;

namespace AtariGo.Client.Services
{
    /// <summary>Owns the reusable transport shared by the application's gRPC adapters.</summary>
    public sealed class GrpcClientConnection : IDisposable
    {
        private static readonly Lazy<GrpcClientConnection> s_sharedConnection =
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
            _channel = GrpcChannel.ForAddress(ServerAddress);
            CallInvoker = _channel.CreateCallInvoker();
        }

        public static GrpcClientConnection Shared
        {
            get
            {
                return s_sharedConnection.Value;
            }
        }

        public Uri ServerAddress { get; }

        public CallInvoker CallInvoker { get; }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}

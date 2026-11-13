using System.Net;

namespace AtariGo.Client.Services
{
    public static class GrpcClientSettings
    {
        private const string ServerAddressEnvironmentVariable =
            "ATARI_GO_GRPC_SERVER_ADDRESS";
        private const string DefaultDevelopmentServerAddress =
            "https://localhost:7061";

        public static Uri ServerAddress
        {
            get
            {
                string? configuredAddress = Environment.GetEnvironmentVariable(
                    ServerAddressEnvironmentVariable);

                return ParseServerAddress(configuredAddress);
            }
        }

        public static Uri ParseServerAddress(string? configuredAddress)
        {
            string address = string.IsNullOrWhiteSpace(configuredAddress)
                ? DefaultDevelopmentServerAddress
                : configuredAddress.Trim();

            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? serverAddress)
                || !IsServerAddressValid(serverAddress))
            {
                throw new ArgumentException(
                    "The gRPC server address must be an HTTPS origin with a concrete host "
                        + "and a valid port, without credentials, a path, a query, or a fragment.",
                    nameof(configuredAddress));
            }

            return serverAddress;
        }

        private static bool IsServerAddressValid(Uri serverAddress)
        {
            return serverAddress.Scheme == Uri.UriSchemeHttps
                && HasConcreteHost(serverAddress)
                && serverAddress.Port != IPEndPoint.MinPort
                && string.IsNullOrEmpty(serverAddress.UserInfo)
                && serverAddress.AbsolutePath == "/"
                && string.IsNullOrEmpty(serverAddress.Query)
                && string.IsNullOrEmpty(serverAddress.Fragment);
        }

        private static bool HasConcreteHost(Uri serverAddress)
        {
            bool isWildcardAddress = IPAddress.TryParse(
                serverAddress.Host,
                out IPAddress? hostAddress)
                && (hostAddress.Equals(IPAddress.Any)
                    || hostAddress.Equals(IPAddress.IPv6Any));

            return !string.IsNullOrWhiteSpace(serverAddress.Host)
                && serverAddress.HostNameType != UriHostNameType.Unknown
                && !isWildcardAddress;
        }
    }
}

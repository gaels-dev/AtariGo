namespace AtariGo.Client.Services;

public static class GrpcClientSettings
{
    private const string ServerAddressEnvironmentVariable =
        "ATARI_GO_GRPC_SERVER_ADDRESS";
    private const string DefaultDevelopmentServerAddress =
        "https://localhost:7061";

    public static string ServerAddress
    {
        get
        {
            string? configuredAddress = Environment.GetEnvironmentVariable(
                ServerAddressEnvironmentVariable);

            return string.IsNullOrWhiteSpace(configuredAddress)
                ? DefaultDevelopmentServerAddress
                : configuredAddress;
        }
    }
}

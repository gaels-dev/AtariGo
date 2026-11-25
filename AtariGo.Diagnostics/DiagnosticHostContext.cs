namespace AtariGo.Diagnostics;

public sealed class DiagnosticHostContext
{
    public DiagnosticHostContext(string? listeningAddress, string? environmentName)
    {
        ListeningAddress = NormalizeAddress(listeningAddress);
        EnvironmentName = environmentName is "Development" or "Staging" or "Production"
            ? environmentName : null;
    }

    public string? ListeningAddress
    {
        get;
    }

    public string? EnvironmentName
    {
        get;
    }

    private static string? NormalizeAddress(string? address)
    {
        if (address is null || address.Any(char.IsControl)
            || !Uri.TryCreate(address, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }
        return IsOrigin(uri) ? uri.GetComponents(UriComponents.SchemeAndServer,
            UriFormat.UriEscaped) : null;
    }

    private static bool IsOrigin(Uri address)
    {
        return address.Scheme is "http" or "https" && !string.IsNullOrEmpty(address.Host)
            && address.Port > 0 && address.AbsolutePath == "/"
            && string.IsNullOrEmpty(address.UserInfo) && string.IsNullOrEmpty(address.Query)
            && string.IsNullOrEmpty(address.Fragment);
    }
}

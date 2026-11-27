using System.Net;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace AtariGo.Server.Configuration;

public static class LanEndpointSettings
{
    public const string EnabledKey = "AtariGo:Lan:Enabled";
    public const string PortKey = "AtariGo:Lan:Port";
    public const string CertificateThumbprintKey = "AtariGo:Lan:CertificateThumbprint";

    private const int DefaultPort = 7062;
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    public static X509Certificate2? Configure(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.Configuration.GetValue<bool>(EnabledKey))
        {
            return null;
        }

        int port = GetPort(builder.Configuration);
        X509Certificate2 certificate = LoadCertificate(
            builder.Configuration[CertificateThumbprintKey]);
        builder.WebHost.ConfigureKestrel(server => server.ListenAnyIP(port, endpoint =>
        {
            endpoint.Protocols = HttpProtocols.Http2;
            endpoint.UseHttps(certificate);
        }));
        return certificate;
    }

    public static int GetPort(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        int port = configuration.GetValue(PortKey, DefaultPort);
        string? localAddress = configuration["Kestrel:Endpoints:Grpc:Url"];
        if (port <= IPEndPoint.MinPort || port > IPEndPoint.MaxPort
            || (Uri.TryCreate(localAddress, UriKind.Absolute, out Uri? localEndpoint)
                && port == localEndpoint.Port))
        {
            throw new InvalidOperationException(
                "The LAN port must be valid and different from the local gRPC port.");
        }

        return port;
    }

    private static X509Certificate2 LoadCertificate(string? thumbprint)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The LAN certificate must be provisioned in the Windows certificate store.");
        }
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            throw new InvalidOperationException(
                "Run New-LanCertificate.ps1 before enabling the LAN endpoint.");
        }

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        X509Certificate2Collection matches = store.Certificates.Find(
            X509FindType.FindByThumbprint, thumbprint.Trim(), validOnly: false);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "The configured LAN certificate was not found in CurrentUser/My.");
        }

        X509Certificate2 certificate = matches[0];
        ValidateCertificate(certificate);
        return certificate;
    }

    private static void ValidateCertificate(X509Certificate2 certificate)
    {
        DateTime now = DateTime.UtcNow;
        X509BasicConstraintsExtension? constraints = certificate.Extensions
            .OfType<X509BasicConstraintsExtension>().FirstOrDefault();
        X509EnhancedKeyUsageExtension? enhancedUsage = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        bool isServerCertificate = enhancedUsage?.EnhancedKeyUsages
            .Cast<System.Security.Cryptography.Oid>()
            .Any(usage => usage.Value == ServerAuthenticationOid) == true;
        bool hasSubjectAlternativeName = certificate.Extensions
            .OfType<X509SubjectAlternativeNameExtension>().Any();
        if (!certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > now
            || certificate.NotAfter.ToUniversalTime() <= now
            || constraints?.CertificateAuthority != false
            || !isServerCertificate || !hasSubjectAlternativeName)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "The configured LAN certificate is invalid for TLS server authentication.");
        }
    }
}

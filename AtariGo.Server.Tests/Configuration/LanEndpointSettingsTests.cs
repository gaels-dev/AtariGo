using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

using AtariGo.Server.Configuration;

namespace AtariGo.Server.Tests.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class LanEndpointSettingsTests
{
    [TestMethod]
    public void Configure_LanDisabled_DoesNotRequireCertificate()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [LanEndpointSettings.EnabledKey] = "false",
            [LanEndpointSettings.CertificateThumbprintKey] = null
        });

        // Act
        X509Certificate2? certificate = LanEndpointSettings.Configure(builder);

        // Assert
        Assert.IsNull(certificate);
    }

    [TestMethod]
    public void Configure_LanEnabledWithoutCertificate_RejectsConfiguration()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [LanEndpointSettings.EnabledKey] = "true",
            [LanEndpointSettings.CertificateThumbprintKey] = null,
            ["Kestrel:Endpoints:Grpc:Url"] = "https://localhost:7061"
        });

        // Act
        Action configure = () => LanEndpointSettings.Configure(builder);

        // Assert
        InvalidOperationException failure =
            Assert.ThrowsExactly<InvalidOperationException>(configure);
        StringAssert.Contains(failure.Message, "New-LanCertificate.ps1");
    }

    [TestMethod]
    [DataRow("7061")]
    [DataRow("0")]
    [DataRow("65536")]
    public void GetPort_InvalidOrConflictingPort_RejectsConfiguration(string value)
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [LanEndpointSettings.PortKey] = value,
                ["Kestrel:Endpoints:Grpc:Url"] = "https://localhost:7061"
            }).Build();

        // Act
        Action validate = () => LanEndpointSettings.GetPort(configuration);

        // Assert
        Assert.ThrowsExactly<InvalidOperationException>(validate);
    }

    [TestMethod]
    public void GetPort_NoOverride_UsesSeparateLanPort()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Kestrel:Endpoints:Grpc:Url"] = "https://localhost:7061"
            }).Build();

        // Act
        int port = LanEndpointSettings.GetPort(configuration);

        // Assert
        Assert.AreEqual(7062, port);
    }
}

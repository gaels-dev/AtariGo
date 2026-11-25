using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
public sealed class FrameworkLoggingTests
{
    private const int DynamicPort = 0;
    private readonly TestContext _testContext;

    public FrameworkLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Start_KestrelOverridesUrls_RecordsWarningAndLifecycleAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        KestrelServerOptions options = host.Application.Services
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        options.Listen(IPAddress.Loopback, DynamicPort,
            endpoint => endpoint.Protocols = HttpProtocols.Http2);
        host.Application.Urls.Add("http://127.0.0.1:1");
        // Act
        await host.Application.StartAsync(_testContext.CancellationToken);
        await host.Application.StopAsync(_testContext.CancellationToken);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        JsonElement warning = entries.Single(entry => entry.GetProperty("Operation").GetString()
            == "KestrelEndpointsOverrideUrls");
        Assert.AreEqual("Warning", warning.GetProperty("Severity").GetString());
        string?[] operations = entries.Select(entry =>
            entry.GetProperty("Operation").GetString()).ToArray();
        Assert.Contains("ServerListening", operations);
        Assert.Contains("ApplicationStarted", operations);
        Assert.Contains("ApplicationStopping", operations);
        Assert.Contains("HostingEnvironmentConfigured", operations);
        Assert.Contains("ContentRootConfigured", operations);
        Assert.Contains(message => message.StartsWith("Now listening on: http://127.0.0.1:",
            StringComparison.Ordinal),
            host.Records.Messages);
    }

    [TestMethod]
    [DataRow("Application started. Press Ctrl+C to shut down.", "ApplicationStarted")]
    [DataRow("Application is shutting down...", "ApplicationStopping")]
    [DataRow("Now listening on: {address}", "ServerListening")]
    [DataRow("Hosting environment: {EnvName}", "HostingEnvironmentConfigured")]
    [DataRow("Content root path: {ContentRoot}", "ContentRootConfigured")]
    [DataRow("Overriding address(es) '{addresses}'. Binding to endpoints defined via "
        + "IConfiguration and/or UseKestrel() instead.", "KestrelEndpointsOverrideUrls")]
    [DataRow("Overriding endpoints defined via IConfiguration and/or UseKestrel() because "
        + "{settingName} is set to true. Binding to address(es) '{addresses}' instead.",
        "HostingUrlsOverrideKestrelEndpoints")]
    [DataRow("The ASP.NET Core developer certificate is not trusted. For information about "
        + "trusting the ASP.NET Core developer certificate, see "
        + "https://aka.ms/aspnet/https-trust-dev-cert", "DevelopmentCertificateNotTrusted")]
    [DataRow("The ASP.NET Core developer certificate is only trusted by some clients. "
        + "For information about trusting the ASP.NET Core developer certificate, see "
        + "https://aka.ms/aspnet/https-trust-dev-cert",
        "DevelopmentCertificatePartiallyTrusted")]
    [DataRow("HTTP/2 is not enabled for {Endpoint}. The endpoint is configured to use HTTP/1.1 "
        + "and HTTP/2, but TLS is not enabled. HTTP/2 requires TLS application protocol "
        + "negotiation. Connections to this endpoint will use HTTP/1.1.",
        "Http2DisabledWithoutTls")]
    public async Task Log_KnownFrameworkTemplate_PreservesMeaningWithoutPayloadAsync(
        string template, string expectedOperation)
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        ILogger logger = host.Application.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Microsoft.AspNetCore.Server.Kestrel");
        const string secret = "https://private-user:private-password@example.invalid/private";
        var state = new Dictionary<string, object?>
        {
            ["{OriginalFormat}"] = template,
            ["addresses"] = secret,
            ["address"] = secret,
            ["Endpoint"] = secret,
            ["ContentRoot"] = secret,
            ["EnvName"] = secret,
            ["settingName"] = secret
        };
        // Act
        logger.Log(LogLevel.Warning, new EventId(0, secret), state, null,
            (_, _) => throw new InvalidOperationException("Raw formatter must not run."));
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(1, entries);
        Assert.AreEqual(expectedOperation, entries[0].GetProperty("Operation").GetString());
        Assert.AreEqual(0, entries[0].GetProperty("TraceEventId").GetInt32());
        Assert.AreEqual(0, host.Records.EventIds.Single().Id);
        Assert.AreEqual("Warning", entries[0].GetProperty("Severity").GetString());
        Assert.DoesNotContain(secret, entries[0].GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, host.Records.Messages.Single(), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Log_UnknownFrameworkTemplate_DoesNotTrustEventNameOrPayloadAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        ILogger logger = host.Application.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Microsoft.AspNetCore.Server.Kestrel");
        const string secret = "private@example.invalid";
        // Act
        logger.LogWarning(new EventId(0, secret), "Unknown framework warning: {Payload}", secret);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual("Event_0", entries.Single().GetProperty("Operation").GetString());
        Assert.DoesNotContain(secret, host.Records.Messages.Single(), StringComparison.Ordinal);
    }
}

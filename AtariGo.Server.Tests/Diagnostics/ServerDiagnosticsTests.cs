using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Grpc.Core;

using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
[OSCondition(OperatingSystems.Windows)]
public sealed class ServerDiagnosticsTests
{
    private readonly TestContext _testContext;

    public ServerDiagnosticsTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Log_PrivatePayload_SanitizesBeforeEveryProviderAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        ILogger logger = host.Application.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AtariGo.Server.Security");
        const string secret = "synthetic-private-payload@example.invalid";
        var failure = new IOException(secret, new InvalidOperationException(secret));
        using Activity activity = new Activity("Synthetic").SetIdFormat(ActivityIdFormat.W3C)
            .AddBaggage("Token", secret).AddTag("Password", secret).Start();
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = 8,
            ["RoomId"] = -1,
            ["Token"] = secret
        });
        // Act
        logger.LogError(new EventId(17, secret), failure,
            "Private {Email} {GameId} {Status}", secret, 42L, StatusCode.Unavailable);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        DiagnosticEvent entry = (DiagnosticEvent)host.Records.States.Single();
        Assert.AreEqual(42L, entry.Details.Context!.GameId);
        Assert.AreEqual(8, entry.Details.Context.UserId);
        Assert.IsNull(entry.Details.Context.RoomId);
        Assert.AreEqual(activity.TraceId, entry.Details.Context.TraceId);
        Assert.AreEqual("Unavailable", entry.Details.Status!.Value);
        Assert.AreEqual(17, entry.Details.TraceEventId);
        Assert.HasCount(1, entries);
        Assert.DoesNotContain(secret, entries[0].GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, host.Records.Messages.Single(), StringComparison.Ordinal);
        Assert.IsEmpty(host.Records.Exceptions);
        Assert.IsEmpty(host.Records.Scopes);
        Assert.AreEqual(TimeSpan.FromHours(-6), entries[0].GetProperty("Timestamp")
            .GetDateTimeOffset().Offset);
    }

    [TestMethod]
    public async Task Log_ProviderFilters_ApplyBeforeFileAndRecordingDestinationsAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        ILogger logger = host.Application.Logger;
        // Act
        logger.LogDebug("Filtered debug");
        logger.LogInformation("Recorded information");
        logger.LogWarning("Persisted warning");
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(2, host.Records.Messages);
        Assert.HasCount(1, entries);
        Assert.AreEqual("Warning", entries[0].GetProperty("Severity").GetString());
        Assert.IsFalse(logger.IsEnabled(LogLevel.None));
    }

    [TestMethod]
    public async Task Log_UnsafeFormatterAndLateProvider_NeverReceivesRawStateAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        ILoggerFactory factory = host.Application.Services.GetRequiredService<ILoggerFactory>();
        ILogger logger = factory.CreateLogger("private\r\ncategory");
        using var late = new RecordingLogProvider();
        factory.AddProvider(late);
        // Act
        logger.Log(LogLevel.Error, new EventId(-3, "private"), "private", null,
            (_, _) => throw new InvalidOperationException("Formatter must not run."));
        await host.Diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        DiagnosticEvent entry = (DiagnosticEvent)late.States.Single();
        Assert.AreEqual("AtariGo.Server.UnknownSource", entry.Definition.Source);
        Assert.AreEqual(-3, entry.Details.TraceEventId);
        Assert.AreEqual(host.Records.Messages.Single(), late.Messages.Single());
        Assert.DoesNotContain("private", late.Messages.Single(), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task BeginScope_ConcurrentOwners_KeepIdentifiersIsolatedAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        ILogger logger = host.Application.Logger;
        void LogOwner(int identifier)
        {
            using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["GameId"] = (long)identifier
            });
            logger.LogWarning("Owner transition");
        }
        // Act
        Parallel.For(1, 65, LogOwner);
        logger.LogWarning("Outside scope");
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(65, entries);
        Assert.HasCount(64, host.Records.States.Cast<DiagnosticEvent>()
            .Select(entry => entry.Details.Context!.GameId).OfType<long>().Distinct());
        Assert.IsNull(((DiagnosticEvent)host.Records.States.Last()).Details.Context!.GameId);
    }

    [TestMethod]
    public async Task Log_TypedEvent_PreservesOperationAndContextAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        var definition = new DiagnosticEventDefinition(2010, "AtariGo.Server", "SessionStarted");
        var details = new DiagnosticEventDetails { Context = new DiagnosticContext { UserId = 9 } };
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow);
        // Act
        host.Application.Logger.Log(LogLevel.Warning, new EventId(2010), entry, null,
            (_, _) => "unused");
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual("SessionStarted", entries[0].GetProperty("Operation").GetString());
        Assert.AreEqual(9, entries[0].GetProperty("UserId").GetInt32());
        Assert.AreEqual("Warning", entries[0].GetProperty("Severity").GetString());
    }

    [TestMethod]
    [DataRow("RpcException")]
    [DataRow("DbUpdateException")]
    [DataRow("SmtpCommandException")]
    [DataRow("SocketException")]
    [DataRow("AuthenticationException")]
    public async Task Log_KnownErrorTypeWithoutException_PreservesSafeCauseAsync(string typeName)
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        // Act
        host.Application.Logger.LogWarning("Transport failed with {ErrorType}.", typeName);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual(typeName, entries[0].GetProperty("Error")
            .GetProperty("TypeName").GetString());
        Assert.AreEqual(JsonValueKind.Null, entries[0].GetProperty("Error")
            .GetProperty("HResult").ValueKind);
    }

    [TestMethod]
    public async Task Log_DefaultInformation_PersistsKnownOperationAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        // Act
        host.Application.Logger.LogInformation("Real-time session stream stopped by cancellation.");
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(1, entries);
        Assert.AreEqual("RealTimeSessionStreamStoppedByCancellation",
            entries[0].GetProperty("Operation").GetString());
    }

    [TestMethod]
    public async Task Log_ReloadedConfiguration_UpdatesProviderFiltersAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        host.Application.Logger.LogInformation("Before reload");
        // Act
        host.Application.Configuration["Logging:LogLevel:Default"] = "Error";
        host.Application.Configuration["Logging:DiagnosticFile:LogLevel:Default"] = "Error";
        ((IConfigurationRoot)host.Application.Configuration).Reload();
        host.Application.Logger.LogWarning("Filtered after reload");
        host.Application.Logger.LogError("Persisted after reload");
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(2, entries);
        Assert.HasCount(2, host.Records.Messages);
    }

    [TestMethod]
    public async Task BeginScope_TypedContext_MergesNumericIdentifiersAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        Guid operation = Guid.NewGuid();
        using IDisposable? scope = host.Application.Logger.BeginScope(new DiagnosticContext
        {
            RoomId = 3,
            UserId = 2,
            RequestId = 4,
            InvitationId = 5,
            OperationId = operation,
            ActionId = operation
        });
        // Act
        host.Application.Logger.LogWarning("Transition {RequestId} {InvitationId} {GameId}",
            6, 7, -1);
        await host.Diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        DiagnosticEvent entry = (DiagnosticEvent)host.Records.States.Single();
        DiagnosticContext context = entry.Details.Context!;
        Assert.AreEqual(3, context.RoomId);
        Assert.AreEqual(2, context.UserId);
        Assert.AreEqual(6L, context.RequestId);
        Assert.AreEqual(7L, context.InvitationId);
        Assert.IsNull(context.GameId);
        Assert.AreEqual(operation, context.OperationId);
        Assert.AreEqual(operation, context.ActionId);
    }

    [TestMethod]
    public async Task Dispose_Provider_DrainsAcceptedEventsWithoutClosingBorrowedWriterAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost();
        using ILoggerProvider provider = host.Diagnostics.CreateProvider();
        ILogger logger = provider.CreateLogger("AtariGo.Server");
        using IDisposable? scope = logger.BeginScope("private");
        var definition = new DiagnosticEventDefinition(2020, "AtariGo.Server", "Stored");
        var entry = new DiagnosticEvent(definition, new DiagnosticEventDetails(),
            DateTimeOffset.UtcNow);
        // Act
        logger.LogInformation("private raw payload");
        logger.Log(LogLevel.Warning, new EventId(2020), entry, null, (_, _) => "unused");
        provider.Dispose();
        logger.Log(LogLevel.Warning, new EventId(2020), entry, null, (_, _) => "unused");
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(1, entries);
        Assert.AreEqual(1, host.Diagnostics.Status.Written);
        Assert.IsFalse(logger.IsEnabled(LogLevel.Warning));
    }
}

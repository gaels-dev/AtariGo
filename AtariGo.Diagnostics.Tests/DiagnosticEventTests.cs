using System.Diagnostics;
using System.Text.Json;

namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticEventTests
{
    [TestMethod]
    public void Constructor_LocalTimestamp_NormalizesInstantToUtc()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Client", "Login");
        var details = new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Started };
        var timestamp = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.FromHours(-6));
        // Act
        var entry = new DiagnosticEvent(definition, details, timestamp);
        // Assert
        Assert.AreEqual(timestamp.UtcDateTime, entry.TimestampUtc.UtcDateTime);
        Assert.AreEqual(TimeSpan.Zero, entry.TimestampUtc.Offset);
        Assert.AreSame(definition, entry.Definition);
        Assert.AreSame(details, entry.Details);
    }

    [TestMethod]
    public void Constructor_MissingOptionalData_PreservesAbsence()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Client", "Startup");
        var details = new DiagnosticEventDetails();
        // Act
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
        // Assert
        Assert.AreEqual(DiagnosticSeverity.Information, entry.Details.Severity);
        Assert.AreEqual(DiagnosticOutcome.Unspecified, entry.Details.Outcome);
        Assert.IsNull(entry.Details.Context);
        Assert.IsNull(entry.Details.Error);
    }

    [TestMethod]
    public void Constructor_AllowedContext_PreservesResourceAndCorrelationIdentifiers()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Server", "JoinGame");
        var context = new DiagnosticContext
        {
            GameId = 42,
            RoomId = 7,
            UserId = 3,
            RequestId = 11,
            InvitationId = 12,
            OperationId = Guid.Parse("2857f2c3-6fc3-4cf6-82d3-432e24fd55fe"),
            ActionId = Guid.Parse("c2938f3c-6758-4fa2-8553-685a3b5a1222"),
            TraceId = ActivityTraceId.CreateFromString("0123456789abcdef0123456789abcdef")
        };
        var details = new DiagnosticEventDetails { Context = context };
        // Act
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
        DiagnosticContext? captured = entry.Details.Context;
        // Assert
        Assert.IsNotNull(captured);
        Assert.AreSame(context, captured);
        Assert.AreEqual(42, captured.GameId);
        Assert.AreEqual(7, captured.RoomId);
        Assert.AreEqual(3, captured.UserId);
        Assert.AreEqual(11, captured.RequestId);
        Assert.AreEqual(12, captured.InvitationId);
        Assert.AreEqual(context.OperationId, captured.OperationId);
        Assert.AreEqual(context.ActionId, captured.ActionId);
        Assert.AreEqual(context.TraceId, captured.TraceId);
    }

    [TestMethod]
    public void Constructor_PrivateFailure_SerializesOnlySafeErrorData()
    {
        // Arrange
        const string privateValue = "synthetic-session-token";
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Server", "SaveGame");
        var details = new DiagnosticEventDetails
        {
            Severity = DiagnosticSeverity.Error,
            Outcome = DiagnosticOutcome.Failed,
            Context = new DiagnosticContext { GameId = 42 },
            Error = DiagnosticError.FromException(new IOException(privateValue), 2627)
        };
        // Act
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
        string serialized = JsonSerializer.Serialize(entry);
        // Assert
        Assert.DoesNotContain(privateValue, serialized, StringComparison.Ordinal);
        Assert.Contains(nameof(IOException), serialized, StringComparison.Ordinal);
        Assert.AreSame(details.Error, entry.Details.Error);
        Assert.AreEqual(DiagnosticSeverity.Error, entry.Details.Severity);
        Assert.AreEqual(DiagnosticOutcome.Failed, entry.Details.Outcome);
    }

    [TestMethod]
    public void Constructor_ContextCopy_DoesNotChangeCapturedEvent()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Server", "JoinGame");
        var context = new DiagnosticContext { GameId = 42 };
        var details = new DiagnosticEventDetails { Context = context };
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
        // Act
        DiagnosticEventDetails changed = details with { Context = context with { GameId = 99 } };
        // Assert
        Assert.AreEqual(42, entry.Details.Context!.GameId);
        Assert.AreEqual(99, changed.Context.GameId);
    }

    [TestMethod]
    public void Constructor_NullDefinition_RejectsInput()
    {
        // Arrange
        var details = new DiagnosticEventDetails();
        // Act
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new DiagnosticEvent(null!, details, DateTimeOffset.UnixEpoch));
        // Assert
        Assert.AreEqual("definition", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullDetails_RejectsInput()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Client", "Login");
        // Act
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new DiagnosticEvent(definition, null!, DateTimeOffset.UnixEpoch));
        // Assert
        Assert.AreEqual("details", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(6, 0)]
    [DataRow(2, -1)]
    [DataRow(2, 9)]
    public void Constructor_UnknownSeverityOrOutcome_RejectsEvent(int severity, int outcome)
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Client", "Login");
        var details = new DiagnosticEventDetails
        {
            Severity = (DiagnosticSeverity)severity,
            Outcome = (DiagnosticOutcome)outcome
        };
        // Act
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch));
        // Assert
        Assert.AreEqual("details", exception.ParamName);
    }

    [TestMethod]
    [DataRow("GameId", 0)]
    [DataRow("GameId", -1)]
    [DataRow("RoomId", 0)]
    [DataRow("RoomId", -1)]
    [DataRow("UserId", 0)]
    [DataRow("UserId", -1)]
    [DataRow("RequestId", 0)]
    [DataRow("RequestId", -1)]
    [DataRow("InvitationId", 0)]
    [DataRow("InvitationId", -1)]
    public void Constructor_NonpositiveContextId_RejectsEvent(string identifier, int value)
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Server", "JoinGame");
        var details = new DiagnosticEventDetails
        {
            Context = CreateInvalidContext(identifier, value)
        };
        // Act
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch));
        // Assert
        Assert.AreEqual("context", exception.ParamName);
    }

    private static DiagnosticContext CreateInvalidContext(string identifier, int value)
    {
        return identifier switch
        {
            "GameId" => new DiagnosticContext { GameId = value },
            "RoomId" => new DiagnosticContext { RoomId = value },
            "UserId" => new DiagnosticContext { UserId = value },
            "RequestId" => new DiagnosticContext { RequestId = value },
            "InvitationId" => new DiagnosticContext { InvitationId = value },
            _ => throw new ArgumentException("Unknown context identifier.", nameof(identifier))
        };
    }
}

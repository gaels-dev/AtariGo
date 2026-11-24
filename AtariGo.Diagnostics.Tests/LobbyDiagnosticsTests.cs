using System.Diagnostics;

namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class LobbyDiagnosticsTests
{
    [TestMethod]
    public void Create_ActiveTrace_PreservesCorrelationAndStableDomainIdentity()
    {
        // Arrange
        using Activity activity = new Activity("LobbyTest").SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var details = new DiagnosticEventDetails
        {
            Outcome = DiagnosticOutcome.Succeeded,
            Context = new DiagnosticContext { RoomId = 1, InvitationId = 2 }
        };
        // Act
        DiagnosticEvent server = LobbyDiagnostics.Create(LobbyOperation.InvitationCreated, details);
        DiagnosticEvent client = LobbyDiagnostics.Create(LobbyOperation.SendInvitationRequest,
            details, isClient: true);
        // Assert
        Assert.AreEqual(activity.TraceId, server.Details.Context?.TraceId);
        Assert.AreEqual(server.Details.Context, client.Details.Context);
        Assert.AreEqual("AtariGo.Server.Lobby", server.Definition.Source);
        Assert.AreEqual("AtariGo.Client.Lobby", client.Definition.Source);
        Assert.AreEqual((int)LobbyOperation.InvitationCreated, server.Definition.Id);
        Assert.AreNotEqual(server.Definition.Id, client.Definition.Id);
    }

    [TestMethod]
    [DataRow(LobbyOperation.Unspecified)]
    [DataRow((LobbyOperation)int.MaxValue)]
    public void Create_UndefinedOperation_RejectsUnstableEventIdentity(LobbyOperation operation)
    {
        // Arrange
        var details = new DiagnosticEventDetails();
        // Act
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LobbyDiagnostics.Create(operation, details));
    }
}

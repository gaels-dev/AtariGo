using System.Diagnostics;

namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class IdentityDiagnosticsTests
{
    [TestMethod]
    [DataRow(false, "AtariGo.Server.Identity")]
    [DataRow(true, "AtariGo.Client.Identity")]
    public void Create_Origins_PreserveStableIdentityAndAvailableTrace(bool client, string source)
    {
        // Arrange
        using Activity activity = new Activity("IdentityTest").SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var details = new DiagnosticEventDetails
        {
            Context = new DiagnosticContext { UserId = 7 }
        };
        // Act
        DiagnosticEvent entry = IdentityDiagnostics.Create(IdentityOperation.SendFriendRequest,
            details, client);
        // Assert
        Assert.AreEqual((int)IdentityOperation.SendFriendRequest, entry.Definition.Id);
        Assert.AreEqual(source, entry.Definition.Source);
        Assert.AreEqual(7, entry.Details.Context!.UserId);
        Assert.AreEqual(activity.TraceId, entry.Details.Context.TraceId);
        Assert.IsNull(details.Context.TraceId);
    }

    [TestMethod]
    public void Result_UnknownStatus_RecordsUnconfirmedWithoutInventingSuccess()
    {
        // Arrange
        DiagnosticOutcome undefined = (DiagnosticOutcome)999;
        // Act
        DiagnosticEventDetails details = IdentityDiagnostics.Result(
            IdentityOperation.SendFriendRequest, undefined);
        // Assert
        Assert.AreEqual(DiagnosticOutcome.Unconfirmed, details.Outcome);
        Assert.IsNull(details.Status);
    }
}

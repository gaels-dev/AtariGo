using AtariGo.Diagnostics;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Friends;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class IdentityResultTests
{
    [TestMethod]
    [DataRow(LoginStatus.Success, DiagnosticOutcome.Succeeded)]
    [DataRow(LoginStatus.ChallengeRequired, DiagnosticOutcome.Started)]
    [DataRow(LoginStatus.InvalidCode, DiagnosticOutcome.Rejected)]
    [DataRow(LoginStatus.Unavailable, DiagnosticOutcome.Unconfirmed)]
    public void Result_LoginOutcome_PreservesMeaning(LoginStatus status, DiagnosticOutcome expected)
    {
        DiagnosticEventDetails details = IdentityDiagnostics.Result(IdentityOperation.Login,
            status);
        Assert.AreEqual(expected, details.Outcome);
        Assert.AreEqual(status.ToString(), details.Status!.Value);
    }

    [TestMethod]
    public void Result_DeclinedFriendRequest_RecordsSuccessfulDecision()
    {
        // Arrange
        FriendRequestResponseResult status = FriendRequestResponseResult.Rejected;
        // Act
        DiagnosticEventDetails details = IdentityDiagnostics.Result(
            IdentityOperation.RespondToFriendRequest, status);
        // Assert
        Assert.AreEqual(DiagnosticOutcome.Succeeded, details.Outcome);
        Assert.AreEqual("Rejected", details.Status!.Value);
    }

    [TestMethod]
    public void Result_CompletedLoginCancellation_DoesNotReportChallengeRejection()
    {
        DiagnosticEventDetails details = IdentityDiagnostics.Result(IdentityOperation.CancelLogin,
            LoginStatus.InvalidChallenge);
        Assert.AreEqual(DiagnosticOutcome.Succeeded, details.Outcome);
        Assert.IsNull(details.Status);
    }
}

using System.Globalization;

using Grpc.Core;

using AtariGo.Client.Services;
using AtariGo.Client.Properties;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class ClientFailureMessagesTests
{
    [TestMethod]
    [DataRow(GrpcFailureCodes.AccountSuspended, "GameAccess_Err_Suspended")]
    [DataRow(GrpcFailureCodes.ParticipantSuspended,
        "GameAccess_Err_ParticipantSuspended")]
    [DataRow("room-membership-required", "GameAccess_Err_PermissionDenied")]
    public void GetGameAccessMessage_PermissionDenied_UsesExactReason(
        string errorCode, string expectedResourceKey)
    {
        // Arrange
        var trailers = new Metadata { { GrpcFailureCodes.TrailerKey, errorCode } };
        var failure = new RpcException(new Status(StatusCode.PermissionDenied,
            "Transport detail"), trailers);
        string expected = Resources.ResourceManager.GetString(
            expectedResourceKey, CultureInfo.CurrentUICulture)!;

        // Act
        string message = ClientFailureMessages.GetGameAccessMessage(failure);

        // Assert
        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    public void GetRoomJoinMessage_UnrelatedPermissionDenied_UsesGenericMessage()
    {
        // Arrange
        var failure = new RpcException(new Status(StatusCode.PermissionDenied,
            "Room membership is required."));
        string expected = Resources.ResourceManager.GetString(
            "GameAccess_Err_PermissionDenied", CultureInfo.CurrentUICulture)!;

        // Act
        string message = ClientFailureMessages.GetRoomJoinMessage(failure);

        // Assert
        Assert.AreEqual(expected, message);
    }
}

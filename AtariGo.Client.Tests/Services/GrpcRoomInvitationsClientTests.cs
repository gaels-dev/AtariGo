
using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcRoomInvitationsClientTests
{
    [TestMethod]
    public async Task InvitationCalls_TwoSessions_PreserveIdentityDeadlineAndCancellation()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var recorder = new RecordingInterceptor();
        CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
        var sender = new GrpcRoomInvitationsClient("sender", invoker);
        var recipient = new GrpcRoomInvitationsClient("recipient", invoker);
        using var cancellation = new CancellationTokenSource();
        DateTime earliest = DateTime.UtcNow;

        // Act
        await sender.SendAsync(new SendRoomInvitationRequest
        {
            RoomId = 42, RecipientUserId = 2
        }, cancellation.Token);
        await recipient.ListAsync(0, cancellation.Token);
        await recipient.RespondAsync(new RespondRoomInvitationRequest
        {
            InvitationId = 7, Accept = true
        }, cancellation.Token);
        await sender.CancelAsync(7, cancellation.Token);

        // Assert
        CollectionAssert.AreEqual(new[] { "SendRoomInvitation", "ListRoomInvitations",
            "RespondRoomInvitation", "CancelRoomInvitation" },
            recorder.Calls.Select(call => call.MethodName).ToArray());
        Assert.AreEqual("Bearer sender", recorder.Calls[0].Headers.GetValue("authorization"));
        Assert.AreEqual("Bearer recipient", recorder.Calls[2].Headers.GetValue("authorization"));
        Assert.IsTrue(recorder.Calls.All(
            call => call.Options.CancellationToken == cancellation.Token));
        Assert.IsTrue(recorder.Calls.All(call => call.Options.Deadline > earliest));
    }
}

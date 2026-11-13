
using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcRoomsClientTests
{
    [TestMethod]
    public async Task UpdateRoomConfiguration_TwoSessions_PreservesIdentityDeadlineAndRequest()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var recorder = new RecordingInterceptor();
        CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
        var first = new GrpcRoomsClient("first", invoker);
        var second = new GrpcRoomsClient("second", invoker);
        using var cancellation = new CancellationTokenSource();
        DateTime earliest = DateTime.UtcNow;
        var request = new UpdateRoomConfigurationRequest
        {
            RoomId = 42,
            ExpectedVersion = 7,
            Configuration = new RoomConfiguration
            {
                CaptureGoal = 3, GameTimeSeconds = 600, TurnTimeSeconds = 30
            }
        };

        // Act
        await first.UpdateRoomConfigurationAsync(request, cancellation.Token);
        await second.UpdateRoomConfigurationAsync(request, cancellation.Token);

        // Assert
        Assert.HasCount(2, recorder.Calls);
        Assert.AreEqual("UpdateRoomConfiguration", recorder.Calls[0].MethodName);
        Assert.AreEqual("Bearer first",
            recorder.Calls[0].Options.Headers?.GetValue("authorization"));
        Assert.AreEqual("Bearer second",
            recorder.Calls[1].Options.Headers?.GetValue("authorization"));
        Assert.AreEqual(cancellation.Token, recorder.Calls[0].Options.CancellationToken);
        Assert.IsTrue(recorder.Calls[0].Options.Deadline > earliest);
        Assert.IsTrue(recorder.Calls[0].Options.Deadline
            <= DateTime.UtcNow.AddSeconds(GrpcCallPolicy.RequestTimeoutSeconds));
        Assert.AreSame(request, recorder.Calls[0].Request);
    }
}

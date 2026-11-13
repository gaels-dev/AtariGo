

using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcFriendClientTests
{
    [TestMethod]
    public async Task Requests_AllFriendOperations_ForwardDeadlineCancellationAndToken()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        using var cancellation = new CancellationTokenSource();
        var recorder = new RecordingInterceptor();
        var client = new GrpcFriendClient("first-token", connection.CallInvoker.Intercept(
            recorder));
        DateTime earliest = DateTime.UtcNow.AddSeconds(GrpcCallPolicy.RequestTimeoutSeconds);

        // Act
        await client.SearchUsersAsync("player", cancellation.Token);
        await client.SendFriendRequestAsync(2, cancellation.Token);
        await client.GetIncomingFriendRequestsAsync(cancellation.Token);
        await client.RespondToFriendRequestAsync(3, true, cancellation.Token);
        await client.ListFriendRequestsBySenderAsync("cursor", cancellation.Token);
        await client.CancelFriendRequestAsync(3, cancellation.Token);
        DateTime latest = DateTime.UtcNow.AddSeconds(GrpcCallPolicy.RequestTimeoutSeconds);

        // Assert
        CollectionAssert.AreEqual(new[]
        {
            "SearchUsers", "SendFriendRequest", "GetIncomingFriendRequests",
                "RespondToFriendRequest", "ListFriendRequestsBySender", "CancelFriendRequest"
        }, recorder.Calls.Select(call => call.MethodName).ToArray());
        foreach (RecordedGrpcCall call in recorder.Calls)
        {
            Assert.AreEqual(cancellation.Token, call.Options.CancellationToken);
            Assert.IsTrue(call.Options.Deadline >= earliest && call.Options.Deadline <= latest);
            Assert.AreEqual("Bearer first-token", call.Options.Headers?.GetValue("authorization"));
        }
    }

    [TestMethod]
    public async Task SearchUsers_TwoSessionsOnOneInvoker_KeepAuthorizationSeparate()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var recorder = new RecordingInterceptor();
        CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
        var first = new GrpcFriendClient("first-token", invoker);
        var second = new GrpcFriendClient("second-token", invoker);

        // Act
        await first.SearchUsersAsync("player");
        await second.SearchUsersAsync("player");

        // Assert
        Assert.AreEqual("Bearer first-token",
            recorder.Calls[0].Options.Headers?.GetValue("authorization"));
        Assert.AreEqual("Bearer second-token",
            recorder.Calls[1].Options.Headers?.GetValue("authorization"));
        Assert.AreNotSame(recorder.Calls[0].Options.Headers, recorder.Calls[1].Options.Headers);
    }
}

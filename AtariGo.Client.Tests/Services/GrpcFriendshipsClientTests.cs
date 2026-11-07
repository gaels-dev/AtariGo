using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcFriendshipsClientTests
{
    [TestMethod]
    public async Task RemoveFriendship_TwoSessions_PreservesIdentityDeadlineAndCancellation()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var recorder = new RecordingInterceptor();
        CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
        var first = new GrpcFriendshipsClient("first", invoker);
        var second = new GrpcFriendshipsClient("second", invoker);
        using var cancellation = new CancellationTokenSource();
        DateTime earliest = DateTime.UtcNow;

        // Act
        await first.RemoveFriendshipAsync(2, cancellation.Token);
        await second.RemoveFriendshipAsync(1, cancellation.Token);

        // Assert
        Assert.HasCount(2, recorder.Calls);
        Assert.AreEqual("RemoveFriendship", recorder.Calls[0].MethodName);
        Assert.AreEqual("Bearer first",
            recorder.Calls[0].Options.Headers?.GetValue("authorization"));
        Assert.AreEqual("Bearer second",
            recorder.Calls[1].Options.Headers?.GetValue("authorization"));
        Assert.AreEqual(cancellation.Token, recorder.Calls[0].Options.CancellationToken);
        Assert.IsTrue(recorder.Calls[0].Options.Deadline > earliest);
        Assert.AreEqual(2, ((RemoveFriendshipRequest)recorder.Calls[0].Request!).FriendUserId);
    }
}

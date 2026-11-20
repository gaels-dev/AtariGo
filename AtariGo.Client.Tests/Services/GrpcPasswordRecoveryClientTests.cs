using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcPasswordRecoveryClientTests
{
    [TestMethod]
    public async Task ResetAsync_LostResponse_UsesDeadlineClearsRequestAndNeverRetriesAsync()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new RecoveryResponseInterceptor();
        var client = new GrpcPasswordRecoveryClient(connection.CallInvoker.Intercept(interceptor));
        using var lifetime = new CancellationTokenSource();
        PasswordRecoveryInput input = PasswordRecoveryDialogViewModelTests.CreateInput();
        DateTime started = DateTime.UtcNow;
        // Act
        PasswordRecoveryResult rejected = await client.ResetAsync(input, lifetime.Token);
        interceptor.Response = Task.FromException<PasswordRecoveryResponse>(
            new RpcException(new Status(StatusCode.DeadlineExceeded, "Private detail")));
        PasswordRecoveryResult unknown = await client.ResetAsync(input, lifetime.Token);
        // Assert
        Assert.AreEqual(PasswordRecoveryResult.NotSaved, rejected);
        Assert.AreEqual(PasswordRecoveryResult.Unconfirmed, unknown);
        Assert.AreEqual(2, interceptor.Calls);
        Assert.AreEqual(lifetime.Token, interceptor.Options.CancellationToken);
        Assert.IsTrue(interceptor.Options.Deadline > started);
        Assert.IsNull(interceptor.Options.Headers);
        Assert.AreEqual(string.Empty, interceptor.Request!.Code);
        Assert.AreEqual(string.Empty, interceptor.Request.NewPassword);
        Assert.AreEqual(string.Empty, interceptor.Request.Confirmation);
    }
}

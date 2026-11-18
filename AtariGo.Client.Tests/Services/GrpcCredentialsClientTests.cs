using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcCredentialsClientTests
{
    [TestMethod]
    public async Task ChangePassword_TransportFailures_PreservesOptionsAndNeverRetriesAsync()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new CredentialsResponseInterceptor();
        var client = new GrpcCredentialsClient("owner",
            connection.CallInvoker.Intercept(interceptor));
        using var lifetime = new CancellationTokenSource();
        PasswordChangeInput input = ChangePasswordDialogViewModelTests.CreateInput();
        DateTime earliest = DateTime.UtcNow;
        // Act
        PasswordChangeResult changed = await client.ChangePasswordAsync(input, lifetime.Token);
        interceptor.Response = Task.FromException<ChangePasswordResponse>(
            new RpcException(new Status(StatusCode.DeadlineExceeded, "Private detail")));
        PasswordChangeResult unknown = await client.ChangePasswordAsync(input, lifetime.Token);
        interceptor.Response = Task.FromException<ChangePasswordResponse>(
            new RpcException(new Status(StatusCode.Unauthenticated, "Private detail")));
        PasswordChangeResult expired = await client.ChangePasswordAsync(input, lifetime.Token);
        // Assert
        Assert.AreEqual(PasswordChangeResult.Changed, changed);
        Assert.AreEqual(PasswordChangeResult.Unconfirmed, unknown);
        Assert.AreEqual(PasswordChangeResult.SessionExpired, expired);
        Assert.AreEqual(3, interceptor.Calls);
        Assert.AreEqual("Bearer owner", interceptor.Options.Headers?.GetValue("authorization"));
        Assert.IsTrue(interceptor.Options.Deadline > earliest);
        Assert.AreEqual(lifetime.Token, interceptor.Options.CancellationToken);
    }
}

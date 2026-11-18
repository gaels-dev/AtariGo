using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcAuthenticationClientTests
{
    [TestMethod]
    public async Task VerifyLoginCodeAsync_LostResponse_ClearsSecretsAndDoesNotRetryAsync()
    {
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new LoginResponseInterceptor
        {
            Response = Task.FromException<LoginResponse>(new RpcException(
                new Status(StatusCode.DeadlineExceeded, "Simulated lost response")))
        };
        var client = new GrpcAuthenticationClient(connection.CallInvoker.Intercept(interceptor));
        using var cancellation = new CancellationTokenSource();
        LoginAttemptResult result = await client.VerifyLoginCodeAsync("pending-token", "01234567",
            cancellation.Token);
        Assert.AreEqual(LoginAttemptStatus.Unconfirmed, result.Status);
        Assert.AreEqual(1, interceptor.Calls);
        Assert.AreEqual(cancellation.Token, interceptor.Options.CancellationToken);
        Assert.IsNotNull(interceptor.Options.Deadline);
        Assert.IsInstanceOfType<VerifyLoginCodeRequest>(interceptor.Request);
        VerifyLoginCodeRequest request = (VerifyLoginCodeRequest)interceptor.Request;
        Assert.AreEqual(string.Empty, request.Code);
        Assert.AreEqual(string.Empty, request.ChallengeToken);
    }

    [TestMethod]
    public async Task LoginAsync_ChallengeResponse_PreservesPendingDataAndClearsPasswordAsync()
    {
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new LoginResponseInterceptor
        {
            Response = Task.FromResult(new LoginResponse
            {
                Result = LoginResult.ChallengeRequired,
                ChallengeToken = "pending-token",
                MaskedEmail = "p***@example.test"
            })
        };
        var client = new GrpcAuthenticationClient(connection.CallInvoker.Intercept(interceptor));
        LoginAttemptResult result = await client.LoginAsync("Player", "Secret8");
        Assert.AreEqual(LoginAttemptStatus.ChallengeRequired, result.Status);
        Assert.AreEqual("pending-token", result.ChallengeToken);
        Assert.AreEqual("p***@example.test", result.MaskedEmail);
        Assert.IsInstanceOfType<LoginRequest>(interceptor.Request);
        Assert.AreEqual(string.Empty, ((LoginRequest)interceptor.Request).Password);
        Assert.AreEqual(string.Empty, result.SessionToken);
    }
}

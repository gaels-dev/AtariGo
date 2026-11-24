using System.Diagnostics;
using System.Text.Json;

using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
[OSCondition(OperatingSystems.Windows)]
public sealed class ClientIdentityLoggingTests
{
    private readonly TestContext _testContext;

    public ClientIdentityLoggingTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    [DataRow(StatusCode.Cancelled, true, "Cancelled")]
    [DataRow(StatusCode.Unavailable, true, "Unconfirmed")]
    [DataRow(StatusCode.DeadlineExceeded, false, "Unconfirmed")]
    public async Task ChangePassword_TransportFailure_RecordsCorrectOutcomeOnceAsync(
        StatusCode status, bool cancelled, string expected)
    {
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        ClientFileTraceListener listener = Trace.Listeners.OfType<ClientFileTraceListener>()
            .Single();
        listener.Filter = new EventTypeFilter(expected == "Cancelled"
            ? SourceLevels.Information : SourceLevels.Warning);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new CredentialsResponseInterceptor
        {
            Response = Task.FromException<ChangePasswordResponse>(new RpcException(
                new Status(status, "private")))
        };
        var client = new GrpcCredentialsClient("private",
            connection.CallInvoker.Intercept(interceptor));
        using var cancellation = new CancellationTokenSource();
        if (cancelled)
        {
            await cancellation.CancelAsync();
        }
        var input = new PasswordChangeInput
        {
            CurrentPassword = "private",
            NewPassword = "NewSecret9",
            Confirmation = "NewSecret9"
        };
        await client.ChangePasswordAsync(input, cancellation.Token);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement[] entries = directory.ReadEvents().Where(entry =>
            entry.GetProperty("Operation").GetString() == "ChangePassword").ToArray();

        Assert.HasCount(1, entries);
        Assert.AreEqual(expected, entries[0].GetProperty("Outcome").GetString());
        Assert.AreEqual((int)status, entries[0].GetProperty("Error")
            .GetProperty("NativeCode").GetInt32());
        Assert.DoesNotContain("private", entries[0].GetRawText(), StringComparison.Ordinal);
        Assert.AreEqual(1, interceptor.Calls);
    }

    [TestMethod]
    public async Task Login_PrivateResponse_PersistsOnlyAllowedContextAsync()
    {
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new LoginResponseInterceptor
        {
            Response = Task.FromResult(new LoginResponse
            {
                Result = LoginResult.Success,
                UserId = 21,
                UserName = "private",
                SessionToken = "private",
                ChallengeToken = "private",
                MaskedEmail = "private"
            })
        };
        var client = new GrpcAuthenticationClient(connection.CallInvoker.Intercept(interceptor));
        await client.LoginAsync("private", "private", _testContext.CancellationToken);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            item.GetProperty("Operation").GetString() == "Login");

        Assert.AreEqual(21, entry.GetProperty("UserId").GetInt32());
        Assert.AreEqual("Succeeded", entry.GetProperty("Outcome").GetString());
        Assert.DoesNotContain("private", entry.GetRawText(), StringComparison.Ordinal);
    }
}

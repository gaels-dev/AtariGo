using System.IO;
using System.Reflection;
using System.Text.Json;

using Grpc.Core;

using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
[OSCondition(OperatingSystems.Windows)]
public sealed class GuestSessionLoggingTests
{
    private readonly TestContext _testContext;

    public GuestSessionLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    [DataRow(StatusCode.DeadlineExceeded)]
    [DataRow(StatusCode.Unavailable)]
    public async Task EnterCommand_RpcFailure_RecordsSafeCauseAndKeepsErrorVisibleAsync(
        StatusCode status)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        const string privateValue = "private-guest-credential@example.invalid";
        var failure = new RpcException(new Status(status, privateValue));
        var client = new StubGuestIdentityClient
        {
            Response = Task.FromException<string>(failure)
        };
        using var model = new GuestRegistrationDialogViewModel(navigation, client);
        model.GuestName = "PrivatePlayer";
        // Act
        await CommandExecution.ExecuteAsync(model.EnterCommand, null);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        JsonElement entry = directory.ReadEvents().Single(record =>
            record.GetProperty("Operation").GetString() == "OpenGuestSession");
        Assert.AreEqual("Warning", entry.GetProperty("Severity").GetString());
        Assert.AreEqual("Unconfirmed", entry.GetProperty("Outcome").GetString());
        Assert.AreEqual("RpcException", entry.GetProperty("Error")
            .GetProperty("TypeName").GetString());
        Assert.AreEqual((int)status, entry.GetProperty("Error")
            .GetProperty("NativeCode").GetInt32());
        Assert.DoesNotContain(privateValue, entry.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(model.GuestName, entry.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(privateValue, model.ErrorMessage, StringComparison.Ordinal);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.ErrorMessage));
        Assert.IsTrue(model.CanEdit);
        Assert.IsEmpty(((RecordingNavigationProxy)navigation).Calls);
        await using var stream = new FileStream(
            Directory.GetFiles(directory.DirectoryPath, "*.log").Single(),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string text = await reader.ReadToEndAsync(_testContext.CancellationToken);
        Assert.Contains("RPC status: " + status + ".", text, StringComparison.Ordinal);
        Assert.DoesNotContain(privateValue, text, StringComparison.Ordinal);
    }
}

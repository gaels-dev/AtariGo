using System.Text.Json;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
[OSCondition(OperatingSystems.Windows)]
public sealed class GameLoggingTests
{
    private readonly TestContext _testContext;

    public GameLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Recovery_ReplayedAction_LogsOneConfirmationWithOriginalIdentityAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        var first = new StubGameSessionStream { FailSend = true };
        var second = new StubGameSessionStream();
        await using var context = new GameplaySessionTestContext(first, second);
        await context.StartAsync(3);
        // Act
        await context.Session.SubmitMoveAsync(0, 1, _testContext.CancellationToken);
        string actionId = first.Sent.Single().SubmitMove.ActionId;
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Client.ConnectCount == 2);
        second.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(4) });
        await GameplaySessionTestContext.WaitUntilAsync(() => second.Sent.Count == 1);
        var confirmation = new GameSessionServerEvent
        {
            ActionResult = new GameActionResponse
            {
                ActionId = actionId,
                IsAccepted = true,
                Version = 4,
                Rejection = "private-response"
            }
        };
        second.Publish(confirmation);
        second.Publish(confirmation);
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Updates.Count(update =>
            update.ServerEvent?.ActionResult is not null) == 2);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        List<JsonElement> entries = directory.ReadEvents();
        // Assert
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "ActionCommitted"));
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "Connected"));
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "Restored"));
        JsonElement committed = entries.Single(entry => ReadOperation(entry) == "ActionCommitted");
        Assert.AreEqual(Guid.ParseExact(actionId, "N"),
            committed.GetProperty("ActionId").GetGuid());
        Assert.AreEqual(1L, committed.GetProperty("GameId").GetInt64());
        Assert.AreEqual(actionId, second.Sent.Single().SubmitMove.ActionId);
        Assert.DoesNotContain(entry => entry.GetRawText().Contains("private",
            StringComparison.Ordinal),
            entries);
    }

    [TestMethod]
    public async Task Chat_LostConfirmation_RecordsUncertaintyWithoutMessageTextAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        var first = new StubGameSessionStream { FailSend = true };
        await using var context = new GameplaySessionTestContext(first,
            new StubGameSessionStream());
        await context.StartAsync();
        // Act
        bool sent = await context.Session.SendChatAsync("private-chat",
            _testContext.CancellationToken);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            ReadOperation(item) == "ChatSubmission");
        // Assert
        Assert.IsFalse(sent);
        Assert.AreEqual("Unconfirmed", entry.GetProperty("Outcome").GetString());
        Assert.AreEqual("Warning", entry.GetProperty("Severity").GetString());
        Assert.IsFalse(entry.GetRawText().Contains("private", StringComparison.Ordinal));
    }

    private static string ReadOperation(JsonElement entry)
    {
        return entry.GetProperty("Operation").GetString()!;
    }
}

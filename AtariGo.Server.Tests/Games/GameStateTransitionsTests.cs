using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameStateTransitionsTests
{
    [TestMethod]
    public void Start_WaitingGame_SetsPlayingAndFullTurnDeadline()
    {
        // Arrange
        DateTime now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        GameRuntimeState waiting = new GameRuntimeState
        {
            DeadlineUtc = now.AddSeconds(120)
        };
        // Act
        GameRuntimeState playing = GameStateTransitions.Start(waiting, now, 30);
        // Assert
        Assert.AreEqual(GameRuntimeState.Playing, playing.Status);
        Assert.AreEqual(now.AddSeconds(30), playing.DeadlineUtc);
        Assert.AreSame(waiting.Board, playing.Board);
        Assert.AreEqual(GameRuntimeState.Waiting, waiting.Status);
    }

    [TestMethod]
    [DataRow(GameRuntimeState.Playing)]
    [DataRow(GameRuntimeState.Completed)]
    [DataRow(GameRuntimeState.Cancelled)]
    public void Start_GameIsNotWaiting_PreservesStateAndDeadline(string status)
    {
        // Arrange
        DateTime now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        GameRuntimeState runtime = new GameRuntimeState
        {
            Status = status,
            DeadlineUtc = now.AddSeconds(10)
        };
        // Act
        GameRuntimeState result = GameStateTransitions.Start(runtime, now, 30);
        // Assert
        Assert.AreSame(runtime, result);
        Assert.AreEqual(now.AddSeconds(10), result.DeadlineUtc);
    }
}

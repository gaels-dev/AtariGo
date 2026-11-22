using AtariGo.GameLogic.Models;
using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameConnectionRulesTests
{
    private static readonly DateTime start = new DateTime(2026, 1, 1, 12, 0, 0,
        DateTimeKind.Utc);

    [TestMethod]
    public void Reconnect_BeforeGraceExpires_ResumesRemainingTurnWithFreshGrace()
    {
        // Arrange
        GameRuntimeState playing = CreatePlaying();
        DateTime disconnectedAt = start.AddSeconds(18);
        GameRuntimeState paused = GameConnectionRules.Disconnect(
            playing, StoneColor.Black, disconnectedAt);
        // Act
        DateTime returnedAt = disconnectedAt.AddSeconds(5);
        GameRuntimeState restored = GameConnectionRules.Reconnect(
            paused, StoneColor.Black, returnedAt);
        // Assert
        Assert.AreEqual(12000, paused.PausedTurnRemainingMilliseconds);
        Assert.AreEqual(disconnectedAt.AddSeconds(30), paused.BlackReconnectDeadlineUtc);
        Assert.IsNull(restored.PausedTurnRemainingMilliseconds);
        Assert.AreEqual(returnedAt.AddSeconds(12), restored.DeadlineUtc);
        Assert.AreEqual(30000, restored.BlackGraceRemainingMilliseconds);
    }

    [TestMethod]
    public void Resolve_OnePlayerGraceExpires_AwardsConnectedOpponent()
    {
        // Arrange
        GameRuntimeState paused = GameConnectionRules.Disconnect(
            CreatePlaying(), StoneColor.Black, start);
        // Act
        GameRuntimeState ended = GameConnectionRules.Resolve(paused,
            start.AddSeconds(30), new GameConnectionPresence(false, true));
        // Assert
        Assert.AreEqual(GameRuntimeState.Completed, ended.Status);
        Assert.AreEqual(StoneColor.White, ended.Board.Winner);
        Assert.AreEqual("DisconnectionTimeout", ended.EndReason);
    }

    [TestMethod]
    public void Resolve_BothGracePeriodsExpire_CancelsWithoutWinner()
    {
        // Arrange
        GameRuntimeState blackLeft = GameConnectionRules.Disconnect(
            CreatePlaying(), StoneColor.Black, start);
        GameRuntimeState bothLeft = GameConnectionRules.Disconnect(
            blackLeft, StoneColor.White, start.AddSeconds(5));
        // Act
        GameRuntimeState blackExpired = GameConnectionRules.Resolve(bothLeft,
            start.AddSeconds(30), new GameConnectionPresence(false, false));
        GameRuntimeState bothExpired = GameConnectionRules.Resolve(blackExpired,
            start.AddSeconds(35), new GameConnectionPresence(false, false));
        // Assert
        Assert.AreEqual(GameRuntimeState.Playing, blackExpired.Status);
        Assert.AreEqual(0, blackExpired.BlackGraceRemainingMilliseconds);
        Assert.AreEqual(GameRuntimeState.Cancelled, bothExpired.Status);
        Assert.AreEqual("BothDisconnected", bothExpired.EndReason);
        Assert.AreEqual(StoneColor.Empty, bothExpired.Board.Winner);
    }

    [TestMethod]
    public void Disconnect_AfterReconnect_StartsFreshGracePeriod()
    {
        // Arrange
        GameRuntimeState first = GameConnectionRules.Disconnect(
            CreatePlaying(), StoneColor.Black, start);
        GameRuntimeState returned = GameConnectionRules.Reconnect(
            first, StoneColor.Black, start.AddSeconds(20));
        // Act
        GameRuntimeState second = GameConnectionRules.Disconnect(
            returned, StoneColor.Black, start.AddSeconds(22));
        // Assert
        Assert.AreEqual(30000, returned.BlackGraceRemainingMilliseconds);
        Assert.AreEqual(start.AddSeconds(52), second.BlackReconnectDeadlineUtc);
    }

    private static GameRuntimeState CreatePlaying()
    {
        return new GameRuntimeState
        {
            Status = GameRuntimeState.Playing,
            DeadlineUtc = start.AddSeconds(30)
        };
    }
}

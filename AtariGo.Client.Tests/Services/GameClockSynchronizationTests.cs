
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GameClockSynchronizationTests
{
    [TestMethod]
    public void ObserveServerTime_FirstSnapshot_EstimatesTransitFromHandshake()
    {
        // Arrange
        GameClockTimeProvider time = new GameClockTimeProvider();
        GameClockSynchronization clock = new GameClockSynchronization(time);
        clock.BeginConnection();
        time.Advance(TimeSpan.FromMilliseconds(100));
        // Act
        clock.ObserveServerTime(100000);
        double remaining = clock.GetRemainingMilliseconds(130000);
        // Assert
        Assert.IsTrue(clock.IsSynchronized);
        Assert.AreEqual(29950d, remaining);
    }

    [TestMethod]
    public void ObserveServerTime_DelayedSnapshot_DoesNotRestoreElapsedSeconds()
    {
        // Arrange
        GameClockTimeProvider time = new GameClockTimeProvider();
        GameClockSynchronization clock = new GameClockSynchronization(time);
        clock.BeginConnection();
        clock.ObserveServerTime(100000);
        time.Advance(TimeSpan.FromSeconds(10));
        // Act
        clock.ObserveServerTime(101000);
        double remaining = clock.GetRemainingMilliseconds(130000);
        // Assert
        Assert.AreEqual(20000d, remaining);
    }

    [TestMethod]
    public void GetRemainingMilliseconds_NewTurnDeliveredLate_UsesDeadlineInsteadOfFullInterval()
    {
        // Arrange
        GameClockTimeProvider time = new GameClockTimeProvider();
        GameClockSynchronization clock = new GameClockSynchronization(time);
        clock.BeginConnection();
        clock.ObserveServerTime(100000);
        time.Advance(TimeSpan.FromSeconds(40));
        // Act
        clock.ObserveServerTime(130000);
        double remaining = clock.GetRemainingMilliseconds(160000);
        // Assert
        Assert.AreEqual(20000d, remaining);
    }

    [TestMethod]
    public void GetRemainingMilliseconds_SystemClockChanges_UsesOnlyMonotonicElapsedTime()
    {
        // Arrange
        GameClockTimeProvider time = new GameClockTimeProvider();
        GameClockSynchronization clock = new GameClockSynchronization(time);
        clock.BeginConnection();
        clock.ObserveServerTime(100000);
        time.Advance(TimeSpan.FromSeconds(5));
        // Act
        time.UtcNow = time.UtcNow.AddDays(-2);
        double remaining = clock.GetRemainingMilliseconds(130000);
        // Assert
        Assert.AreEqual(25000d, remaining);
    }

    [TestMethod]
    public void GetRemainingMilliseconds_ExpiredDeadline_ReturnsZero()
    {
        // Arrange
        GameClockTimeProvider time = new GameClockTimeProvider();
        GameClockSynchronization clock = new GameClockSynchronization(time);
        clock.BeginConnection();
        clock.ObserveServerTime(100000);
        // Act
        time.Advance(TimeSpan.FromSeconds(31));
        double remaining = clock.GetRemainingMilliseconds(130000);
        // Assert
        Assert.AreEqual(0d, remaining);
    }

    [TestMethod]
    public void BeginConnection_Reconnects_RecalibratesFromNewHandshake()
    {
        // Arrange
        GameClockTimeProvider time = new GameClockTimeProvider();
        GameClockSynchronization clock = new GameClockSynchronization(time);
        clock.BeginConnection();
        clock.ObserveServerTime(100000);
        time.Advance(TimeSpan.FromSeconds(10));
        // Act
        clock.BeginConnection();
        bool synchronizedBeforeSnapshot = clock.IsSynchronized;
        time.Advance(TimeSpan.FromMilliseconds(200));
        clock.ObserveServerTime(200000);
        double remaining = clock.GetRemainingMilliseconds(230000);
        // Assert
        Assert.IsFalse(synchronizedBeforeSnapshot);
        Assert.IsTrue(clock.IsSynchronized);
        Assert.AreEqual(29900d, remaining);
    }

    [TestMethod]
    public void ObserveServerTime_MissingTimestamp_DoesNotStartSynchronization()
    {
        // Arrange
        GameClockSynchronization clock = new GameClockSynchronization(new GameClockTimeProvider());
        clock.BeginConnection();
        // Act
        clock.ObserveServerTime(0);
        // Assert
        Assert.IsFalse(clock.IsSynchronized);
    }
}

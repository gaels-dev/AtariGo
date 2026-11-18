using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Tests.RoomInvitations;

[TestClass]
[TestCategory("Unit")]
public sealed class RoomInvitationEmailLimiterTests
{
    [TestMethod]
    public void TryReserve_SameFriendAndRoom_EnforcesCooldownAcrossInvitationIds()
    {
        // Arrange
        var time = new ManualTimeProvider();
        var limiter = new RoomInvitationEmailLimiter(time);
        RoomInvitationEntry invitation = Create(1, 1);

        // Act
        int first = limiter.TryReserve(invitation);
        limiter.Complete(invitation);
        time.Advance(TimeSpan.FromSeconds(59));
        int early = limiter.TryReserve(invitation with { InvitationId = 2 });
        time.Advance(TimeSpan.FromSeconds(1));
        int next = limiter.TryReserve(invitation);

        // Assert
        Assert.AreEqual(0, first);
        Assert.AreEqual(1, early);
        Assert.AreEqual(0, next);
    }

    [TestMethod]
    public void TryReserve_RollingHour_LimitsSenderAcrossRoomsAndReleasesOldestSlot()
    {
        // Arrange
        var time = new ManualTimeProvider();
        var limiter = new RoomInvitationEmailLimiter(time);
        for (int room = 1; room <= 10; room++)
        {
            RoomInvitationEntry invitation = Create(room, 1);
            Assert.AreEqual(0, limiter.TryReserve(invitation));
            limiter.Complete(invitation);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        // Act
        int limited = limiter.TryReserve(Create(11, 1));
        int otherSender = limiter.TryReserve(Create(12, 3));
        time.Advance(TimeSpan.FromMinutes(50));
        int released = limiter.TryReserve(Create(11, 1));
        int stillLimited = limiter.TryReserve(Create(13, 1));

        // Assert
        Assert.AreEqual(3000, limited);
        Assert.AreEqual(0, otherSender);
        Assert.AreEqual(0, released);
        Assert.AreEqual(60, stillLimited);
    }

    [TestMethod]
    public async Task TryReserve_ConcurrentCalls_AdmitsOnlyOnePendingDelivery()
    {
        // Arrange
        var time = new ManualTimeProvider();
        var limiter = new RoomInvitationEmailLimiter(time);
        RoomInvitationEntry invitation = Create(1, 1);

        // Act
        int[] results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => limiter.TryReserve(invitation))));
        time.Advance(TimeSpan.FromMinutes(2));
        int whileSending = limiter.TryReserve(invitation);
        limiter.Complete(invitation);
        int afterCompletion = limiter.TryReserve(invitation);

        // Assert
        Assert.AreEqual(1, results.Count(result => result == 0));
        Assert.AreEqual(60, whileSending);
        Assert.AreEqual(0, afterCompletion);
    }

    private static RoomInvitationEntry Create(int roomId, int senderId)
    {
        return new RoomInvitationEntry(roomId, roomId, DateTimeOffset.MaxValue)
        {
            RoomCode = "ABCDEF1234", Sender = new LobbyParticipant(senderId, "Host"),
            Recipient = new LobbyParticipant(2, "Friend")
        };
    }
}

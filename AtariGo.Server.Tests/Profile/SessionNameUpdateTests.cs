using AtariGo.Contracts;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Tests.Profile;

[TestClass]
[TestCategory("Unit")]
public sealed class SessionNameUpdateTests
{
    [TestMethod]
    public void UpdateUserName_ConnectedFriend_NotifiesAndUpdatesSnapshot()
    {
        // Arrange
        SessionConnectionRegistry registry = new SessionConnectionRegistry();
        using SessionConnectionLease first = registry.Register(
            new SessionConnectionInfo(10, 1, "First")
            {
                Friends = new[] { new FriendPresenceEntry(2, "Second", false) }
            });
        using SessionConnectionLease second = registry.Register(
            new SessionConnectionInfo(20, 2, "Second")
            {
                Friends = new[] { new FriendPresenceEntry(1, "First", true) }
            });
        while (second.OutboundEvents.Reader.TryRead(out _))
        {
        }

        // Act
        registry.UpdateUserName(1, "Renamed");
        bool received = second.OutboundEvents.Reader.TryRead(out SessionServerEvent? message);

        // Assert
        Assert.IsTrue(received);
        Assert.IsNotNull(message);
        Assert.AreEqual("Renamed", first.ConnectionInfo.UserName);
        Assert.AreEqual("Renamed", second.ConnectionInfo.Friends[0].UserName);
        Assert.AreEqual("Renamed", message.FriendPresenceChanged.Friend.UserName);
        registry.Remove(first);
        registry.Remove(second);
    }
}

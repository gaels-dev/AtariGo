using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Tests.Sessions;

[TestClass]
[TestCategory("Unit")]
public sealed class SessionConnectionRegistryTests
{
    [TestMethod]
    public void Register_DifferentSessionForConnectedUser_PreservesFirstConnection()
    {
        // Arrange
        using var registry = new SessionConnectionRegistry();
        using SessionConnectionLease first = registry.Register(
            new SessionConnectionInfo(10, 1, "Player"))!;

        // Act
        using SessionConnectionLease? second = registry.Register(
            new SessionConnectionInfo(20, 1, "Player"));

        // Assert
        Assert.IsNull(second);
        Assert.IsFalse(first.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(registry.IsSessionConnected(10));
        Assert.IsFalse(registry.IsSessionConnected(20));
        registry.Remove(first);
    }

    [TestMethod]
    public void Remove_DisconnectedClient_AllowsAnotherSessionImmediately()
    {
        // Arrange
        using var registry = new SessionConnectionRegistry();
        using SessionConnectionLease first = registry.Register(
            new SessionConnectionInfo(10, 1, "Player"))!;

        // Act
        registry.Remove(first);
        using SessionConnectionLease? second = registry.Register(
            new SessionConnectionInfo(20, 1, "Player"));

        // Assert
        Assert.IsNotNull(second);
        Assert.IsTrue(registry.IsSessionConnected(20));
        Assert.IsFalse(registry.Remove(first));
        Assert.IsTrue(registry.IsSessionConnected(20));
        registry.Remove(second);
    }

    [TestMethod]
    public void Register_SameSessionReconnects_OldCleanupPreservesCurrentConnection()
    {
        // Arrange
        using var registry = new SessionConnectionRegistry();
        using SessionConnectionLease first = registry.Register(
            new SessionConnectionInfo(10, 1, "Player"))!;

        // Act
        using SessionConnectionLease? current = registry.Register(
            new SessionConnectionInfo(10, 1, "Player"));
        bool removed = registry.Remove(first);

        // Assert
        Assert.IsNotNull(current);
        Assert.IsTrue(first.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(removed);
        Assert.IsTrue(registry.IsSessionConnected(10));
        Assert.IsFalse(current.CancellationToken.IsCancellationRequested);
        registry.Remove(current);
    }
}

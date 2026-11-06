using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Server.Application.Matchmaking;

namespace AtariGo.Server.Tests.Matchmaking;

[TestClass]
[TestCategory("Unit")]
public sealed class GuestIdentityRegistryTests
{
    [TestMethod]
    public void Create_FindAndRevoke_UsesTemporaryCredential()
    {
        // Arrange
        IGuestIdentityRegistry registry = new GuestIdentityRegistry(TimeProvider.System);

        // Act
        IssuedGuestIdentity issuedIdentity = registry.Create("Guest-Alex");
        GuestIdentity? foundIdentity = registry.Find(issuedIdentity.Credential);
        bool wasRevoked = registry.Revoke(issuedIdentity.Credential);

        // Assert
        Assert.IsNotNull(foundIdentity);
        Assert.AreEqual(issuedIdentity.Identity.GuestId, foundIdentity.GuestId);
        Assert.AreEqual("Guest-Alex", foundIdentity.DisplayName);
        Assert.IsTrue(wasRevoked);
        Assert.IsNull(registry.Find(issuedIdentity.Credential));
    }
}

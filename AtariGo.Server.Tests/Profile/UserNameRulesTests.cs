using AtariGo.Server.Application.Authentication;

namespace AtariGo.Server.Tests.Profile;

[TestClass]
[TestCategory("Unit")]
public sealed class UserNameRulesTests
{
    [TestMethod]
    [DataRow("PlayerOne", true)]
    [DataRow("  PlayerOne  ", true)]
    [DataRow("", false)]
    [DataRow("Guest", false)]
    [DataRow("guest-Player", false)]
    [DataRow("123456789012345", true)]
    [DataRow("1234567890123456", false)]
    public void IsValid_AppliesRegistrationAndProfileNameRules(
        string userName, bool expected)
    {
        // Act
        bool valid = UserNameRules.IsValid(userName);

        // Assert
        Assert.AreEqual(expected, valid);
    }
}

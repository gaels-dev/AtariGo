using AtariGo.Server.Security;

namespace AtariGo.Server.Tests.Security;

[TestClass]
[TestCategory("Unit")]
public sealed class AuthenticationCodeProtectorTests
{
    private const string LegacyHash =
        "07DD21B1608634684E89426287BCC831AF61CDE9A01EB47F1C15B7E960124612";

    [TestMethod]
    public void Protect_RecoveryScope_ProducesLegacyHashAndAcceptsLeadingZero()
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var scope = new AuthenticationCodeScope(1, "PasswordReset");
        string hash = protector.Protect(scope, "01234567");
        bool matches = protector.Matches(scope, "01234567", LegacyHash);
        Assert.AreEqual(LegacyHash, hash);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_OtherUserPurposeOrBinding_RejectsCodeOutsideItsScope()
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var scope = new AuthenticationCodeScope(1, "TwoFactor", "first-attempt");
        string hash = protector.Protect(scope, "01234567");
        AuthenticationCodeScope[] otherScopes =
        {
            new AuthenticationCodeScope(2, "TwoFactor", "first-attempt"),
            new AuthenticationCodeScope(1, "PasswordReset", "first-attempt"),
            new AuthenticationCodeScope(1, "TwoFactor", "second-attempt"),
            new AuthenticationCodeScope(1, "TwoFactor")
        };
        bool matches = protector.Matches(scope, "01234567", hash);
        bool[] otherMatches = otherScopes.Select(other => protector.Matches(other, "01234567",
            hash)).ToArray();
        Assert.IsTrue(matches);
        Assert.IsTrue(otherMatches.All(match => !match));
        Assert.IsFalse(protector.Matches(new AuthenticationCodeScope(1, "TwoFactor"),
            "01234567", LegacyHash));
    }

    [TestMethod]
    public void Protect_BindingContainsSeparators_DoesNotCollideWithOtherScopes()
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var first = new AuthenticationCodeScope(1, "TwoFactor", "x:y");
        var second = new AuthenticationCodeScope(1, "TwoFactor:x", "y");
        string firstHash = protector.Protect(first, "01234567");
        string secondHash = protector.Protect(second, "01234567");
        Assert.AreNotEqual(firstHash, secondHash);
        Assert.IsFalse(protector.Matches(second, "01234567", firstHash));
    }

    [TestMethod]
    [DataRow("1234567")]
    [DataRow("012345678")]
    [DataRow("１２３４５６７８")]
    [DataRow("1234abcd")]
    [DataRow("00000000")]
    public void Matches_InvalidOrIncorrectCode_RejectsCode(string code)
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var scope = new AuthenticationCodeScope(1, "PasswordReset");
        bool matches = protector.Matches(scope, code, LegacyHash);
        Assert.IsFalse(matches);
    }

    [TestMethod]
    [DataRow("", false)]
    [DataRow("1234567890123456789012345678901", false)]
    [DataRow("12345678901234567890123456789012", true)]
    [DataRow("ññññññññññññññññ", true)]
    public void IsConfigured_KeyLength_UsesExistingUtf8ByteRequirement(string key, bool expected)
    {
        var protector = new AuthenticationCodeProtector(key);
        bool configured = protector.IsConfigured;
        Assert.AreEqual(expected, configured);
    }
}

using AtariGo.Server.Security;

namespace AtariGo.Server.Tests.Security;

[TestClass]
[TestCategory("Unit")]
public sealed class AuthenticationCodeProtectorTests
{
    private const string LegacyHash =
        "9C21EEE35502E4CBF0C2DFBA1870C282A043C1B23AE366B2A129D415E59C1782";

    [TestMethod]
    public void CreateCode_NewCodes_ProducesSixAsciiDigitsAndPreservesLeadingZeros()
    {
        for (int index = 0; index < 64; index++)
        {
            string code = AuthenticationCodeProtector.CreateCode();

            Assert.AreEqual(6, code.Length);
            foreach (char digit in code)
            {
                Assert.IsTrue(char.IsAsciiDigit(digit));
            }
        }
        AuthenticationCodeProtector protector = new AuthenticationCodeProtector(
            new string('K', 64));
        AuthenticationCodeScope scope = new AuthenticationCodeScope(1, "TwoFactor", "attempt");
        string hash = protector.Protect(scope, "000001");
        Assert.IsTrue(protector.Matches(scope, "000001", hash));
        Assert.IsFalse(protector.Matches(scope, "00000001", hash));
    }

    [TestMethod]
    public void Protect_RecoveryScope_ProducesLegacyHashAndAcceptsLeadingZero()
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var scope = new AuthenticationCodeScope(1, "PasswordReset");
        string hash = protector.Protect(scope, "012345");
        bool matches = protector.Matches(scope, "012345", LegacyHash);
        Assert.AreEqual(LegacyHash, hash);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_OtherUserPurposeOrBinding_RejectsCodeOutsideItsScope()
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var scope = new AuthenticationCodeScope(1, "TwoFactor", "first-attempt");
        string hash = protector.Protect(scope, "012345");
        AuthenticationCodeScope[] otherScopes =
        {
            new AuthenticationCodeScope(2, "TwoFactor", "first-attempt"),
            new AuthenticationCodeScope(1, "PasswordReset", "first-attempt"),
            new AuthenticationCodeScope(1, "TwoFactor", "second-attempt"),
            new AuthenticationCodeScope(1, "TwoFactor")
        };
        bool matches = protector.Matches(scope, "012345", hash);
        Assert.IsTrue(matches);
        foreach (AuthenticationCodeScope other in otherScopes)
        {
            Assert.IsFalse(protector.Matches(other, "012345", hash));
        }
        Assert.IsFalse(protector.Matches(new AuthenticationCodeScope(1, "TwoFactor"),
            "012345", LegacyHash));
    }

    [TestMethod]
    public void Protect_BindingContainsSeparators_DoesNotCollideWithOtherScopes()
    {
        var protector = new AuthenticationCodeProtector(new string('K', 64));
        var first = new AuthenticationCodeScope(1, "TwoFactor", "x:y");
        var second = new AuthenticationCodeScope(1, "TwoFactor:x", "y");
        string firstHash = protector.Protect(first, "012345");
        string secondHash = protector.Protect(second, "012345");
        Assert.AreNotEqual(firstHash, secondHash);
        Assert.IsFalse(protector.Matches(second, "012345", firstHash));
    }

    [TestMethod]
    [DataRow("12345")]
    [DataRow("0123456")]
    [DataRow("01234567")]
    [DataRow("１２３４５６")]
    [DataRow("123abc")]
    [DataRow("000000")]
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

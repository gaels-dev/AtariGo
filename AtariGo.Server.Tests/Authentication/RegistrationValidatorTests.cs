using AtariGo.Server.Application.Authentication;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Unit")]
public sealed class RegistrationValidatorTests
{
    [TestMethod]
    [DataRow("player@example.com", true)]
    [DataRow(" player+go@sub.example.com ", true)]
    [DataRow("player@dominio.mx", true)]
    [DataRow("player@bücher.de", true)]
    [DataRow("", false)]
    [DataRow("plain text", false)]
    [DataRow("player@local", false)]
    [DataRow("Player <player@example.com>", false)]
    [DataRow("player(comment)@example.com", false)]
    [DataRow("player@example.com;other@example.com", false)]
    [DataRow("player@example.com,other@example.com", false)]
    [DataRow("player name@example.com", false)]
    [DataRow("player@-example.com", false)]
    [DataRow("player@example-.com", false)]
    [DataRow("player@example..com", false)]
    [DataRow("player@127.0.0.1", false)]
    [DataRow("player@[127.0.0.1]", false)]
    [DataRow("player@example.com.", false)]
    [DataRow("player@exam_ple.com", false)]
    [DataRow(".player@example.com", false)]
    [DataRow("player.@example.com", false)]
    [DataRow("pla..yer@example.com", false)]
    [DataRow("pla\u200byer@example.com", false)]
    [DataRow("player@example.com\0", false)]
    [DataRow("player@example.com\r\nBcc: other@example.com", false)]
    public void IsValid_EmailSyntax_RejectsNonAddresses(string email, bool expected)
    {
        RegisterAccountCommand command = ValidCommand() with { Email = email };

        bool result = new RegistrationValidator().IsValid(command);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("a", true)]
    [DataRow("Muñoz_1", true)]
    [DataRow("  Player  ", true)]
    [DataRow("123456789012345", true)]
    [DataRow("", false)]
    [DataRow("1234567890123456", false)]
    [DataRow("Player@", false)]
    [DataRow("Player One", false)]
    [DataRow("Player\tOne", false)]
    [DataRow("Player\u200b", false)]
    [DataRow("Player\0", false)]
    [DataRow("\tPlayer", false)]
    [DataRow("Player\n", false)]
    [DataRow("Player!", false)]
    [DataRow("Guest", false)]
    [DataRow("gUeSt", false)]
    [DataRow("Guest-123", false)]
    public void IsValid_UserNameSyntax_EnforcesSharedRules(string name, bool expected)
    {
        RegisterAccountCommand command = ValidCommand() with { UserName = name };

        bool result = new RegistrationValidator().IsValid(command);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(7, false)]
    [DataRow(8, true)]
    [DataRow(63, true)]
    [DataRow(64, true)]
    [DataRow(65, false)]
    public void IsValid_PasswordLength_ChecksBothBoundaries(int length, bool expected)
    {
        string password = "A" + new string('x', length - 1);
        RegisterAccountCommand command = ValidCommand() with
        {
            Password = password,
            PasswordConfirmation = password
        };

        bool result = new RegistrationValidator().IsValid(command);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("", "", false)]
    [DataRow("onlylowercase", "onlylowercase", false)]
    [DataRow("ValidPassword", "validPassword", false)]
    [DataRow(" ValidPassword ", "ValidPassword", false)]
    [DataRow(" ValidPassword ", " ValidPassword ", true)]
    public void IsValid_PasswordPolicyAndConfirmation_PreservesExactPassword(
        string password, string confirmation, bool expected)
    {
        RegisterAccountCommand command = ValidCommand() with
        {
            Password = password,
            PasswordConfirmation = confirmation
        };

        bool result = new RegistrationValidator().IsValid(command);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(254, true)]
    [DataRow(255, false)]
    public void IsValid_EmailLength_EnforcesDatabaseBoundary(int length, bool expected)
    {
        string email = new string('a', 64) + "@" + new string('b', 63) + "."
            + new string('c', 63) + "." + new string('d', length - 193);
        RegisterAccountCommand command = ValidCommand() with { Email = email };

        bool result = new RegistrationValidator().IsValid(command);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(64, 63, true)]
    [DataRow(65, 63, false)]
    [DataRow(64, 64, false)]
    public void IsValid_EmailParts_RejectsExcessiveLocalOrDomainLabel(
        int localLength, int labelLength, bool expected)
    {
        string email = new string('a', localLength) + "@" + new string('b', labelLength) + ".com";
        RegisterAccountCommand command = ValidCommand() with { Email = email };

        bool result = new RegistrationValidator().IsValid(command);

        Assert.AreEqual(expected, result);
    }

    private static RegisterAccountCommand ValidCommand()
    {
        return new RegisterAccountCommand
        {
            UserName = "Player",
            Email = "player@example.com",
            Password = "ValidPassword",
            PasswordConfirmation = "ValidPassword"
        };
    }
}

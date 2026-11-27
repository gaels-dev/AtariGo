using AtariGo.Client.Models;

namespace AtariGo.Client.Tests.Models;

[TestClass]
[TestCategory("Unit")]
public sealed class PasswordRecoveryInputTests
{
    [TestMethod]
    [DataRow("012345", true)]
    [DataRow("000000", true)]
    [DataRow("999999", true)]
    [DataRow("", false)]
    [DataRow("12345", false)]
    [DataRow("1234567", false)]
    [DataRow("12345678", false)]
    [DataRow("１２３４５６", false)]
    [DataRow("12 456", false)]
    [DataRow("12345a", false)]
    [DataRow("12345\n", false)]
    public void Validate_CodeFormat_PreservesLeadingZerosAndRejectsNonAscii(string code, bool valid)
    {
        PasswordRecoveryInput input = ValidInput();
        input.Code = code;

        PasswordRecoveryResult? result = input.Validate();

        Assert.AreEqual(valid ? null : PasswordRecoveryResult.InvalidCode, result);
    }

    [TestMethod]
    [DataRow(7, false)]
    [DataRow(8, true)]
    [DataRow(63, true)]
    [DataRow(64, true)]
    [DataRow(65, false)]
    public void Validate_PasswordLength_EnforcesSharedBoundaries(int length, bool valid)
    {
        PasswordRecoveryInput input = ValidInput();
        input.NewPassword = "A" + new string('x', length - 1);
        input.Confirmation = input.NewPassword;

        PasswordRecoveryResult? result = input.Validate();

        Assert.AreEqual(valid ? null : PasswordRecoveryResult.InvalidPassword, result);
    }

    [TestMethod]
    [DataRow("", "Password9", PasswordRecoveryResult.MissingFields)]
    [DataRow("Password9", "", PasswordRecoveryResult.MissingFields)]
    [DataRow("lowercase9", "lowercase9", PasswordRecoveryResult.InvalidPassword)]
    [DataRow("Password9", "password9", PasswordRecoveryResult.ConfirmationMismatch)]
    [DataRow(" Password9 ", "Password9", PasswordRecoveryResult.ConfirmationMismatch)]
    public void Validate_PasswordFields_ReturnsSpecificFailure(
        string password, string confirmation, PasswordRecoveryResult expected)
    {
        PasswordRecoveryInput input = ValidInput();
        input.NewPassword = password;
        input.Confirmation = confirmation;

        PasswordRecoveryResult? result = input.Validate();

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("", false)]
    [DataRow("   ", false)]
    [DataRow(" Owner ", true)]
    public void IsIdentifierValid_EmptyOrTrimmedValue_ReturnsExpectedValidity(
        string identifier, bool expected)
    {
        Assert.AreEqual(expected, PasswordRecoveryInput.IsIdentifierValid(identifier));
    }

    private static PasswordRecoveryInput ValidInput()
    {
        return new PasswordRecoveryInput
        {
            Identifier = "Owner",
            Code = "012345",
            NewPassword = " Password9 ",
            Confirmation = " Password9 "
        };
    }
}

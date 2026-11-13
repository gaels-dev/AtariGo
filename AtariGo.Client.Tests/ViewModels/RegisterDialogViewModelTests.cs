using System.Globalization;
using System.Reflection;


using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class RegisterDialogViewModelTests
{
    [TestMethod]
    [DataRow("empty", "Register_Err_EmptyFields")]
    [DataRow("guest", "Register_Err_ReservedUsername")]
    [DataRow("email", "Register_Err_InvalidEmail")]
    [DataRow("password", "Register_Err_PasswordPolicy")]
    [DataRow("confirmation", "Register_Err_PasswordMismatch")]
    public void RegisterCommand_InvalidFields_PreservesValidationMessage(
        string scenario,
        string expectedKey)
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RegisterDialogViewModel viewModel = CreateValidViewModel(navigation);
        SetInvalidFields(viewModel, scenario);

        // Act
        viewModel.RegisterCommand.Execute(null);

        // Assert
        Assert.AreEqual(Resources.ResourceManager.GetString(expectedKey,
            CultureInfo.CurrentUICulture), viewModel.ErrorMessage);
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
    }

    [TestMethod]
    public void RegisterCommand_ValidFields_OpensVerification()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RegisterDialogViewModel viewModel = CreateValidViewModel(navigation);

        // Act
        viewModel.RegisterCommand.Execute(null);

        // Assert
        Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
        Assert.IsTrue(((RecordingNavigationProxy)navigation).Calls
            .Contains(nameof(INavigationService.OpenVerificationDialog)));
    }

    private static RegisterDialogViewModel CreateValidViewModel(INavigationService navigation)
    {
        return new RegisterDialogViewModel(navigation)
        {
            UserName = "Player",
            Email = "player@example.com",
            Password = "ValidPassword",
            PasswordConfirmation = "ValidPassword"
        };
    }

    private static void SetInvalidFields(RegisterDialogViewModel viewModel, string scenario)
    {
        switch (scenario)
        {
            case "empty":
                viewModel.UserName = string.Empty;
                break;
            case "guest":
                viewModel.UserName = "Guest-123";
                break;
            case "email":
                viewModel.Email = "invalid-email";
                break;
            case "password":
                viewModel.Password = "short";
                break;
            case "confirmation":
                viewModel.PasswordConfirmation = "DifferentPassword";
                break;
        }
    }
}

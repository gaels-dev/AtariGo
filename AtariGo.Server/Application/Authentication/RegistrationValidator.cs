using AtariGo.Contracts.Validation;

namespace AtariGo.Server.Application.Authentication;

public sealed class RegistrationValidator
{
    public bool IsValid(RegisterAccountCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return UserNameRules.IsValid(command.UserName)
            && EmailAddressRules.IsValid(command.Email)
            && PasswordPolicy.IsValid(command.Password)
            && string.Equals(command.Password, command.PasswordConfirmation,
                StringComparison.Ordinal);
    }
}

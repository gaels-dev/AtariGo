namespace AtariGo.Server.Application.Authentication;

public interface IRegistrationValidator
{
    bool IsValid(RegisterAccountCommand command);
}

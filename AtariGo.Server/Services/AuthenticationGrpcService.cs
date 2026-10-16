using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using Grpc.Core;

namespace AtariGo.Server.Services;

public sealed class AuthenticationGrpcService(
    IAuthenticationService authenticationService)
    : Authentication.AuthenticationBase
{
    public override async Task<LoginResponse> Login(
        LoginRequest request,
        ServerCallContext context)
    {
        LoginOutcome outcome = await authenticationService.LoginAsync(
            request.Identifier,
            request.Password,
            context.CancellationToken);

        return CreateLoginResponse(outcome);
    }

    public override async Task<RegisterAccountResponse> RegisterAccount(
        RegisterAccountRequest request,
        ServerCallContext context)
    {
        var command = new RegisterAccountCommand(
            request.UserName,
            request.Email,
            request.Password,
            request.PasswordConfirmation,
            request.ConfirmationText);

        RegistrationStatus status = await authenticationService.RegisterAccountAsync(
            command,
            context.CancellationToken);

        return new RegisterAccountResponse
        {
            Result = MapRegistrationStatus(status)
        };
    }

    private static LoginResponse CreateLoginResponse(LoginOutcome outcome)
    {
        var response = new LoginResponse
        {
            Result = MapLoginStatus(outcome.Status)
        };

        if (outcome.Status == LoginStatus.Success)
        {
            response.UserId = outcome.UserId;
            response.UserName = outcome.UserName;
            response.SessionToken = outcome.SessionToken;
        }

        return response;
    }

    private static AtariGo.Contracts.LoginResult MapLoginStatus(LoginStatus status)
    {
        return status switch
        {
            LoginStatus.Success => AtariGo.Contracts.LoginResult.Success,
            LoginStatus.UserNotFound => AtariGo.Contracts.LoginResult.UserNotFound,
            LoginStatus.IncorrectPassword => AtariGo.Contracts.LoginResult.IncorrectPassword,
            LoginStatus.AccountRestricted => AtariGo.Contracts.LoginResult.AccountRestricted,
            _ => AtariGo.Contracts.LoginResult.Unspecified
        };
    }

    private static AtariGo.Contracts.RegistrationResult MapRegistrationStatus(
        RegistrationStatus status)
    {
        return status switch
        {
            RegistrationStatus.Success => AtariGo.Contracts.RegistrationResult.Success,
            RegistrationStatus.UsernameTaken => AtariGo.Contracts.RegistrationResult.UsernameTaken,
            RegistrationStatus.EmailTaken => AtariGo.Contracts.RegistrationResult.EmailTaken,
            _ => AtariGo.Contracts.RegistrationResult.Error
        };
    }
}

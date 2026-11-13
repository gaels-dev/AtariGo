
using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;

namespace AtariGo.Server.Services;

public sealed class RegistrationGrpcService : Registration.RegistrationBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationGrpcService(IRegistrationService registrationService)
    {
        ArgumentNullException.ThrowIfNull(registrationService);
        _registrationService = registrationService;
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

    public override async Task<RegisterAccountResponse> RegisterAccount(
        RegisterAccountRequest request,
        ServerCallContext context)
    {
        var command = new RegisterAccountCommand
        {
            UserName = request.UserName,
            Email = request.Email,
            Password = request.Password,
            PasswordConfirmation = request.PasswordConfirmation,
            ConfirmationText = request.ConfirmationText
        };

        RegistrationStatus status = await _registrationService.RegisterAccountAsync(
            command,
            context.CancellationToken);

        return new RegisterAccountResponse
        {
            Result = MapRegistrationStatus(status)
        };
    }
}

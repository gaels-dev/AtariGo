using System.Threading.Tasks;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using Grpc.Core;

namespace AtariGo.Server.Services;

public sealed class RegistrationGrpcService(
    IRegistrationService registrationService)
    : Registration.RegistrationBase
{
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

        RegistrationStatus status = await registrationService.RegisterAccountAsync(
            command,
            context.CancellationToken);

        return new RegisterAccountResponse
        {
            Result = MapRegistrationStatus(status)
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

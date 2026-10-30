using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using Grpc.Core;

namespace AtariGo.Server.Services;

public sealed class AuthenticationGrpcService : Authentication.AuthenticationBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthenticationGrpcService(IAuthenticationService authenticationService)
    {
        ArgumentNullException.ThrowIfNull(authenticationService);
        _authenticationService = authenticationService;
    }

    public override async Task<LoginResponse> Login(
        LoginRequest request,
        ServerCallContext context)
    {
        LoginOperationResult result = await _authenticationService.LoginAsync(
            request.Identifier,
            request.Password,
            context.CancellationToken);

        return CreateLoginResponse(result);
    }

    private static LoginResponse CreateLoginResponse(LoginOperationResult result)
    {
        var response = new LoginResponse
        {
            Result = MapLoginStatus(result.Status)
        };

        if (result.Status == LoginStatus.Success)
        {
            response.UserId = result.UserId;
            response.UserName = result.UserName;
            response.SessionToken = result.SessionToken;
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

}

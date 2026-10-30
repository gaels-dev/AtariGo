using System;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;

namespace AtariGo.Server.Services
{
    public sealed class AuthenticationGrpcService : Authentication.AuthenticationBase
    {
        private readonly IAuthenticationService _authenticationService;

        public AuthenticationGrpcService(IAuthenticationService authenticationService)
        {
            ArgumentNullException.ThrowIfNull(authenticationService);
            _authenticationService = authenticationService;
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

        private static LoginResult MapLoginStatus(LoginStatus status)
        {
            return status switch
            {
                LoginStatus.Success => LoginResult.Success,
                LoginStatus.UserNotFound => LoginResult.UserNotFound,
                LoginStatus.IncorrectPassword => LoginResult.IncorrectPassword,
                LoginStatus.AccountRestricted => LoginResult.AccountRestricted,
                _ => LoginResult.Unspecified
            };
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
    }
}

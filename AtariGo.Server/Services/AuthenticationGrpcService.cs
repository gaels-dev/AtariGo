using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Services;

public sealed class AuthenticationGrpcService : Authentication.AuthenticationBase
{
    private static readonly IReadOnlyDictionary<LoginStatus, LoginResult> loginResults =
        new Dictionary<LoginStatus, LoginResult>
        {
            [LoginStatus.Success] = LoginResult.Success,
            [LoginStatus.ChallengeRequired] = LoginResult.ChallengeRequired,
            [LoginStatus.AccountRestricted] = LoginResult.AccountRestricted,
            [LoginStatus.InvalidCode] = LoginResult.InvalidCode,
            [LoginStatus.CodeExpired] = LoginResult.CodeExpired,
            [LoginStatus.InvalidChallenge] = LoginResult.InvalidChallenge,
            [LoginStatus.TemporarilyLocked] = LoginResult.TemporarilyLocked,
            [LoginStatus.DeliveryFailed] = LoginResult.DeliveryFailed,
            [LoginStatus.RateLimited] = LoginResult.RateLimited,
            [LoginStatus.Unavailable] = LoginResult.Unavailable
        };
    private readonly IAuthenticationService _authentication;
    private readonly AuthenticationRequestLimiter _limiter;
    private readonly ILogger<AuthenticationGrpcService> _logger;

    public AuthenticationGrpcService(IAuthenticationService authentication,
        AuthenticationRequestLimiter limiter, ILogger<AuthenticationGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(logger);
        _authentication = authentication;
        _limiter = limiter;
        _logger = logger;
    }

    private async Task<LoginResponse> ExecuteAsync(Func<Task<LoginOperationResult>> operation,
        ServerCallContext context, IdentityOperation diagnosticOperation)
    {
        string address = context.GetHttpContext().Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        if (!_limiter.TryAcquireLogin(address))
        {
            ServerIdentityDiagnostics.Completed(_logger, diagnosticOperation,
                LoginStatus.RateLimited);
            return new LoginResponse { Result = LoginResult.RateLimited };
        }
        try
        {
            LoginOperationResult result = await operation();
            ServerIdentityDiagnostics.Record(_logger, diagnosticOperation,
                IdentityDiagnostics.Result(diagnosticOperation, result.Status) with
                {
                    Context = new DiagnosticContext
                    {
                        UserId = result.UserId > 0 ? result.UserId : null
                    }
                });
            return CreateResponse(result);
        }
        catch (OperationCanceledException exception)
        {
            ServerIdentityDiagnostics.Failure(_logger, diagnosticOperation,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled,
                "The login operation was not confirmed."));
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException
            or InvalidOperationException)
        {
            ServerIdentityDiagnostics.Failure(_logger, diagnosticOperation,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Unavailable,
                "The login operation was not confirmed."));
        }
    }

    private static LoginResponse CreateResponse(LoginOperationResult result)
    {
        var response = new LoginResponse
        {
            Result = MapStatus(result.Status),
            ChallengeToken = result.ChallengeToken,
            MaskedEmail = result.MaskedEmail,
            RestrictionReason = result.RestrictionReason,
            CodeExpiresAtUnixSeconds = ToUnixSeconds(result.CodeExpiresAt),
            RetryAtUnixSeconds = ToUnixSeconds(result.RetryAt)
        };
        if (result.Status == LoginStatus.Success)
        {
            response.UserId = result.UserId;
            response.UserName = result.UserName;
            response.SessionToken = result.SessionToken;
        }
        return response;
    }

    private static long ToUnixSeconds(DateTime? value)
    {
        return value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
                .ToUnixTimeSeconds() : 0;
    }

    private static LoginResult MapStatus(LoginStatus status)
    {
        return loginResults.GetValueOrDefault(status, LoginResult.InvalidCredentials);
    }

    private async Task<LoginOperationResult> CancelAsync(string token,
        CancellationToken cancellationToken)
    {
        await _authentication.CancelAsync(token, cancellationToken);
        return new LoginOperationResult(LoginStatus.InvalidChallenge);
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var command = new LoginCommand
        {
            Identifier = request.Identifier,
            Password = request.Password,
            CultureName = request.CultureName
        };
        try
        {
            return await ExecuteAsync(() => _authentication.LoginAsync(command,
                context.CancellationToken), context, IdentityOperation.Login);
        }
        finally
        {
            request.Password = string.Empty;
            command.Password = string.Empty;
        }
    }

    public override async Task<LoginResponse> VerifyLoginCode(VerifyLoginCodeRequest request,
        ServerCallContext context)
    {
        try
        {
            return await ExecuteAsync(() => _authentication.VerifyAsync(
                new LoginCodeCommand(request.ChallengeToken, request.Code),
                context.CancellationToken), context, IdentityOperation.VerifyLoginCode);
        }
        finally
        {
            request.Code = string.Empty;
        }
    }

    public override Task<LoginResponse> ResendLoginCode(ResendLoginCodeRequest request,
        ServerCallContext context)
    {
        return ExecuteAsync(() => _authentication.ResendAsync(
            new LoginChallengeCommand(request.ChallengeToken, request.CultureName),
            context.CancellationToken), context, IdentityOperation.ResendLoginCode);
    }

    public override async Task<CancelLoginResponse> CancelLogin(CancelLoginRequest request,
        ServerCallContext context)
    {
        await ExecuteAsync(() => CancelAsync(request.ChallengeToken, context.CancellationToken),
            context, IdentityOperation.CancelLogin);
        return new CancelLoginResponse();
    }
}

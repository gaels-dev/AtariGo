using System.Globalization;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcAuthenticationClient : IAuthenticationClient
{
    private static readonly IReadOnlyDictionary<LoginResult, LoginAttemptStatus> loginResults =
        new Dictionary<LoginResult, LoginAttemptStatus>
        {
            [LoginResult.Success] = LoginAttemptStatus.Success,
            [LoginResult.ChallengeRequired] = LoginAttemptStatus.ChallengeRequired,
            [LoginResult.AccountRestricted] = LoginAttemptStatus.AccountRestricted,
            [LoginResult.InvalidCode] = LoginAttemptStatus.InvalidCode,
            [LoginResult.CodeExpired] = LoginAttemptStatus.CodeExpired,
            [LoginResult.InvalidChallenge] = LoginAttemptStatus.InvalidChallenge,
            [LoginResult.TemporarilyLocked] = LoginAttemptStatus.TemporarilyLocked,
            [LoginResult.DeliveryFailed] = LoginAttemptStatus.DeliveryFailed,
            [LoginResult.RateLimited] = LoginAttemptStatus.RateLimited,
            [LoginResult.Unavailable] = LoginAttemptStatus.Unavailable,
            [LoginResult.Unconfirmed] = LoginAttemptStatus.Unconfirmed
        };
    private readonly Authentication.AuthenticationClient _client;

    public GrpcAuthenticationClient() : this(GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcAuthenticationClient(CallInvoker callInvoker)
    {
        ArgumentNullException.ThrowIfNull(callInvoker);
        _client = new Authentication.AuthenticationClient(callInvoker);
    }

    public async Task<LoginAttemptResult> LoginAsync(string identifier, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(password);
        var request = new LoginRequest
        {
            Identifier = identifier,
            Password = password,
            CultureName = CultureInfo.CurrentUICulture.Name
        };
        try
        {
            return await ExecuteAsync(() => _client.LoginAsync(request,
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync,
                IdentityOperation.Login, cancellationToken);
        }
        finally
        {
            request.Password = string.Empty;
        }
    }

    public async Task<LoginAttemptResult> VerifyLoginCodeAsync(string token, string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(code);
        var request = new VerifyLoginCodeRequest { ChallengeToken = token, Code = code };
        try
        {
            return await ExecuteAsync(() => _client.VerifyLoginCodeAsync(request,
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync,
                IdentityOperation.VerifyLoginCode, cancellationToken);
        }
        finally
        {
            request.Code = string.Empty;
            request.ChallengeToken = string.Empty;
        }
    }

    public Task<LoginAttemptResult> ResendLoginCodeAsync(string token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        var request = new ResendLoginCodeRequest
        {
            ChallengeToken = token,
            CultureName = CultureInfo.CurrentUICulture.Name
        };
        return ExecuteAsync(() => _client.ResendLoginCodeAsync(request,
            GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync,
            IdentityOperation.ResendLoginCode, cancellationToken);
    }

    public async Task CancelLoginAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        try
        {
            await _client.CancelLoginAsync(new CancelLoginRequest { ChallengeToken = token },
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync;
            ClientIdentityDiagnostics.Record(IdentityOperation.CancelLogin,
                new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded });
        }
        catch (Exception exception) when (exception is RpcException or OperationCanceledException)
        {
            ClientIdentityDiagnostics.Failure(IdentityOperation.CancelLogin, exception,
                cancellationToken);
        }
    }

    private static async Task<LoginAttemptResult> ExecuteAsync(Func<Task<LoginResponse>> operation,
        IdentityOperation diagnosticOperation, CancellationToken cancellationToken)
    {
        try
        {
            LoginResponse response = await operation();
            var result = new LoginAttemptResult
            {
                Status = MapStatus(response.Result),
                UserName = response.UserName,
                SessionToken = response.SessionToken,
                ChallengeToken = response.ChallengeToken,
                MaskedEmail = response.MaskedEmail,
                RestrictionReason = response.RestrictionReason,
                CodeExpiresAtUnixSeconds = response.CodeExpiresAtUnixSeconds,
                RetryAtUnixSeconds = response.RetryAtUnixSeconds
            };
            ClientIdentityDiagnostics.Record(diagnosticOperation,
                IdentityDiagnostics.Result(diagnosticOperation, result.Status) with
                {
                    Context = new DiagnosticContext
                    {
                        UserId = response.UserId > 0 ? response.UserId : null
                    }
                });
            return result;
        }
        catch (Exception exception) when (exception is RpcException or OperationCanceledException)
        {
            ClientIdentityDiagnostics.Failure(diagnosticOperation, exception, cancellationToken);
            return new LoginAttemptResult { Status = LoginAttemptStatus.Unconfirmed };
        }
    }

    private static LoginAttemptStatus MapStatus(LoginResult result)
    {
        return loginResults.GetValueOrDefault(result, LoginAttemptStatus.InvalidCredentials);
    }
}

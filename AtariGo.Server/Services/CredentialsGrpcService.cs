using System.Data.Common;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.Credentials;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Services;

public sealed class CredentialsGrpcService : Credentials.CredentialsBase
{
    private readonly ICredentialsService _credentials;
    private readonly ISessionService _sessions;
    private readonly ILogger<CredentialsGrpcService> _logger;

    public CredentialsGrpcService(ICredentialsService credentials, ISessionService sessions,
        ILogger<CredentialsGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(logger);
        _credentials = credentials;
        _sessions = sessions;
        _logger = logger;
    }

    private async Task<ChangePasswordResponse> ChangeCredentialAsync(ChangePasswordRequest request,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessions.ValidateSessionAsync(token,
            context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }
        var command = new ChangePasswordCommand
        {
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword,
            Confirmation = request.Confirmation
        };
        ChangePasswordResult result = await _credentials.ChangePasswordAsync(identity, command,
            context.CancellationToken);
        ServerIdentityDiagnostics.Record(_logger, IdentityOperation.ChangePassword,
            IdentityDiagnostics.Result(IdentityOperation.ChangePassword, result) with
            {
                Context = new DiagnosticContext { UserId = identity.UserId }
            });
        return new ChangePasswordResponse { Status = MapResult(result) };
    }

    private static PasswordChangeStatus MapResult(ChangePasswordResult result)
    {
        return result switch
        {
            ChangePasswordResult.Changed => PasswordChangeStatus.Changed,
            ChangePasswordResult.IncorrectCurrentPassword =>
                PasswordChangeStatus.IncorrectCurrentPassword,
            ChangePasswordResult.InvalidNewPassword => PasswordChangeStatus.InvalidNewPassword,
            ChangePasswordResult.ConfirmationMismatch => PasswordChangeStatus.ConfirmationMismatch,
            ChangePasswordResult.CredentialChanged => PasswordChangeStatus.CredentialChanged,
            _ => PasswordChangeStatus.AccountUnavailable
        };
    }

    public override async Task<ChangePasswordResponse> ChangePassword(ChangePasswordRequest request,
        ServerCallContext context)
    {
        try
        {
            return await ChangeCredentialAsync(request, context);
        }
        catch (OperationCanceledException exception)
            when (context.CancellationToken.IsCancellationRequested)
        {
            ServerIdentityDiagnostics.Failure(_logger, IdentityOperation.ChangePassword,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled,
                "The password change was not confirmed."));
        }
        catch (DbException exception)
        {
            ServerIdentityDiagnostics.Failure(_logger, IdentityOperation.ChangePassword,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Unavailable,
                "The password change was not confirmed."));
        }
    }
}

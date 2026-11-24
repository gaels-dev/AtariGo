using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Services;

public sealed class PasswordRecoveryGrpcService : PasswordRecovery.PasswordRecoveryBase
{
    private readonly IPasswordRecoveryService _recovery;
    private readonly AuthenticationRequestLimiter _limiter;
    private readonly ILogger<PasswordRecoveryGrpcService> _logger;

    public PasswordRecoveryGrpcService(IPasswordRecoveryService recovery,
        AuthenticationRequestLimiter limiter, ILogger<PasswordRecoveryGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(logger);
        _recovery = recovery;
        _limiter = limiter;
        _logger = logger;
    }

    private bool CanRequest(ServerCallContext context)
    {
        string address = context.GetHttpContext().Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return _limiter.TryAcquireRecovery(address);
    }

    private async Task<PasswordRecoveryResponse> ExecuteAsync(Func<Task<RecoveryResult>> operation,
        IdentityOperation diagnosticOperation, CancellationToken cancellationToken)
    {
        try
        {
            RecoveryResult result = await operation();
            ServerIdentityDiagnostics.Completed(_logger, diagnosticOperation, result);
            return CreateResponse(result);
        }
        catch (OperationCanceledException exception)
        {
            ServerIdentityDiagnostics.Failure(_logger, diagnosticOperation,
                (exception, cancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled,
                "The password recovery operation was not confirmed."));
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            ServerIdentityDiagnostics.Failure(_logger, diagnosticOperation,
                (exception, cancellationToken));
            throw new RpcException(new Status(StatusCode.Unavailable,
                "The password recovery operation was not confirmed."));
        }
    }

    private static PasswordRecoveryResponse CreateResponse(RecoveryResult result)
    {
        RecoveryStatus status = result switch
        {
            RecoveryResult.Accepted => RecoveryStatus.Accepted,
            RecoveryResult.Reset => RecoveryStatus.Reset,
            RecoveryResult.InvalidCode => RecoveryStatus.InvalidCode,
            RecoveryResult.InvalidPassword => RecoveryStatus.InvalidPassword,
            RecoveryResult.ConfirmationMismatch => RecoveryStatus.ConfirmationMismatch,
            RecoveryResult.RateLimited => RecoveryStatus.RateLimited,
            RecoveryResult.DeliveryFailed => RecoveryStatus.DeliveryFailed,
            RecoveryResult.NotSaved => RecoveryStatus.NotSaved,
            _ => RecoveryStatus.Unavailable
        };
        return new PasswordRecoveryResponse { Status = status };
    }

    public override Task<PasswordRecoveryResponse> RequestPasswordRecovery(
        PasswordRecoveryRequest request, ServerCallContext context)
    {
        if (!CanRequest(context))
        {
            ServerIdentityDiagnostics.Completed(_logger, IdentityOperation.RequestPasswordRecovery,
                RecoveryResult.RateLimited);
            return Task.FromResult(CreateResponse(RecoveryResult.RateLimited));
        }
        var command = new RecoveryRequestCommand
        {
            Identifier = request.Identifier,
            CultureName = request.CultureName
        };
        return ExecuteAsync(() => _recovery.RequestAsync(command, context.CancellationToken),
            IdentityOperation.RequestPasswordRecovery, context.CancellationToken);
    }

    public override Task<PasswordRecoveryResponse> ResetPassword(ResetPasswordRequest request,
        ServerCallContext context)
    {
        if (!CanRequest(context))
        {
            ServerIdentityDiagnostics.Completed(_logger, IdentityOperation.ResetPassword,
                RecoveryResult.RateLimited);
            return Task.FromResult(CreateResponse(RecoveryResult.RateLimited));
        }
        var command = new PasswordResetCommand
        {
            Identifier = request.Identifier,
            Code = request.Code,
            NewPassword = request.NewPassword,
            Confirmation = request.Confirmation
        };
        return ExecuteAsync(() => _recovery.ResetAsync(command, context.CancellationToken),
            IdentityOperation.ResetPassword, context.CancellationToken);
    }
}

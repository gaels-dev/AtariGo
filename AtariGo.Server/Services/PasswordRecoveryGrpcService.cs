using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.PasswordRecovery;

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

    private async Task<PasswordRecoveryResponse> ExecuteAsync(Func<Task<RecoveryResult>> operation)
    {
        try
        {
            RecoveryResult result = await operation();
            if (result == RecoveryResult.NotSaved)
            {
                _logger.LogWarning("The password reset write was rolled back.");
            }
            return CreateResponse(result);
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled,
                "The password recovery operation was not confirmed."));
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            _logger.LogError("Password recovery failed with {ErrorType}.",
                exception.GetType().Name);
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
            return Task.FromResult(CreateResponse(RecoveryResult.RateLimited));
        }
        var command = new RecoveryRequestCommand
        {
            Identifier = request.Identifier,
            CultureName = request.CultureName
        };
        return ExecuteAsync(() => _recovery.RequestAsync(command, context.CancellationToken));
    }

    public override Task<PasswordRecoveryResponse> ResetPassword(ResetPasswordRequest request,
        ServerCallContext context)
    {
        if (!CanRequest(context))
        {
            return Task.FromResult(CreateResponse(RecoveryResult.RateLimited));
        }
        var command = new PasswordResetCommand
        {
            Identifier = request.Identifier,
            Code = request.Code,
            NewPassword = request.NewPassword,
            Confirmation = request.Confirmation
        };
        return ExecuteAsync(() => _recovery.ResetAsync(command, context.CancellationToken));
    }
}

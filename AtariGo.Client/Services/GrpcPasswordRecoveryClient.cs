using System.Diagnostics;
using System.Globalization;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcPasswordRecoveryClient : IPasswordRecoveryClient
{
    private readonly PasswordRecovery.PasswordRecoveryClient _client;

    public GrpcPasswordRecoveryClient()
        : this(GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcPasswordRecoveryClient(CallInvoker callInvoker)
    {
        ArgumentNullException.ThrowIfNull(callInvoker);
        _client = new PasswordRecovery.PasswordRecoveryClient(callInvoker);
    }

    public Task<PasswordRecoveryResult> RequestAsync(string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var request = new PasswordRecoveryRequest
        {
            Identifier = identifier.Trim(),
            CultureName = CultureInfo.CurrentUICulture.Name
        };
        return ExecuteAsync(() => _client.RequestPasswordRecoveryAsync(request,
            GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync);
    }

    public async Task<PasswordRecoveryResult> ResetAsync(PasswordRecoveryInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        PasswordRecoveryResult? validation = input.Validate();
        if (validation.HasValue)
        {
            return validation.Value;
        }
        var request = new ResetPasswordRequest
        {
            Identifier = input.Identifier,
            Code = input.Code,
            NewPassword = input.NewPassword,
            Confirmation = input.Confirmation
        };
        try
        {
            return await ExecuteAsync(() => _client.ResetPasswordAsync(request,
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync);
        }
        finally
        {
            request.Code = string.Empty;
            request.NewPassword = string.Empty;
            request.Confirmation = string.Empty;
        }
    }

    private static async Task<PasswordRecoveryResult> ExecuteAsync(
        Func<Task<PasswordRecoveryResponse>> operation)
    {
        try
        {
            PasswordRecoveryResponse response = await operation();
            return MapStatus(response.Status);
        }
        catch (Exception exception) when (exception is RpcException or OperationCanceledException)
        {
            Trace.TraceWarning("Password recovery was not confirmed. Error type: {0}.",
                exception.GetType().Name);
            return PasswordRecoveryResult.Unconfirmed;
        }
    }

    private static PasswordRecoveryResult MapStatus(RecoveryStatus status)
    {
        return status switch
        {
            RecoveryStatus.Accepted => PasswordRecoveryResult.Accepted,
            RecoveryStatus.Reset => PasswordRecoveryResult.Reset,
            RecoveryStatus.InvalidCode => PasswordRecoveryResult.InvalidCode,
            RecoveryStatus.InvalidPassword => PasswordRecoveryResult.InvalidPassword,
            RecoveryStatus.ConfirmationMismatch => PasswordRecoveryResult.ConfirmationMismatch,
            RecoveryStatus.RateLimited => PasswordRecoveryResult.RateLimited,
            RecoveryStatus.Unavailable => PasswordRecoveryResult.Unavailable,
            RecoveryStatus.DeliveryFailed => PasswordRecoveryResult.DeliveryFailed,
            RecoveryStatus.NotSaved => PasswordRecoveryResult.NotSaved,
            _ => PasswordRecoveryResult.Unconfirmed
        };
    }
}

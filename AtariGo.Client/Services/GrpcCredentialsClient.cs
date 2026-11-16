using System.Diagnostics;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcCredentialsClient : ICredentialsClient
{
    private readonly string _sessionToken;
    private readonly Credentials.CredentialsClient _client;

    public GrpcCredentialsClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcCredentialsClient(string sessionToken, CallInvoker callInvoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(callInvoker);
        _sessionToken = sessionToken;
        _client = new Credentials.CredentialsClient(callInvoker);
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(PasswordChangeInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        PasswordChangeResult? validation = input.Validate();
        if (validation.HasValue)
        {
            return validation.Value;
        }
        var request = new ChangePasswordRequest
        {
            CurrentPassword = input.CurrentPassword,
            NewPassword = input.NewPassword,
            Confirmation = input.Confirmation
        };
        PasswordChangeResult result;
        try
        {
            var headers = new Metadata { { "authorization", $"Bearer {_sessionToken}" } };
            ChangePasswordResponse response = await _client.ChangePasswordAsync(request,
                GrpcCallPolicy.CreateOptions(cancellationToken, headers)).ResponseAsync;
            result = MapStatus(response.Status);
        }
        catch (RpcException exception)
        {
            result = exception.StatusCode == StatusCode.Unauthenticated
                ? PasswordChangeResult.SessionExpired : PasswordChangeResult.Unconfirmed;
            LogFailure(exception, cancellationToken);
        }
        catch (OperationCanceledException exception)
        {
            result = PasswordChangeResult.Unconfirmed;
            LogFailure(exception, cancellationToken);
        }
        finally
        {
            request.CurrentPassword = string.Empty;
            request.NewPassword = string.Empty;
            request.Confirmation = string.Empty;
        }
        return result;
    }

    private static PasswordChangeResult MapStatus(PasswordChangeStatus status)
    {
        return status switch
        {
            PasswordChangeStatus.Changed => PasswordChangeResult.Changed,
            PasswordChangeStatus.IncorrectCurrentPassword =>
                PasswordChangeResult.IncorrectCurrentPassword,
            PasswordChangeStatus.InvalidNewPassword => PasswordChangeResult.InvalidNewPassword,
            PasswordChangeStatus.ConfirmationMismatch => PasswordChangeResult.ConfirmationMismatch,
            PasswordChangeStatus.CredentialChanged => PasswordChangeResult.CredentialChanged,
            PasswordChangeStatus.AccountUnavailable => PasswordChangeResult.AccountUnavailable,
            _ => PasswordChangeResult.Unconfirmed
        };
    }

    private static void LogFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            Trace.TraceWarning("Password change was not confirmed. Error type: {0}.",
                exception.GetType().Name);
        }
    }
}

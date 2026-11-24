using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

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
            Metadata headers = GrpcCallPolicy.CreateHeaders(_sessionToken);
            ChangePasswordResponse response = await _client.ChangePasswordAsync(request,
                GrpcCallPolicy.CreateOptions(cancellationToken, headers)).ResponseAsync;
            result = MapStatus(response.Status);
            ClientIdentityDiagnostics.Completed(IdentityOperation.ChangePassword, result);
        }
        catch (RpcException exception)
        {
            result = exception.StatusCode == StatusCode.Unauthenticated
                ? PasswordChangeResult.SessionExpired : PasswordChangeResult.Unconfirmed;
            ClientIdentityDiagnostics.Failure(IdentityOperation.ChangePassword, exception,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
        {
            result = PasswordChangeResult.Unconfirmed;
            ClientIdentityDiagnostics.Failure(IdentityOperation.ChangePassword, exception,
                cancellationToken);
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

}

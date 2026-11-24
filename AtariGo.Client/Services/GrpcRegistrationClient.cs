using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services
{
    public sealed class GrpcRegistrationClient : IRegistrationClient
    {
        private readonly Registration.RegistrationClient _client;

        public GrpcRegistrationClient()
            : this(GrpcClientConnection.Shared.CallInvoker)
        {
        }

        public GrpcRegistrationClient(CallInvoker callInvoker)
        {
            ArgumentNullException.ThrowIfNull(callInvoker);
            _client = new Registration.RegistrationClient(callInvoker);
        }

        public async Task<RegisterAccountResponse> RegisterAccountAsync(
            RegistrationDraft registrationDraft,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(registrationDraft);

            var request = new RegisterAccountRequest
            {
                UserName = registrationDraft.UserName,
                Email = registrationDraft.Email,
                Password = registrationDraft.Password,
                PasswordConfirmation = registrationDraft.PasswordConfirmation
            };
            try
            {
                RegisterAccountResponse response = await GrpcCallPolicy.ExecuteAsync(() =>
                    _client.RegisterAccountAsync(request,
                        GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync);
                ClientIdentityDiagnostics.Completed(IdentityOperation.RegisterAccount,
                    response.Result);
                return response;
            }
            finally
            {
                request.Password = string.Empty;
                request.PasswordConfirmation = string.Empty;
            }
        }
    }
}

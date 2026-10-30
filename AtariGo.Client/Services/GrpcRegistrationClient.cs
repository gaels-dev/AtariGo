using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;

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

        public Task<RegisterAccountResponse> RegisterAccountAsync(
            RegistrationDraft registrationDraft,
            string confirmationText,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(registrationDraft);

            return _client.RegisterAccountAsync(
                new RegisterAccountRequest
                {
                    UserName = registrationDraft.UserName,
                    Email = registrationDraft.Email,
                    Password = registrationDraft.Password,
                    PasswordConfirmation = registrationDraft.PasswordConfirmation,
                    ConfirmationText = confirmationText
                },
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync;
        }
    }
}

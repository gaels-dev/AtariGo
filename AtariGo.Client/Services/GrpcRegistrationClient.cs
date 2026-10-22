using System;
using System.Threading;
using System.Threading.Tasks;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using Grpc.Net.Client;

namespace AtariGo.Client.Services;

public sealed class GrpcRegistrationClient : IRegistrationClient
{
    private readonly GrpcChannel _channel = GrpcChannel.ForAddress(
        GrpcClientSettings.ServerAddress);
    private Registration.RegistrationClient? _client;

    public Task<RegisterAccountResponse> RegisterAccountAsync(
        RegistrationDraft registrationDraft,
        string confirmationText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationDraft);
        _client ??= new Registration.RegistrationClient(_channel);

        return _client.RegisterAccountAsync(
            new RegisterAccountRequest
            {
                UserName = registrationDraft.UserName,
                Email = registrationDraft.Email,
                Password = registrationDraft.Password,
                PasswordConfirmation = registrationDraft.PasswordConfirmation,
                ConfirmationText = confirmationText
            },
            cancellationToken: cancellationToken).ResponseAsync;
    }
}

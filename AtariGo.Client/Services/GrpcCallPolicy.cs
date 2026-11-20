using Grpc.Core;

using AtariGo.Client.Models;

namespace AtariGo.Client.Services
{
    public static class GrpcCallPolicy
    {
        public const int RequestTimeoutSeconds = 10;
        private const string GuestCredentialHeader = "x-guest-credential";

        public static CallOptions CreateOptions(
            CancellationToken cancellationToken,
            Metadata? headers = null)
        {
            return new CallOptions(
                headers: headers,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: cancellationToken);
        }

        public static bool IsRequestedCancellation(
            Exception exception,
            CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested
                && (exception is OperationCanceledException
                    || GrpcClientFailures.GetReason(exception) == ClientFailureReason.Cancelled);
        }

        public static Metadata CreateHeaders(string? sessionToken, string? guestCredential = null)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken)
                && !string.IsNullOrWhiteSpace(guestCredential))
            {
                throw new ArgumentException("Only one authentication credential is allowed.");
            }
            var headers = new Metadata();
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                headers.Add("authorization", $"Bearer {sessionToken}");
            }
            if (!string.IsNullOrWhiteSpace(guestCredential))
            {
                headers.Add(GuestCredentialHeader, guestCredential);
            }
            return headers;
        }

        public static async Task<TResponse> ExecuteAsync<TResponse>(Func<Task<TResponse>> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            try
            {
                return await operation();
            }
            catch (RpcException exception)
            {
                throw new ClientRequestException(GrpcClientFailures.GetReason(exception),
                    GrpcClientFailures.GetErrorCode(exception), exception);
            }
        }
    }
}

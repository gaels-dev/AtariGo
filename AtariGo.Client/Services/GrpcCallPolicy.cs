using System;
using System.Threading;

using Grpc.Core;

namespace AtariGo.Client.Services
{
    public static class GrpcCallPolicy
    {
        public const int RequestTimeoutSeconds = 10;

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
                    || exception is RpcException { StatusCode: StatusCode.Cancelled });
        }
    }
}

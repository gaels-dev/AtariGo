using Grpc.Core;

using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public static class GrpcClientFailures
{
    public static ClientFailureReason GetReason(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            ClientRequestException request => request.Reason,
            RpcException { StatusCode: StatusCode.ResourceExhausted } =>
                ClientFailureReason.CapacityExceeded,
            RpcException rpcException => MapStatus(rpcException.StatusCode),
            OperationCanceledException => ClientFailureReason.Timeout,
            _ => ClientFailureReason.RequestFailed
        };
    }

    public static bool IsTransportFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is RpcException or ClientRequestException or OperationCanceledException;
    }

    public static string GetErrorCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            ClientRequestException request => request.ErrorCode,
            RpcException rpc => rpc.Status.Detail,
            _ => string.Empty
        };
    }

    private static ClientFailureReason MapStatus(StatusCode statusCode)
    {
        return statusCode switch
        {
            StatusCode.Cancelled => ClientFailureReason.Cancelled,
            StatusCode.DeadlineExceeded => ClientFailureReason.Timeout,
            StatusCode.Unavailable => ClientFailureReason.Unavailable,
            StatusCode.Unauthenticated => ClientFailureReason.SessionExpired,
            StatusCode.PermissionDenied => ClientFailureReason.PermissionDenied,
            StatusCode.NotFound => ClientFailureReason.NotFound,
            StatusCode.Aborted => ClientFailureReason.VersionConflict,
            StatusCode.InvalidArgument => ClientFailureReason.InvalidRequest,
            StatusCode.AlreadyExists => ClientFailureReason.ConnectionReplaced,
            _ => ClientFailureReason.RequestFailed
        };
    }
}

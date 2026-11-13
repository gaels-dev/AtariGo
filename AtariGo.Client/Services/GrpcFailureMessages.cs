using System.Diagnostics;
using System.Globalization;

using Grpc.Core;

using AtariGo.Client.Properties;

namespace AtariGo.Client.Services
{
    public static class GrpcFailureMessages
    {
        public static string GetMessage(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Trace.TraceWarning("Client request failed. Exception type: {0}.",
                exception.GetType().Name);

            string resourceKey = exception is RpcException rpcException
                ? GetResourceKey(rpcException.StatusCode)
                : "Connection_Err_RequestFailed";
            if (exception is RpcException failure)
            {
                Trace.TraceWarning("Client request failed. RPC status: {0}.", failure.StatusCode);
            }

            return GetLocalizedMessage(resourceKey);
        }

        public static string GetRoomJoinMessage(Exception exception)
        {
            string message = GetMessage(exception);
            if (exception is RpcException { StatusCode: StatusCode.NotFound })
            {
                message = GetLocalizedMessage("JoinPrivateRoom_Err_UnknownCode");
            }
            else if (exception is RpcException { StatusCode: StatusCode.ResourceExhausted })
            {
                message = GetLocalizedMessage("JoinPrivateRoom_Err_Full");
            }

            return message;
        }

        public static string GetRegistrationMessage(RpcException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Trace.TraceWarning("Registration request failed. RPC status: {0}.",
                exception.StatusCode);

            string resourceKey = exception.StatusCode switch
            {
                StatusCode.DeadlineExceeded or StatusCode.Unavailable or StatusCode.Cancelled
                    => "Registration_Err_Unconfirmed",
                _ => GetResourceKey(exception.StatusCode)
            };

            return GetLocalizedMessage(resourceKey);
        }

        public static string GetResourceKey(StatusCode statusCode)
        {
            return statusCode switch
            {
                StatusCode.DeadlineExceeded => "Connection_Err_Timeout",
                StatusCode.Unavailable => "Connection_Err_Unavailable",
                StatusCode.Unauthenticated => "Connection_Err_SessionExpired",
                StatusCode.Cancelled => "Connection_Err_Cancelled",
                _ => "Connection_Err_RequestFailed"
            };
        }

        private static string GetLocalizedMessage(string resourceKey)
        {
            return Resources.ResourceManager.GetString(
                resourceKey,
                CultureInfo.CurrentUICulture) ?? resourceKey;
        }
    }
}

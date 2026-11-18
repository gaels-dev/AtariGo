using System.Diagnostics;
using System.Globalization;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;

namespace AtariGo.Client.Services
{
    public static class ClientFailureMessages
    {
        public static string GetMessage(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Trace.TraceWarning("Client request failed. Exception type: {0}.",
                exception.GetType().Name);

            string resourceKey = GetResourceKey(GrpcClientFailures.GetReason(exception));

            return GetLocalizedMessage(resourceKey);
        }

        public static string GetRoomJoinMessage(Exception exception)
        {
            string message = GetMessage(exception);
            ClientFailureReason reason = GrpcClientFailures.GetReason(exception);
            if (reason == ClientFailureReason.NotFound)
            {
                message = GetLocalizedMessage("JoinPrivateRoom_Err_UnknownCode");
            }
            else if (reason == ClientFailureReason.CapacityExceeded)
            {
                message = GetLocalizedMessage("JoinPrivateRoom_Err_Full");
            }

            return message;
        }

        public static string GetRegistrationMessage(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            ClientFailureReason reason = GrpcClientFailures.GetReason(exception);
            Trace.TraceWarning("Registration request failed. Reason: {0}.", reason);

            string resourceKey = reason switch
            {
                ClientFailureReason.Timeout or ClientFailureReason.Unavailable
                    or ClientFailureReason.Cancelled
                    => "Registration_Err_Unconfirmed",
                _ => GetResourceKey(reason)
            };

            return GetLocalizedMessage(resourceKey);
        }

        public static string GetResourceKey(ClientFailureReason reason)
        {
            return reason switch
            {
                ClientFailureReason.Timeout => "Connection_Err_Timeout",
                ClientFailureReason.Unavailable => "Connection_Err_Unavailable",
                ClientFailureReason.SessionExpired => "Connection_Err_SessionExpired",
                ClientFailureReason.Cancelled => "Connection_Err_Cancelled",
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

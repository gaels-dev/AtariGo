namespace AtariGo.Client.Models;

public enum ClientFailureReason
{
    None,
    Cancelled,
    Timeout,
    Unavailable,
    SessionExpired,
    PermissionDenied,
    NotFound,
    VersionConflict,
    RequestFailed,
    InvalidRequest,
    ConnectionReplaced
}

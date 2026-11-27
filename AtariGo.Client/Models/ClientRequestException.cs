namespace AtariGo.Client.Models;

public sealed class ClientRequestException : Exception
{
    public ClientRequestException(ClientFailureReason reason, string errorCode,
        Exception innerException)
        : base("The remote request could not be completed.", innerException)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        ArgumentNullException.ThrowIfNull(innerException);
        Reason = reason;
        ErrorCode = errorCode;
    }

    public ClientFailureReason Reason
    {
        get;
    }

    public string ErrorCode
    {
        get;
    }
}

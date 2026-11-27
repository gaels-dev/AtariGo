using System.Data.Common;

namespace AtariGo.Server.Tests.Credentials;

internal sealed class CredentialWriteFailureException : DbException
{
    public CredentialWriteFailureException() : base("Private test database detail.")
    {
    }
}

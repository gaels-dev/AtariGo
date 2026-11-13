using System.Data.Common;

namespace AtariGo.Server.Tests.Friends;

internal sealed class FriendTestDatabaseException : DbException
{
    public FriendTestDatabaseException(bool isTransient) : base("Private database detail.")
    {
        IsTransient = isTransient;
    }

    public override bool IsTransient { get; }
}

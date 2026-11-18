using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class AccountCredentialStore
{
    public const int MaximumIdentifierLength = 254;
    private readonly AtariGoDbContext _dbContext;

    public AccountCredentialStore(AtariGoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<User?> LockAccountAsync(string identifier, CancellationToken cancellationToken)
    {
        string normalized = identifier.Trim();
        if (normalized.Length is 0 or > MaximumIdentifierLength)
        {
            return Task.FromResult<User?>(null);
        }
        return _dbContext.Users.FromSqlInterpolated($"""
            SELECT * FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
            WHERE UserName = {normalized} OR Email = {normalized}
            """)
            .Include(user => user.Credential)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<User?> LockAccountAsync(int userId, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FromSqlInterpolated($"""
            SELECT * FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
            WHERE UserId = {userId}
            """)
            .Include(user => user.Credential).SingleOrDefaultAsync(cancellationToken);
    }
}

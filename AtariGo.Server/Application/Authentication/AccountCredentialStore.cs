using Microsoft.EntityFrameworkCore;

using AtariGo.Contracts.Validation;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class AccountCredentialStore
{
    public const int MaximumIdentifierLength = EmailAddressRules.MaximumLength;
    private readonly AtariGoDbContext _dbContext;

    public AccountCredentialStore(AtariGoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<User?> LockAccountAsync(string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        string normalized = identifier.Trim();
        if (normalized.Length is 0 or > MaximumIdentifierLength)
        {
            return null;
        }
        IQueryable<User> candidates = _dbContext.Users.FromSqlInterpolated($"""
            SELECT * FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
            WHERE UserName = {normalized} OR Email = {normalized}
            """)
            .Include(user => user.Credential);
        IQueryable<User> ordered = from user in candidates
                                  orderby user.UserId
                                  select user;
        List<User> matches = await ordered.Take(2).ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
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

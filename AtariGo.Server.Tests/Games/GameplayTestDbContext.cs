using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameplayTestDbContext : AtariGoDbContext
{
    public GameplayTestDbContext(DbContextOptions<AtariGoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // SQLite spelling of the same reserved-name rule; production SQL remains unchanged.
        modelBuilder.Entity<User>().ToTable("Users", table => table.HasCheckConstraint(
            "CK_Users_UserName_NotGuest",
            "lower(trim(UserName)) <> 'guest' "
                + "AND substr(lower(trim(UserName)), 1, 6) <> 'guest-'"));
    }
}

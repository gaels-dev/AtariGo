using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data.Configurations;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data;

public partial class AtariGoDbContext : DbContext
{
    public AtariGoDbContext(DbContextOptions<AtariGoDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuthenticationCode> AuthenticationCodes
    {
        get; set;
    }

    public virtual DbSet<Credential> Credentials
    {
        get; set;
    }

    public virtual DbSet<FriendRequest> FriendRequests
    {
        get; set;
    }

    public virtual DbSet<Friendship> Friendships
    {
        get; set;
    }

    public virtual DbSet<Game> Games
    {
        get; set;
    }

    public virtual DbSet<GameConfiguration> GameConfigurations
    {
        get; set;
    }

    public virtual DbSet<GameEvent> GameEvents
    {
        get; set;
    }

    public virtual DbSet<GamePlayer> GamePlayers
    {
        get; set;
    }

    public virtual DbSet<Invitation> Invitations
    {
        get; set;
    }

    public virtual DbSet<Move> Moves
    {
        get; set;
    }

    public virtual DbSet<Profile> Profiles
    {
        get; set;
    }

    public virtual DbSet<Report> Reports
    {
        get; set;
    }

    public virtual DbSet<Role> Roles
    {
        get; set;
    }

    public virtual DbSet<Room> Rooms
    {
        get; set;
    }

    public virtual DbSet<Sanction> Sanctions
    {
        get; set;
    }

    public virtual DbSet<Session> Sessions
    {
        get; set;
    }

    public virtual DbSet<User> Users
    {
        get; set;
    }

    public virtual DbSet<UserPreference> UserPreferences
    {
        get; set;
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CI_AI_SC");

        modelBuilder.ApplyConfiguration(new AuthenticationCodeEntityConfiguration());

        modelBuilder.ApplyConfiguration(new CredentialEntityConfiguration());

        modelBuilder.ApplyConfiguration(new FriendRequestEntityConfiguration());

        modelBuilder.ApplyConfiguration(new FriendshipEntityConfiguration());

        modelBuilder.ApplyConfiguration(new GameEntityConfiguration());

        modelBuilder.ApplyConfiguration(new GameConfigurationEntityConfiguration());

        modelBuilder.ApplyConfiguration(new GameEventEntityConfiguration());

        modelBuilder.ApplyConfiguration(new GamePlayerEntityConfiguration());

        modelBuilder.ApplyConfiguration(new InvitationEntityConfiguration());

        modelBuilder.ApplyConfiguration(new MoveEntityConfiguration());

        modelBuilder.ApplyConfiguration(new ProfileEntityConfiguration());

        modelBuilder.ApplyConfiguration(new ReportEntityConfiguration());

        modelBuilder.ApplyConfiguration(new RoleEntityConfiguration());

        modelBuilder.ApplyConfiguration(new RoomEntityConfiguration());

        modelBuilder.ApplyConfiguration(new SanctionEntityConfiguration());

        modelBuilder.ApplyConfiguration(new SessionEntityConfiguration());

        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());

        modelBuilder.ApplyConfiguration(new UserPreferenceEntityConfiguration());

        OnModelCreatingPartial(modelBuilder);
    }

}

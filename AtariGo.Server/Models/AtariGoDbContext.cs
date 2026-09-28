using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Models;

public partial class AtariGoDbContext : DbContext
{
    public AtariGoDbContext(DbContextOptions<AtariGoDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuthenticationCode> AuthenticationCodes { get; set; }

    public virtual DbSet<Credential> Credentials { get; set; }

    public virtual DbSet<FriendRequest> FriendRequests { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<GameConfiguration> GameConfigurations { get; set; }

    public virtual DbSet<GameEvent> GameEvents { get; set; }

    public virtual DbSet<GamePlayer> GamePlayers { get; set; }

    public virtual DbSet<Invitation> Invitations { get; set; }

    public virtual DbSet<Move> Moves { get; set; }

    public virtual DbSet<Profile> Profiles { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<Sanction> Sanctions { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserPreference> UserPreferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CI_AI_SC");

        modelBuilder.Entity<AuthenticationCode>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.CodeType, e.ExpiresAt }, "IX_AuthenticationCodes_User_Type_Expiry");

            entity.Property(e => e.CodeHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CodeType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_AuthenticationCodes_CreatedAt");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.UsedAt).HasPrecision(0);

            entity.HasOne(d => d.User).WithMany(p => p.AuthenticationCodes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuthenticationCodes_User");
        });

        modelBuilder.Entity<Credential>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.LockedUntil).HasPrecision(0);
            entity.Property(e => e.PasswordChangedAt).HasPrecision(0);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.Credential)
                .HasForeignKey<Credential>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Credentials_User");
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasIndex(e => new { e.RecipientId, e.State, e.RequestedAt }, "IX_FriendRequests_Recipient_State").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.RequesterId, e.RequestedAt }, "IX_FriendRequests_Requester").IsDescending(false, true);

            entity.HasIndex(e => new { e.RequesterId, e.RecipientId }, "UX_FriendRequests_PendingPair")
                .IsUnique()
                .HasFilter("([State]='Pending')");

            entity.Property(e => e.RequestedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_FriendRequests_RequestedAt");
            entity.Property(e => e.RespondedAt).HasPrecision(0);
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_FriendRequests_State");

            entity.HasOne(d => d.Recipient).WithMany(p => p.FriendRequestRecipients)
                .HasForeignKey(d => d.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FriendRequests_Recipient");

            entity.HasOne(d => d.Requester).WithMany(p => p.FriendRequestRequesters)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FriendRequests_Requester");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FriendUserId });

            entity.HasIndex(e => new { e.FriendUserId, e.UserId }, "IX_Friendships_FriendUserId");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Friendships_CreatedAt");

            entity.HasOne(d => d.FriendUser).WithMany(p => p.FriendshipFriendUsers)
                .HasForeignKey(d => d.FriendUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Friendships_Friend");

            entity.HasOne(d => d.User).WithMany(p => p.FriendshipUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Friendships_User");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasIndex(e => e.GameConfigurationId, "IX_Games_Configuration");

            entity.HasIndex(e => new { e.State, e.GameType }, "IX_Games_State_Type");

            entity.HasIndex(e => e.RoomId, "UX_Games_RoomId")
                .IsUnique()
                .HasFilter("([RoomId] IS NOT NULL)");

            entity.Property(e => e.GameId).ValueGeneratedOnAdd();
            entity.Property(e => e.EndReason)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.FinishedAt).HasPrecision(0);
            entity.Property(e => e.GameType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.StartedAt).HasPrecision(0);
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Waiting", "DF_Games_State");

            entity.HasOne(d => d.GameConfiguration).WithMany(p => p.Games)
                .HasForeignKey(d => d.GameConfigurationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Games_Configuration");

            entity.HasOne(d => d.Room).WithOne(p => p.Game)
                .HasForeignKey<Game>(d => d.RoomId)
                .HasConstraintName("FK_Games_Room");

            entity.HasOne(d => d.GamePlayer).WithMany(p => p.Games)
                .HasPrincipalKey(p => new { p.GameId, p.GamePlayerId })
                .HasForeignKey(d => new { d.GameId, d.CurrentTurnGamePlayerId })
                .HasConstraintName("FK_Games_CurrentTurnPlayer");
        });

        modelBuilder.Entity<GameConfiguration>(entity =>
        {
            entity.Property(e => e.CaptureGoal).HasDefaultValue((byte)1, "DF_GameConfigurations_CaptureGoal");
        });

        modelBuilder.Entity<GameEvent>(entity =>
        {
            entity.HasIndex(e => new { e.GameId, e.CreatedAt, e.GameEventId }, "IX_GameEvents_Game_Created");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_GameEvents_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EventType)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.Game).WithMany(p => p.GameEvents)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameEvents_Game");

            entity.HasOne(d => d.GamePlayer).WithMany(p => p.GameEvents)
                .HasPrincipalKey(p => new { p.GameId, p.GamePlayerId })
                .HasForeignKey(d => new { d.GameId, d.GamePlayerId })
                .HasConstraintName("FK_GameEvents_GamePlayer");
        });

        modelBuilder.Entity<GamePlayer>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_GamePlayers_UserId");

            entity.HasIndex(e => new { e.GameId, e.StoneColor }, "UQ_GamePlayers_Game_Color").IsUnique();

            entity.HasIndex(e => new { e.GameId, e.GamePlayerId }, "UQ_GamePlayers_Game_Player").IsUnique();

            entity.HasIndex(e => new { e.GameId, e.TurnOrder }, "UQ_GamePlayers_Game_TurnOrder").IsUnique();

            entity.HasIndex(e => new { e.GameId, e.UserId }, "UX_GamePlayers_Game_User")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");

            entity.Property(e => e.GuestName).HasMaxLength(100);
            entity.Property(e => e.JoinedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_GamePlayers_JoinedAt");
            entity.Property(e => e.LeftAt).HasPrecision(0);
            entity.Property(e => e.Result)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_GamePlayers_Result");
            entity.Property(e => e.StoneColor)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Game).WithMany(p => p.GamePlayers)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GamePlayers_Game");

            entity.HasOne(d => d.User).WithMany(p => p.GamePlayers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_GamePlayers_User");
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasIndex(e => new { e.RecipientId, e.State }, "IX_Invitations_Recipient_State").HasFilter("([RecipientId] IS NOT NULL)");

            entity.HasIndex(e => new { e.RoomId, e.State }, "IX_Invitations_Room_State");

            entity.HasIndex(e => new { e.SenderId, e.CreatedAt }, "IX_Invitations_Sender_Created").IsDescending(false, true);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Invitations_CreatedAt");
            entity.Property(e => e.InvitationType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RecipientEmail).HasMaxLength(254);
            entity.Property(e => e.RespondedAt).HasPrecision(0);
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_Invitations_State");

            entity.HasOne(d => d.Recipient).WithMany(p => p.InvitationRecipients)
                .HasForeignKey(d => d.RecipientId)
                .HasConstraintName("FK_Invitations_Recipient");

            entity.HasOne(d => d.Room).WithMany(p => p.Invitations)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invitations_Room");

            entity.HasOne(d => d.Sender).WithMany(p => p.InvitationSenders)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invitations_Sender");
        });

        modelBuilder.Entity<Move>(entity =>
        {
            entity.HasIndex(e => new { e.GamePlayerId, e.PlayedAt, e.MoveId }, "IX_Moves_GamePlayer_PlayedAt");

            entity.HasIndex(e => new { e.GamePlayerId, e.TurnNumber }, "UQ_Moves_Player_Turn").IsUnique();

            entity.Property(e => e.PlayedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Moves_PlayedAt");

            entity.HasOne(d => d.GamePlayer).WithMany(p => p.Moves)
                .HasForeignKey(d => d.GamePlayerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Moves_GamePlayer");
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.LastUpdate).HasPrecision(0);

            entity.HasOne(d => d.User).WithOne(p => p.Profile)
                .HasForeignKey<Profile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Profiles_User");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasIndex(e => new { e.ReportedUserId, e.CreatedAt }, "IX_Reports_ReportedUser").IsDescending(false, true);

            entity.HasIndex(e => e.ReviewerId, "IX_Reports_Reviewer").HasFilter("([ReviewerId] IS NOT NULL)");

            entity.HasIndex(e => new { e.State, e.CreatedAt }, "IX_Reports_State_Created");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Reports_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ReportReason)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ReviewedAt).HasPrecision(0);
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_Reports_State");

            entity.HasOne(d => d.Game).WithMany(p => p.Reports)
                .HasForeignKey(d => d.GameId)
                .HasConstraintName("FK_Reports_Game");

            entity.HasOne(d => d.ReportedUser).WithMany(p => p.ReportReportedUsers)
                .HasForeignKey(d => d.ReportedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_ReportedUser");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Reporter");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.ReportReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .HasConstraintName("FK_Reports_Reviewer");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Roles_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(30);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasIndex(e => new { e.CreatorId, e.State }, "IX_Rooms_Creator_State");

            entity.HasIndex(e => e.InvitationCode, "UQ_Rooms_InvitationCode").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Rooms_CreatedAt");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.InvitationCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Waiting", "DF_Rooms_State");

            entity.HasOne(d => d.Creator).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.CreatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Rooms_Creator");
        });

        modelBuilder.Entity<Sanction>(entity =>
        {
            entity.HasIndex(e => new { e.AdministratorId, e.StartedAt }, "IX_Sanctions_Administrator").IsDescending(false, true);

            entity.HasIndex(e => e.ReportId, "IX_Sanctions_Report").HasFilter("([ReportId] IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.State, e.StartedAt }, "IX_Sanctions_User_State").IsDescending(false, false, true);

            entity.Property(e => e.EndsAt).HasPrecision(0);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.SanctionType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.StartedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Sanctions_StartedAt");
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active", "DF_Sanctions_State");

            entity.HasOne(d => d.Administrator).WithMany(p => p.SanctionAdministrators)
                .HasForeignKey(d => d.AdministratorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sanctions_Administrator");

            entity.HasOne(d => d.Report).WithMany(p => p.Sanctions)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("FK_Sanctions_Report");

            entity.HasOne(d => d.User).WithMany(p => p.SanctionUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sanctions_User");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.State, e.ExpiresAt }, "IX_Sessions_User_State_Expiry");

            entity.HasIndex(e => e.SessionTokenHash, "UQ_Sessions_TokenHash").IsUnique();

            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.LastActivityAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Sessions_LastActivityAt");
            entity.Property(e => e.SessionTokenHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.StartedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Sessions_StartedAt");
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active", "DF_Sessions_State");

            entity.HasOne(d => d.User).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sessions_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_Users_RoleId");

            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.HasIndex(e => e.UserName, "UQ_Users_UserName").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.LastUpdate).HasPrecision(0);
            entity.Property(e => e.RegistrationDate)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Users_RegistrationDate");
            entity.Property(e => e.State)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active", "DF_Users_State");
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Role");
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.MusicEnabled).HasDefaultValue(true, "DF_UserPreferences_MusicEnabled");
            entity.Property(e => e.SoundEnabled).HasDefaultValue(true, "DF_UserPreferences_SoundEnabled");

            entity.HasOne(d => d.User).WithOne(p => p.UserPreference)
                .HasForeignKey<UserPreference>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserPreferences_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class User
{
    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string State { get; set; } = null!;

    public DateTime RegistrationDate { get; set; }

    public DateTime? LastUpdate { get; set; }

    public int RoleId { get; set; }

    public virtual ICollection<AuthenticationCode> AuthenticationCodes { get; set; } = new List<AuthenticationCode>();

    public virtual Credential? Credential { get; set; }

    public virtual ICollection<FriendRequest> FriendRequestRecipients { get; set; } = new List<FriendRequest>();

    public virtual ICollection<FriendRequest> FriendRequestRequesters { get; set; } = new List<FriendRequest>();

    public virtual ICollection<Friendship> FriendshipFriendUsers { get; set; } = new List<Friendship>();

    public virtual ICollection<Friendship> FriendshipUsers { get; set; } = new List<Friendship>();

    public virtual ICollection<GamePlayer> GamePlayers { get; set; } = new List<GamePlayer>();

    public virtual ICollection<Invitation> InvitationRecipients { get; set; } = new List<Invitation>();

    public virtual ICollection<Invitation> InvitationSenders { get; set; } = new List<Invitation>();

    public virtual Profile? Profile { get; set; }

    public virtual ICollection<Report> ReportReportedUsers { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportReporters { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportReviewers { get; set; } = new List<Report>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual ICollection<Sanction> SanctionAdministrators { get; set; } = new List<Sanction>();

    public virtual ICollection<Sanction> SanctionUsers { get; set; } = new List<Sanction>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual UserPreference? UserPreference { get; set; }
}

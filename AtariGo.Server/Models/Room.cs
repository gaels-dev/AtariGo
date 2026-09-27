using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Room
{
    public int RoomId { get; set; }

    public string InvitationCode { get; set; } = null!;

    public int CreatorId { get; set; }

    public string State { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual User Creator { get; set; } = null!;

    public virtual Game? Game { get; set; }

    public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
}

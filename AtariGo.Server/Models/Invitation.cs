using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Invitation
{
    public long InvitationId { get; set; }

    public int RoomId { get; set; }

    public int SenderId { get; set; }

    public int? RecipientId { get; set; }

    public string? RecipientEmail { get; set; }

    public string InvitationType { get; set; } = null!;

    public string State { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual User? Recipient { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}

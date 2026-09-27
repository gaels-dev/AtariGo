using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class FriendRequest
{
    public long FriendRequestId { get; set; }

    public int RequesterId { get; set; }

    public int RecipientId { get; set; }

    public string State { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual User Recipient { get; set; } = null!;

    public virtual User Requester { get; set; } = null!;
}

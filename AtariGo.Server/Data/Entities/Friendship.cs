using System;
using System.Collections.Generic;

namespace AtariGo.Server.Data.Entities;

public partial class Friendship
{
    public int UserId { get; set; }

    public int FriendUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User FriendUser { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

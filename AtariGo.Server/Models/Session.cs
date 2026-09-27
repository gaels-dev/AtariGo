using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Session
{
    public long SessionId { get; set; }

    public int UserId { get; set; }

    public string SessionTokenHash { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string State { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

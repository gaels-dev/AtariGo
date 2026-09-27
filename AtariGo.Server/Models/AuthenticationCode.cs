using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class AuthenticationCode
{
    public long AuthenticationCodeId { get; set; }

    public int UserId { get; set; }

    public string CodeType { get; set; } = null!;

    public string CodeHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public int AttemptCount { get; set; }

    public virtual User User { get; set; } = null!;
}

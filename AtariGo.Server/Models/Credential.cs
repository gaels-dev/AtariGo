using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Credential
{
    public int UserId { get; set; }

    public string PasswordHash { get; set; } = null!;

    public DateTime? PasswordChangedAt { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    public virtual User User { get; set; } = null!;
}

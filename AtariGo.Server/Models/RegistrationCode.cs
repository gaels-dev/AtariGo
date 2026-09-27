using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class RegistrationCode
{
    public int RegistrationCodeId { get; set; }

    public int PendingRegistrationId { get; set; }

    public string CodeHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public int AttemptCount { get; set; }

    public virtual PendingRegistration PendingRegistration { get; set; } = null!;
}

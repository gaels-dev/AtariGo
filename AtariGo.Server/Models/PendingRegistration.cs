using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class PendingRegistration
{
    public int PendingRegistrationId { get; set; }

    public string UserName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public virtual ICollection<RegistrationCode> RegistrationCodes { get; set; } = new List<RegistrationCode>();
}

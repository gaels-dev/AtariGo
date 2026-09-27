using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Profile
{
    public int UserId { get; set; }

    public byte[]? Avatar { get; set; }

    public string? Description { get; set; }

    public DateTime? LastUpdate { get; set; }

    public virtual User User { get; set; } = null!;
}

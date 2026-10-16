using System;
using System.Collections.Generic;

namespace AtariGo.Server.Data.Entities;

public partial class UserPreference
{
    public int UserId { get; set; }

    public bool SoundEnabled { get; set; }

    public bool MusicEnabled { get; set; }

    public virtual User User { get; set; } = null!;
}

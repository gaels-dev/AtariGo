using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class GameConfiguration
{
    public int GameConfigurationId { get; set; }

    public byte CaptureGoal { get; set; }

    public int GameTimeSeconds { get; set; }

    public int TurnTimeSeconds { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}

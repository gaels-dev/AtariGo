using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Move
{
    public long MoveId { get; set; }

    public long GamePlayerId { get; set; }

    public int TurnNumber { get; set; }

    public byte Row { get; set; }

    public byte Column { get; set; }

    public DateTime PlayedAt { get; set; }

    public int CapturedStones { get; set; }

    public virtual GamePlayer GamePlayer { get; set; } = null!;
}

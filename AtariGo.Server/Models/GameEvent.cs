using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class GameEvent
{
    public long GameEventId { get; set; }

    public long GameId { get; set; }

    public long? GamePlayerId { get; set; }

    public string EventType { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual GamePlayer? GamePlayer { get; set; }
}

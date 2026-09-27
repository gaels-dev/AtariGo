using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Game
{
    public long GameId { get; set; }

    public int? RoomId { get; set; }

    public int GameConfigurationId { get; set; }

    public string GameType { get; set; } = null!;

    public string State { get; set; } = null!;

    public long? CurrentTurnGamePlayerId { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? EndReason { get; set; }

    public virtual GameConfiguration GameConfiguration { get; set; } = null!;

    public virtual ICollection<GameEvent> GameEvents { get; set; } = new List<GameEvent>();

    public virtual GamePlayer? GamePlayer { get; set; }

    public virtual ICollection<GamePlayer> GamePlayers { get; set; } = new List<GamePlayer>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual Room? Room { get; set; }
}

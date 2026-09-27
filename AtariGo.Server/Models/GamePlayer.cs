using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class GamePlayer
{
    public long GamePlayerId { get; set; }

    public long GameId { get; set; }

    public int? UserId { get; set; }

    public string? GuestName { get; set; }

    public string StoneColor { get; set; } = null!;

    public byte TurnOrder { get; set; }

    public int CapturedStones { get; set; }

    public string Result { get; set; } = null!;

    public DateTime JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<GameEvent> GameEvents { get; set; } = new List<GameEvent>();

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual ICollection<Move> Moves { get; set; } = new List<Move>();

    public virtual User? User { get; set; }
}

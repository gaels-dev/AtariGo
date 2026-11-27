
namespace AtariGo.Server.Data.Entities;

public partial class Game
{
    public long StateVersion
    {
        get; set;
    }

    public long GameId
    {
        get; set;
    }

    public int? RoomId
    {
        get; set;
    }

    public int GameConfigurationId
    {
        get; set;
    }

    public string GameType { get; set; } = null!;

    public string State { get; set; } = null!;

    public DateTime? StartedAt
    {
        get; set;
    }

    public DateTime? FinishedAt
    {
        get; set;
    }

    public string? EndReason
    {
        get; set;
    }

    public virtual GameConfiguration GameConfiguration { get; set; } = null!;

    public virtual ICollection<GameEvent> GameEvents { get; set; } = new List<GameEvent>();

    public virtual ICollection<GamePlayer> GamePlayers { get; set; } = new List<GamePlayer>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual Room? Room
    {
        get; set;
    }
}

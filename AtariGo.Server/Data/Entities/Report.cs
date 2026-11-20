
namespace AtariGo.Server.Data.Entities;

public partial class Report
{
    public long ReportId
    {
        get; set;
    }

    public int ReporterId
    {
        get; set;
    }

    public int ReportedUserId
    {
        get; set;
    }

    public long GameId
    {
        get; set;
    }

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt
    {
        get; set;
    }

    public virtual Game Game
    {
        get; set;
    } = null!;

    public virtual User ReportedUser { get; set; } = null!;

    public virtual User Reporter { get; set; } = null!;

    public virtual Sanction? TriggeredSanction { get; set; }
}

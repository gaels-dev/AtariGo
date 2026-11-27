
namespace AtariGo.Server.Data.Entities;

public partial class Sanction
{
    public long SanctionId
    {
        get; set;
    }

    public int UserId
    {
        get; set;
    }

    public long TriggeringReportId
    {
        get; set;
    }

    public string SanctionType { get; set; } = null!;

    public int Threshold { get; set; }

    public DateTime StartedAt
    {
        get; set;
    }

    public DateTime EndsAt
    {
        get; set;
    }

    public DateTime? RevokedAt { get; set; }

    public virtual Report TriggeringReport
    {
        get; set;
    } = null!;

    public virtual User User { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Sanction
{
    public long SanctionId { get; set; }

    public int UserId { get; set; }

    public int AdministratorId { get; set; }

    public long? ReportId { get; set; }

    public string SanctionType { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string State { get; set; } = null!;

    public virtual User Administrator { get; set; } = null!;

    public virtual Report? Report { get; set; }

    public virtual User User { get; set; } = null!;
}

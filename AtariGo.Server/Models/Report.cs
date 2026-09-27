using System;
using System.Collections.Generic;

namespace AtariGo.Server.Models;

public partial class Report
{
    public long ReportId { get; set; }

    public int ReporterId { get; set; }

    public int ReportedUserId { get; set; }

    public long? GameId { get; set; }

    public string ReportReason { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string State { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewerId { get; set; }

    public virtual Game? Game { get; set; }

    public virtual User ReportedUser { get; set; } = null!;

    public virtual User Reporter { get; set; } = null!;

    public virtual User? Reviewer { get; set; }

    public virtual ICollection<Sanction> Sanctions { get; set; } = new List<Sanction>();
}

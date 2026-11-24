using System.Diagnostics;

namespace AtariGo.Diagnostics;

public sealed record DiagnosticContext
{
    public long? GameId { get; init; }

    public int? RoomId { get; init; }

    public int? UserId { get; init; }

    public long? RequestId { get; init; }

    public long? InvitationId { get; init; }

    public Guid? OperationId { get; init; }

    public Guid? ActionId { get; init; }

    public ActivityTraceId? TraceId { get; init; }
}

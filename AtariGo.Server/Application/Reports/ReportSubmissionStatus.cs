namespace AtariGo.Server.Application.Reports;

public enum ReportSubmissionStatus
{
    Accepted,
    AlreadyReported,
    NotParticipant,
    OpponentIsGuest,
    ChatMuted,
    InvalidRequest,
    DescriptionTooLong
}

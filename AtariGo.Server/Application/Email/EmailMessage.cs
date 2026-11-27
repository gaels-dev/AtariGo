namespace AtariGo.Server.Application.Email;

public sealed class EmailMessage
{
    public string Recipient { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}

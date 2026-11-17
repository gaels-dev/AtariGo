namespace AtariGo.Server.Application.Email;

public sealed class SmtpOptions
{
    public const int ImplicitTlsPort = 465;

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = ImplicitTlsPort;

    public string Sender { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

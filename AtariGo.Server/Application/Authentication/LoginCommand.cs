namespace AtariGo.Server.Application.Authentication;

public sealed class LoginCommand
{
    public string Identifier { get; init; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string CultureName { get; init; } = string.Empty;
}

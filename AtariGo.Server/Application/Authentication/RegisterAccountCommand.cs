namespace AtariGo.Server.Application.Authentication;

public sealed record RegisterAccountCommand
{
    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string PasswordConfirmation { get; set; } = string.Empty;

}

namespace AtariGo.Server.Application.Authentication;

public sealed record RegisterAccountCommand
{
    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string PasswordConfirmation { get; init; } = string.Empty;

    public string ConfirmationText { get; init; } = string.Empty;
}

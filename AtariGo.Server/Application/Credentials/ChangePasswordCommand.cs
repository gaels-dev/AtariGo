namespace AtariGo.Server.Application.Credentials;

public sealed class ChangePasswordCommand
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string Confirmation { get; init; } = string.Empty;
}

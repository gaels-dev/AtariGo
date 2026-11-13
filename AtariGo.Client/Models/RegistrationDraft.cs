namespace AtariGo.Client.Models;

public sealed class RegistrationDraft
{
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string PasswordConfirmation { get; set; } = string.Empty;

    public void ClearSensitiveData()
    {
        Password = string.Empty;
        PasswordConfirmation = string.Empty;
    }
}

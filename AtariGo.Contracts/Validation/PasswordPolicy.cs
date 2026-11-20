namespace AtariGo.Contracts.Validation;

public static class PasswordPolicy
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 255;

    public static bool IsValid(string? password)
    {
        return !string.IsNullOrWhiteSpace(password)
            && password.Length >= MinimumPasswordLength
            && password.Length <= MaximumPasswordLength
            && password.Any(char.IsUpper);
    }
}

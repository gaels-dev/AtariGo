namespace AtariGo.Client.ViewModels.Dialogs;

internal static class PasswordPolicyExtensions
{
    public static bool ContainsUppercaseLetter(this string value)
    {
        foreach (char character in value)
        {
            if (char.IsUpper(character))
            {
                return true;
            }
        }

        return false;
    }
}
